using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class MarketingWorkbenchRepository : IMarketingWorkbenchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public MarketingWorkbenchRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<MarketingWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default)
    {
        const string campaignsSql = @"
SELECT TOP 50
    c.CampaignId AS ItemId,
    'campaigns' AS QueueCode,
    c.Name AS Title,
    CONCAT('MKT-CMP-', RIGHT(CONVERT(NVARCHAR(36), c.CampaignId), 6)) AS RefNumber,
    '' AS ContactName,
    c.Name AS CampaignName,
    c.Type AS Channel,
    '' AS ContentType,
    c.Segment AS TargetAudience,
    'Tenant Admin' AS AssignedTo,
    '' AS ReviewedBy,
    '' AS Location,
    CASE WHEN c.Status = 'Active' AND c.OpenRate < 18 THEN 'High' ELSE 'Normal' END AS Priority,
    CASE WHEN c.Status = 'Active' AND c.OpenRate < 18 THEN 'At Risk' ELSE 'On Track' END AS SlaStatus,
    CASE WHEN c.Status = 'Completed' THEN 'Complete' ELSE c.Status END AS Status,
    COALESCE(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(pr.JsonData, '$.budget')), c.Revenue * 0.08) AS Budget,
    0 AS EstPremium,
    c.Reached AS Leads,
    c.Conversions AS Conversions,
    0 AS Attendees,
    DATEADD(day, 21, CAST(c.StartDate AS date)) AS DueDate,
    DATEADD(day, 45, CAST(c.StartDate AS date)) AS EndDate,
    NULL AS EventDate,
    NULL AS ReceivedDate,
    COALESCE(JSON_VALUE(pr.JsonData, '$.notes'), CONCAT('Campaign reached ', FORMAT(c.Reached, 'N0'), ' contacts with ', FORMAT(c.OpenRate, 'N1'), '% open rate.')) AS Notes,
    '/marketing/campaigns' AS DetailUrl
FROM Comms.Campaign c
LEFT JOIN Portal.AdminRecord pr ON pr.TenantId = c.TenantId AND pr.Kind = 'MarketingWorkbench' AND pr.Code = CONCAT('CMP-', CONVERT(NVARCHAR(36), c.CampaignId)) AND pr.IsDeleted = 0
WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
ORDER BY CASE WHEN c.Status = 'Active' THEN 0 WHEN c.Status = 'Scheduled' THEN 1 ELSE 2 END, c.StartDate DESC;";

        const string outreachSql = @"
SELECT TOP 50
    oc.OutreachContactId AS ItemId,
    'outreach' AS QueueCode,
    oc.Reason AS Title,
    CONCAT('MKT-OUT-', RIGHT(CONVERT(NVARCHAR(36), oc.OutreachContactId), 6)) AS RefNumber,
    oc.ContactName,
    '' AS CampaignName,
    CASE WHEN oc.Phone IS NOT NULL AND oc.Phone <> '' THEN 'Phone' ELSE 'Email' END AS Channel,
    '' AS ContentType,
    '' AS TargetAudience,
    COALESCE(NULLIF(oc.AssignedTo, ''), 'Tenant Admin') AS AssignedTo,
    '' AS ReviewedBy,
    COALESCE(oc.Branch, '') AS Location,
    oc.Priority,
    CASE WHEN oc.NextContactDate < CAST(SYSUTCDATETIME() AS date) THEN 'Breached' WHEN oc.NextContactDate <= DATEADD(day, 1, CAST(SYSUTCDATETIME() AS date)) THEN 'At Risk' ELSE 'On Track' END AS SlaStatus,
    oc.Status,
    0 AS Budget,
    0 AS EstPremium,
    0 AS Leads,
    0 AS Conversions,
    0 AS Attendees,
    COALESCE(CAST(oc.NextContactDate AS DATETIME2), DATEADD(day, 2, oc.CreatedDateUtc)) AS DueDate,
    DATEADD(day, 30, oc.CreatedDateUtc) AS EndDate,
    NULL AS EventDate,
    NULL AS ReceivedDate,
    COALESCE(NULLIF(oc.Notes, ''), oc.LastOutcome, 'Follow up with marketing contact.') AS Notes,
    '/communications/outreach' AS DetailUrl
FROM Comms.OutreachContact oc
WHERE oc.TenantId = @TenantId AND oc.IsDeleted = 0 AND oc.Status <> 'Opted Out'
ORDER BY DueDate, CASE oc.Priority WHEN 'Critical' THEN 0 WHEN 'Urgent' THEN 1 WHEN 'High' THEN 2 ELSE 3 END;";

        const string adminSql = @"
SELECT
    pr.PortalAdminRecordId AS ItemId,
    JSON_VALUE(pr.JsonData, '$.queueCode') AS QueueCode,
    pr.Name AS Title,
    pr.Code AS RefNumber,
    COALESCE(JSON_VALUE(pr.JsonData, '$.contactName'), '') AS ContactName,
    COALESCE(JSON_VALUE(pr.JsonData, '$.campaignName'), '') AS CampaignName,
    COALESCE(JSON_VALUE(pr.JsonData, '$.channel'), '') AS Channel,
    COALESCE(JSON_VALUE(pr.JsonData, '$.contentType'), '') AS ContentType,
    COALESCE(JSON_VALUE(pr.JsonData, '$.targetAudience'), '') AS TargetAudience,
    COALESCE(JSON_VALUE(pr.JsonData, '$.assignedTo'), 'Tenant Admin') AS AssignedTo,
    COALESCE(JSON_VALUE(pr.JsonData, '$.reviewedBy'), '') AS ReviewedBy,
    COALESCE(JSON_VALUE(pr.JsonData, '$.location'), '') AS Location,
    COALESCE(JSON_VALUE(pr.JsonData, '$.priority'), 'Normal') AS Priority,
    COALESCE(JSON_VALUE(pr.JsonData, '$.slaStatus'), 'On Track') AS SlaStatus,
    COALESCE(JSON_VALUE(pr.JsonData, '$.status'), pr.Status) AS Status,
    COALESCE(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(pr.JsonData, '$.budget')), 0) AS Budget,
    COALESCE(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(pr.JsonData, '$.estPremium')), 0) AS EstPremium,
    COALESCE(TRY_CONVERT(INT, JSON_VALUE(pr.JsonData, '$.leads')), 0) AS Leads,
    COALESCE(TRY_CONVERT(INT, JSON_VALUE(pr.JsonData, '$.conversions')), 0) AS Conversions,
    COALESCE(TRY_CONVERT(INT, JSON_VALUE(pr.JsonData, '$.attendees')), 0) AS Attendees,
    COALESCE(TRY_CONVERT(DATETIME2, JSON_VALUE(pr.JsonData, '$.dueDate')), pr.CreatedDateUtc) AS DueDate,
    COALESCE(TRY_CONVERT(DATETIME2, JSON_VALUE(pr.JsonData, '$.endDate')), DATEADD(day, 30, pr.CreatedDateUtc)) AS EndDate,
    TRY_CONVERT(DATETIME2, JSON_VALUE(pr.JsonData, '$.eventDate')) AS EventDate,
    TRY_CONVERT(DATETIME2, JSON_VALUE(pr.JsonData, '$.receivedDate')) AS ReceivedDate,
    COALESCE(JSON_VALUE(pr.JsonData, '$.notes'), '') AS Notes,
    COALESCE(JSON_VALUE(pr.JsonData, '$.detailUrl'), '/workbench/marketing') AS DetailUrl
FROM Portal.AdminRecord pr
WHERE pr.TenantId = @TenantId
  AND pr.Kind = 'MarketingWorkbench'
  AND pr.IsDeleted = 0
  AND JSON_VALUE(pr.JsonData, '$.queueCode') IN ('referrals','events','content')
ORDER BY DueDate, Title;";

        const string leadSourcesSql = @"
SELECT
    COALESCE(NULLIF(l.SourceCode, ''), 'Unknown') AS SourceName,
    COUNT(1) AS Leads,
    SUM(CASE WHEN l.StatusCodeId = 4 THEN 1 ELSE 0 END) AS Converted,
    CAST(AVG(COALESCE(o.EstimatedAmount, 0)) AS DECIMAL(18,2)) AS AvgPremium
FROM CRM.Lead l
LEFT JOIN CRM.Opportunity o ON o.TenantId = l.TenantId AND o.IsDeleted = 0 AND o.AccountId IS NOT NULL AND o.CreatedDateUtc >= DATEADD(day, -120, SYSUTCDATETIME())
WHERE l.TenantId = @TenantId AND l.IsDeleted = 0 AND l.CreatedDateUtc >= DATEADD(day, -180, SYSUTCDATETIME())
GROUP BY COALESCE(NULLIF(l.SourceCode, ''), 'Unknown')
UNION ALL
SELECT SourceName, Leads, Converted, AvgPremium
FROM (
    SELECT
        JSON_VALUE(pr.JsonData, '$.sourceName') AS SourceName,
        COALESCE(TRY_CONVERT(INT, JSON_VALUE(pr.JsonData, '$.leads')), 0) AS Leads,
        COALESCE(TRY_CONVERT(INT, JSON_VALUE(pr.JsonData, '$.converted')), 0) AS Converted,
        COALESCE(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(pr.JsonData, '$.avgPremium')), 0) AS AvgPremium
    FROM Portal.AdminRecord pr
    WHERE pr.TenantId = @TenantId AND pr.Kind = 'MarketingLeadSource' AND pr.IsDeleted = 0
) s
WHERE SourceName IS NOT NULL AND SourceName <> ''
ORDER BY Leads DESC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var p = new { TenantId = tenantId };

        var campaigns = (await cn.QueryAsync<MarketingWorkbenchItemDto>(new CommandDefinition(campaignsSql, p, cancellationToken: cancellationToken))).AsList();
        var outreach = (await cn.QueryAsync<MarketingWorkbenchItemDto>(new CommandDefinition(outreachSql, p, cancellationToken: cancellationToken))).AsList();
        var adminItems = (await cn.QueryAsync<MarketingWorkbenchItemDto>(new CommandDefinition(adminSql, p, cancellationToken: cancellationToken))).AsList();
        var leadSources = (await cn.QueryAsync<MarketingWorkbenchLeadSourceDto>(new CommandDefinition(leadSourcesSql, p, cancellationToken: cancellationToken))).AsList();

        var referrals = adminItems.Where(i => i.QueueCode == "referrals").ToList();
        var events = adminItems.Where(i => i.QueueCode == "events").ToList();
        var content = adminItems.Where(i => i.QueueCode == "content").ToList();
        var totalLeads = campaigns.Sum(i => i.Leads) + leadSources.Sum(i => i.Leads);
        var converted = campaigns.Sum(i => i.Conversions) + leadSources.Sum(i => i.Converted);
        var totalBudget = campaigns.Sum(i => i.Budget);

        return new MarketingWorkbenchDto
        {
            Counts = new MarketingWorkbenchCountsDto
            {
                ActiveCampaigns = campaigns.Count(i => i.Status == "Active"),
                CampaignLeads = campaigns.Sum(i => i.Leads),
                OutreachTasks = outreach.Count,
                OutreachOverdue = outreach.Count(i => i.DueDate.Date < DateTime.UtcNow.Date),
                Referrals = referrals.Count,
                ReferralsConverted = referrals.Count(i => i.Status is "Converted" or "Complete"),
                UpcomingEvents = events.Count(i => i.EventDate.HasValue && i.EventDate.Value.Date >= DateTime.UtcNow.Date),
                EventFollowUps = events.Count(i => i.DueDate.Date <= DateTime.UtcNow.Date.AddDays(7) && i.Status != "Complete"),
                ContentPendingApproval = content.Count(i => i.Status == "Pending Approval"),
                TotalLeads = totalLeads,
                LeadsConverted = converted,
                ConversionRate = totalLeads == 0 ? 0 : Math.Round(converted * 100d / totalLeads, 1),
                CostPerLead = totalLeads == 0 ? 0 : Math.Round(totalBudget / totalLeads, 2),
            },
            Campaigns = campaigns,
            Outreach = outreach,
            Referrals = referrals,
            Events = events,
            Content = content,
            LeadSources = leadSources,
        };
    }

    public async Task ApproveContentAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Portal.AdminRecord
SET Status = 'Approved',
    JsonData = JSON_MODIFY(JsonData, '$.status', 'Approved'),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE PortalAdminRecordId = @ItemId
  AND TenantId = @TenantId
  AND Kind = 'MarketingWorkbench'
  AND JSON_VALUE(JsonData, '$.queueCode') = 'content'
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, ItemId = itemId }, cancellationToken: cancellationToken));
    }
}
