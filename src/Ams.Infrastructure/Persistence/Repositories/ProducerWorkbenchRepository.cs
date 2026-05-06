using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ProducerWorkbenchRepository : IProducerWorkbenchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ProducerWorkbenchRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ProducerWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default)
    {
        // ── Goal ──────────────────────────────────────────────────────────────
        const string goalSql = @"
SELECT
    ISNULL((SELECT SUM(ag.TotalContractValue)
            FROM Sales.Agreement ag
            WHERE ag.TenantId = @TenantId
              AND YEAR(ag.CreatedDateUtc) = YEAR(GETUTCDATE())
              AND ag.IsDeleted = 0
              AND ag.AgreementStatusCodeId = 1
              AND (@UserId IS NULL OR ag.CreatedByUserId = @UserId)), 0)   AS WrittenPremium,
    ISNULL((SELECT COUNT(1)
            FROM Sales.Agreement ag
            WHERE ag.TenantId = @TenantId
              AND YEAR(ag.CreatedDateUtc) = YEAR(GETUTCDATE())
              AND ag.IsDeleted = 0
              AND ag.AgreementStatusCodeId = 1
              AND (@UserId IS NULL OR ag.CreatedByUserId = @UserId)), 0)   AS NewPolicies,
    ISNULL((SELECT SUM(o.EstimatedAmount)
            FROM CRM.Opportunity o
            WHERE o.TenantId = @TenantId AND o.IsDeleted = 0 AND o.StatusCodeId = 1
              AND (@UserId IS NULL OR o.OwnerUserId = @UserId)), 0)        AS PipelineValue,
    (SELECT COUNT(1)
     FROM Core.Notification n
     WHERE n.TenantId = @TenantId AND n.IsDeleted = 0 AND n.IsRead = 0
       AND (@UserId IS NULL OR n.RecipientUserId = @UserId))               AS UnreadMessages;";

        // ── KPI counts ─────────────────────────────────────────────────────────
        const string kpiSql = @"
SELECT
    (SELECT COUNT(1) FROM CRM.Lead
     WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId NOT IN (3,4,5)
       AND (@UserId IS NULL OR AssignedToUserId = @UserId))                    AS AssignedLeads,
    (SELECT COUNT(1) FROM CRM.Lead
     WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId NOT IN (3,4,5)
       AND Score >= 70
       AND (@UserId IS NULL OR AssignedToUserId = @UserId))                    AS HotLeads,
    (SELECT COUNT(1) FROM CRM.Opportunity
     WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId = 1
       AND (@UserId IS NULL OR OwnerUserId = @UserId))                         AS OpenOpportunities,
    ISNULL((SELECT SUM(EstimatedAmount) FROM CRM.Opportunity
     WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId = 1
       AND (@UserId IS NULL OR OwnerUserId = @UserId)), 0)                     AS OppsPremium,
    (SELECT COUNT(1) FROM CRM.Quote q
     JOIN Client.Account a ON a.AccountId = q.AccountId
     WHERE q.TenantId = @TenantId AND q.IsDeleted = 0 AND q.StatusCode = 'Presented'
       AND (@UserId IS NULL OR q.CreatedByUserId = @UserId))                   AS QuoteFollowups,
    (SELECT COUNT(1) FROM CRM.Quote q
     WHERE q.TenantId = @TenantId AND q.IsDeleted = 0 AND q.StatusCode = 'Presented'
       AND q.ValidUntilDate < CAST(GETUTCDATE() AS DATE)
       AND (@UserId IS NULL OR q.CreatedByUserId = @UserId))                   AS OverdueQuotes,
    (SELECT COUNT(1) FROM OPS.AgreementRenewal ar
      JOIN Sales.Agreement ag ON ag.AgreementId = ar.AgreementId
     WHERE ar.TenantId = @TenantId AND ar.IsDeleted = 0 AND ar.StatusCode = 'Pending'
       AND ar.NewStartDate <= DATEADD(DAY, 90, CAST(GETUTCDATE() AS DATE))
       AND (@UserId IS NULL OR ag.CreatedByUserId = @UserId))                  AS RenewalCallList,
    (SELECT COUNT(1) FROM OPS.AgreementRenewal ar
     WHERE ar.TenantId = @TenantId AND ar.IsDeleted = 0
       AND MONTH(ar.NewStartDate) = MONTH(GETUTCDATE()) AND YEAR(ar.NewStartDate) = YEAR(GETUTCDATE()))
                                                                               AS RenewalsThisMonth,
    0                                                                          AS CrossSellList,
    0                                                                          AS CrossSellPremium,
    (SELECT COUNT(1) FROM Core.Notification n
     WHERE n.TenantId = @TenantId AND n.IsDeleted = 0 AND n.IsRead = 0
       AND (@UserId IS NULL OR n.RecipientUserId = @UserId))                   AS UnreadMessages;";

        // ── Leads ──────────────────────────────────────────────────────────────
        const string leadsSql = @"
SELECT TOP 50
    l.LeadId, l.LeadNumber, l.FirstName, l.LastName, l.AccountName,
    l.Email, l.Phone, l.InterestedService, l.Score, l.PriorityCode, l.SourceCode,
    l.StatusCodeId, l.CreatedDateUtc,
    (SELECT MAX(la.ActivityDate) FROM CRM.LeadActivity la
     WHERE la.LeadId = l.LeadId AND la.IsDeleted = 0) AS LastActivityDate,
    (SELECT TOP 1 la.Subject FROM CRM.LeadActivity la
     WHERE la.LeadId = l.LeadId AND la.IsDeleted = 0 AND la.IsCompleted = 0
     ORDER BY la.ActivityDate ASC) AS NextAction,
    (SELECT TOP 1 CAST(la.ActivityDate AS DATETIME) FROM CRM.LeadActivity la
     WHERE la.LeadId = l.LeadId AND la.IsDeleted = 0 AND la.IsCompleted = 0
     ORDER BY la.ActivityDate ASC) AS NextActionDate
FROM CRM.Lead l
WHERE l.TenantId = @TenantId AND l.IsDeleted = 0
  AND l.StatusCodeId NOT IN (4, 5)
  AND (@UserId IS NULL OR l.AssignedToUserId = @UserId)
ORDER BY l.Score DESC, l.CreatedDateUtc DESC;";

        // ── Opportunities ──────────────────────────────────────────────────────
        const string oppsSql = @"
SELECT TOP 50
    o.OpportunityId, o.OpportunityNumber, o.OpportunityName,
    a.AccountName, o.EstimatedAmount, o.WinProbability,
    o.ForecastCategoryCode AS StageName, o.ForecastCategoryCode,
    o.CloseDate, o.StatusCodeId,
    (SELECT TOP 1 la.Subject FROM CRM.LeadActivity la
     WHERE la.OpportunityId = o.OpportunityId AND la.IsDeleted = 0 AND la.IsCompleted = 0
     ORDER BY la.ActivityDate ASC) AS NextAction
FROM CRM.Opportunity o
LEFT JOIN Client.Account a ON a.AccountId = o.AccountId
WHERE o.TenantId = @TenantId AND o.IsDeleted = 0 AND o.StatusCodeId = 1
  AND (@UserId IS NULL OR o.OwnerUserId = @UserId)
ORDER BY o.EstimatedAmount DESC;";

        // ── Quote Follow-ups ───────────────────────────────────────────────────
        const string quotesSql = @"
SELECT TOP 50
    q.QuoteId, q.QuoteNumber, a.AccountName, o.OpportunityName,
    q.TotalAmount, q.ValidUntilDate, q.StatusCode, q.CreatedDateUtc
FROM CRM.Quote q
LEFT JOIN Client.Account   a ON a.AccountId     = q.AccountId
LEFT JOIN CRM.Opportunity  o ON o.OpportunityId = q.OpportunityId
WHERE q.TenantId = @TenantId AND q.IsDeleted = 0 AND q.StatusCode = 'Presented'
  AND (@UserId IS NULL OR q.CreatedByUserId = @UserId)
ORDER BY q.ValidUntilDate ASC;";

        // ── Renewals ───────────────────────────────────────────────────────────
        const string renewalsSql = @"
SELECT TOP 50
    ar.RenewalId, ar.RenewalNumber, ar.AgreementId,
    a.AccountName, ag.AgreementNumber,
    ar.TotalContractValue, ar.NewStartDate, ar.NewEndDate,
    ar.StatusCode, ar.CreatedDateUtc
FROM OPS.AgreementRenewal ar
JOIN Sales.Agreement  ag ON ag.AgreementId = ar.AgreementId
LEFT JOIN Client.Account a ON a.AccountId    = ag.AccountId
WHERE ar.TenantId = @TenantId AND ar.IsDeleted = 0 AND ar.StatusCode = 'Pending'
  AND ar.NewStartDate <= DATEADD(DAY, 90, CAST(GETUTCDATE() AS DATE))
ORDER BY ar.NewStartDate ASC;";

        // ── Notifications (messages) ───────────────────────────────────────────
        const string msgsSql = @"
SELECT TOP 30
    n.NotificationId, n.Subject, n.Body, n.ChannelCode,
    n.EntityName, n.EntityId, n.IsRead, n.CreatedDateUtc
FROM Core.Notification n
WHERE n.TenantId = @TenantId AND n.IsDeleted = 0
  AND (@UserId IS NULL OR n.RecipientUserId = @UserId)
ORDER BY n.IsRead ASC, n.CreatedDateUtc DESC;";

        // ── Cross-sell opportunities ───────────────────────────────────────────
        const string crossSellSql = @"
SELECT TOP 50
    a.AccountId,
    a.AccountName,
    COALESCE(JSON_VALUE(pe.JsonData, '$.currentLobs'), 'GL, WC') AS CurrentLobs,
    COALESCE(JSON_VALUE(pe.JsonData, '$.targetLob'), 'Umbrella') AS TargetLob,
    COALESCE(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(pe.JsonData, '$.oppPremium')), 25000) AS OppPremium,
    COALESCE(TRY_CONVERT(FLOAT, JSON_VALUE(pe.JsonData, '$.score')), 72) AS Score,
    COALESCE(JSON_VALUE(pe.JsonData, '$.reason'), 'Account has complementary coverage gap based on current book profile.') AS Reason,
    TRY_CONVERT(DATETIME2, JSON_VALUE(pe.JsonData, '$.lastContact')) AS LastContact
FROM Client.Account a
LEFT JOIN Portal.AdminRecord pe
    ON pe.TenantId = a.TenantId
   AND pe.Kind = 'ProducerCrossSell'
   AND pe.Code = CONVERT(NVARCHAR(36), a.AccountId)
   AND pe.IsDeleted = 0
WHERE a.TenantId = @TenantId
  AND a.IsDeleted = 0
  AND a.StatusCodeId = 1
ORDER BY Score DESC, a.AccountName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var p = new { TenantId = tenantId, UserId = userId };

        var goal     = await cn.QuerySingleAsync<WorkbenchGoalDto>(
                           new CommandDefinition(goalSql, p, cancellationToken: cancellationToken));
        var counts   = await cn.QuerySingleAsync<WorkbenchKpiCountsDto>(
                           new CommandDefinition(kpiSql, p, cancellationToken: cancellationToken));
        var leads    = (await cn.QueryAsync<WorkbenchLeadDto>(
                           new CommandDefinition(leadsSql, p, cancellationToken: cancellationToken))).AsList();
        var opps     = (await cn.QueryAsync<WorkbenchOpportunityDto>(
                           new CommandDefinition(oppsSql, p, cancellationToken: cancellationToken))).AsList();
        var quotes   = (await cn.QueryAsync<WorkbenchQuoteFollowupDto>(
                           new CommandDefinition(quotesSql, p, cancellationToken: cancellationToken))).AsList();
        var renewals = (await cn.QueryAsync<WorkbenchRenewalDto>(
                           new CommandDefinition(renewalsSql, p, cancellationToken: cancellationToken))).AsList();
        var msgs     = (await cn.QueryAsync<WorkbenchNotificationDto>(
                           new CommandDefinition(msgsSql, p, cancellationToken: cancellationToken))).AsList();
        var crossSell = (await cn.QueryAsync<WorkbenchCrossSellDto>(
                            new CommandDefinition(crossSellSql, p, cancellationToken: cancellationToken))).AsList();

        // Sync unread count between goal and counts
        goal.UnreadMessages   = counts.UnreadMessages;
        goal.WrittenPremium   = goal.WrittenPremium;
        goal.GoalPremium      = 1_800_000; // TODO: load from tenant goal settings table when available
        goal.RetentionRate    = 91.4;      // TODO: calculate from policy retention when table available

        counts.AssignedLeads     = counts.AssignedLeads;
        counts.OppsPremium       = opps.Sum(o => o.EstimatedAmount);
        counts.OpenOpportunities = opps.Count(o => o.StatusCodeId == 1);
        counts.QuoteFollowups    = quotes.Count;
        counts.RenewalCallList   = renewals.Count;
        counts.CrossSellList     = crossSell.Count;
        counts.CrossSellPremium  = crossSell.Sum(c => c.OppPremium);

        return new ProducerWorkbenchDto
        {
            Goal            = goal,
            Counts          = counts,
            MyLeads         = leads,
            MyOpportunities = opps,
            QuoteFollowups  = quotes,
            RenewalCallList = renewals,
            CrossSellList   = crossSell,
            Messages        = msgs,
        };
    }

    public async Task<string> GetNextLeadNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT ISNULL(MAX(CAST(SUBSTRING(LeadNumber, 5, 10) AS INT)), 0) + 1
FROM CRM.Lead
WHERE TenantId = @TenantId AND LeadNumber LIKE 'LDN-%';";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var seq = await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return $"LDN-{seq:D4}";
    }

    public async Task LogContactAsync(Guid tenantId, Guid itemId, string itemType, CancellationToken cancellationToken = default)
    {
        // Insert a completed lead activity of type Call
        const string sql = @"
INSERT INTO CRM.LeadActivity
    (ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode, Subject,
     ActivityDate, IsCompleted, OutcomeCode, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId,
     CASE WHEN @ItemType = 'Lead'        THEN @ItemId ELSE NULL END,
     CASE WHEN @ItemType = 'Opportunity' THEN @ItemId ELSE NULL END,
     'Call', 'Contact logged from workbench',
     CAST(GETUTCDATE() AS DATE), 1, 'Contacted', SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, ItemId = itemId, ItemType = itemType },
            cancellationToken: cancellationToken));
    }
}
