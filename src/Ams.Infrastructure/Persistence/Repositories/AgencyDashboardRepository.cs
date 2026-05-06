using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AgencyDashboardRepository : IAgencyDashboardRepository
{
    private readonly ISqlConnectionFactory _db;
    public AgencyDashboardRepository(ISqlConnectionFactory db) => _db = db;

    private const decimal AnnualPremiumGoal = 2500000m;

    // ── Executive Overview ───────────────────────────────────────────
    public async Task<AgencyExecutiveOverviewDto> GetExecutiveOverviewAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @Now  DATE = CAST(GETUTCDATE() AS DATE);
DECLARE @Som  DATE = DATEFROMPARTS(YEAR(@Now), MONTH(@Now), 1);
DECLARE @Soy  DATE = DATEFROMPARTS(YEAR(@Now), 1, 1);

SELECT
    t.TenantName                                                                        AS AgencyName,
    CAST('' AS NVARCHAR(50))                                                            AS AgencyCode,

    -- Written premium
    ISNULL((SELECT SUM(ag.TotalContractValue)
            FROM Sales.Agreement ag
            WHERE ag.TenantId = @TenantId AND ag.IsDeleted = 0
              AND ag.CreatedDateUtc >= @Som), 0)                                   AS WrittenPremiumMtd,

    ISNULL((SELECT SUM(ag.TotalContractValue)
            FROM Sales.Agreement ag
            WHERE ag.TenantId = @TenantId AND ag.IsDeleted = 0
              AND ag.CreatedDateUtc >= @Soy), 0)                                   AS WrittenPremiumYtd,

    @AnnualPremiumGoal                                                             AS WrittenPremiumGoal,

    ISNULL(CAST((SELECT COUNT(1) FROM OPS.AgreementRenewal r WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND r.StatusCode IN ('Renewed','Complete','Won')) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM OPS.AgreementRenewal r2 WHERE r2.TenantId=@TenantId AND r2.IsDeleted=0),0) AS DECIMAL(5,2)), 0) AS RetentionRate,

    -- Lead conversion
    ISNULL(CAST(
        (SELECT COUNT(1) FROM CRM.Lead l
         WHERE l.TenantId = @TenantId AND l.IsDeleted = 0
           AND l.StatusCodeId = 3                 -- Converted
           AND l.CreatedDateUtc >= @Soy) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM CRM.Lead l2
         WHERE l2.TenantId = @TenantId AND l2.IsDeleted = 0
           AND l2.CreatedDateUtc >= @Soy), 0)
    AS DECIMAL(5,2)), 0)                                                            AS ConversionRate,

    (SELECT COUNT(1) FROM Sales.Agreement ag
     WHERE ag.TenantId = @TenantId AND ag.IsDeleted = 0
       AND ag.AgreementStatusCodeId = 1)                                            AS ActivePolicies,

    (SELECT COUNT(1) FROM Client.Account acc
     WHERE acc.TenantId = @TenantId AND acc.IsDeleted = 0)                         AS ActiveAccounts,

    (SELECT COUNT(1) FROM CRM.Lead l
     WHERE l.TenantId = @TenantId AND l.IsDeleted = 0
       AND l.StatusCodeId NOT IN (3,4,5))                                           AS OpenLeads,

    (SELECT COUNT(1) FROM CRM.Opportunity o
     WHERE o.TenantId = @TenantId AND o.IsDeleted = 0
       AND o.StatusCodeId = 1)                                                      AS OpenOpportunities,

    (SELECT COUNT(1) FROM Claims.Claim c
     WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
       AND c.Status NOT IN ('Closed','Denied'))                                      AS OpenClaims,

    (SELECT COUNT(1) FROM OPS.AgreementRenewal r WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND r.StatusCode NOT IN ('Renewed','Complete','Won','Lost','Cancelled')) AS PendingRenewals,

    ISNULL((SELECT SUM(iv.Amount - iv.AmountPaid)
            FROM Finance.ApInvoice iv
            WHERE iv.TenantId = @TenantId AND iv.IsDeleted = 0
              AND iv.StatusCode NOT IN ('Paid','Void','Cancelled')), 0)             AS OutstandingAr,

    ISNULL((SELECT COUNT(1) FROM Core.Alert a WHERE a.TenantId=@TenantId AND a.IsDeleted=0 AND a.StatusCode='Open'), 0) AS OpenAlerts

FROM Core.Tenant t
WHERE t.TenantId = @TenantId;

-- 6-month premium trend
SELECT
    YEAR(ag.CreatedDateUtc)  AS [Year],
    MONTH(ag.CreatedDateUtc) AS [Month],
    FORMAT(ag.CreatedDateUtc, 'MMM yy') AS [Label],
    SUM(ag.TotalContractValue) AS Amount,
    COUNT(1)                   AS [Count]
FROM Sales.Agreement ag
WHERE ag.TenantId = @TenantId AND ag.IsDeleted = 0
  AND ag.CreatedDateUtc >= DATEADD(MONTH, -6, @Som)
GROUP BY YEAR(ag.CreatedDateUtc), MONTH(ag.CreatedDateUtc), FORMAT(ag.CreatedDateUtc, 'MMM yy')
ORDER BY [Year], [Month];";

        using var cn = await _db.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, AnnualPremiumGoal }, cancellationToken: ct));

        var overview = await multi.ReadSingleOrDefaultAsync<AgencyExecutiveOverviewDto>()
                       ?? new AgencyExecutiveOverviewDto { AgencyName = "Agency" };
        overview.PremiumTrend = (await multi.ReadAsync<MonthlyTrendDto>()).ToList();
        return overview;
    }

    // ── Agency KPIs ──────────────────────────────────────────────────
    public async Task<AgencyKpiDto> GetKpisAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @Now  DATE = CAST(GETUTCDATE() AS DATE);
DECLARE @Som  DATE = DATEFROMPARTS(YEAR(@Now), MONTH(@Now), 1);
DECLARE @Soy  DATE = DATEFROMPARTS(YEAR(@Now), 1, 1);
DECLARE @SoyP DATE = DATEFROMPARTS(YEAR(@Now)-1, 1, 1);
DECLARE @EoyP DATE = DATEFROMPARTS(YEAR(@Now)-1, 12, 31);

SELECT
    ISNULL((SELECT SUM(TotalContractValue) FROM Sales.Agreement
            WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som), 0)  AS WrittenPremiumMtd,
    ISNULL((SELECT SUM(TotalContractValue) FROM Sales.Agreement
            WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Soy), 0)  AS WrittenPremiumYtd,
    ISNULL((SELECT SUM(TotalContractValue) FROM Sales.Agreement
            WHERE TenantId=@TenantId AND IsDeleted=0
              AND CreatedDateUtc>=@SoyP AND CreatedDateUtc<@Soy), 0)                AS WrittenPremiumPriorYtd,

    ISNULL(CAST((SELECT COUNT(1) FROM OPS.AgreementRenewal r WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND r.StatusCode IN ('Renewed','Complete','Won')) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM OPS.AgreementRenewal r2 WHERE r2.TenantId=@TenantId AND r2.IsDeleted=0),0) AS DECIMAL(5,2)), 0) AS RetentionRate,

    ISNULL(CAST((SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Soy AND AgreementStatusCodeId=1)*100.0/
        NULLIF((SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Soy),0) AS DECIMAL(5,2)),0) AS NewBusinessRate,

    (SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementStatusCodeId=1)  AS TotalActivePolicies,
    (SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som) AS NewPoliciesMtd,
    (SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementStatusCodeId=4 AND ModifiedDateUtc>=@Som) AS CancelledPoliciesMtd,
    (SELECT COUNT(DISTINCT AssignedToUserId) FROM CRM.Lead WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCodeId NOT IN (3,4,5)) AS ActiveProducers,

    ISNULL(CAST((SELECT SUM(TotalContractValue) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementStatusCodeId=1)/
        NULLIF((SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementStatusCodeId=1),0) AS DECIMAL(18,2)),0) AS AvgPremiumPerPolicy,

    (SELECT COUNT(1) FROM CRM.Lead WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som) AS LeadsThisMonth,

    ISNULL(CAST((SELECT COUNT(1) FROM CRM.Lead WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCodeId=3 AND CreatedDateUtc>=@Soy)*100.0/
        NULLIF((SELECT COUNT(1) FROM CRM.Lead WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Soy),0) AS DECIMAL(5,2)),0) AS LeadConversionRate,

    (SELECT COUNT(1) FROM CRM.Quote WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som) AS QuotesMtd,

    ISNULL(CAST((SELECT COUNT(1) FROM CRM.Quote WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode='Bound' AND CreatedDateUtc>=@Som)*100.0/
        NULLIF((SELECT COUNT(1) FROM CRM.Quote WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som),0) AS DECIMAL(5,2)),0) AS QuoteConversionRate;";

        using var cn = await _db.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleAsync<AgencyKpiDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
    }

    // ── Branch Performance ───────────────────────────────────────────
    public async Task<List<BranchPerformanceDto>> GetBranchPerformanceAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @Som DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);
DECLARE @Soy DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), 1, 1);

SELECT
    b.BranchId,
    b.BranchName,
    b.BranchCode,
    b.City,
    b.StateProvince,
    ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag
            WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND ag.BranchId=b.BranchId AND ag.CreatedDateUtc>=@Som),0) AS WrittenPremiumMtd,
    ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag
            WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND ag.BranchId=b.BranchId AND ag.CreatedDateUtc>=@Soy),0) AS WrittenPremiumYtd,
    (SELECT COUNT(1) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND ag.BranchId=b.BranchId AND ag.AgreementStatusCodeId=1) AS ActivePolicies,
    (SELECT COUNT(DISTINCT u.UserId) FROM IAM.[User] u WHERE u.TenantId=@TenantId AND u.IsDeleted=0 AND u.BranchId=b.BranchId AND u.IsActive=1) AS ActiveProducers,
    ISNULL(CAST((SELECT COUNT(1) FROM OPS.AgreementRenewal r INNER JOIN Sales.Agreement ag ON ag.AgreementId = r.AgreementId WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND ag.BranchId=b.BranchId AND r.StatusCode IN ('Renewed','Complete','Won')) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM OPS.AgreementRenewal r2 INNER JOIN Sales.Agreement ag2 ON ag2.AgreementId = r2.AgreementId WHERE r2.TenantId=@TenantId AND r2.IsDeleted=0 AND ag2.BranchId=b.BranchId),0) AS DECIMAL(5,2)), 0) AS RetentionRate,
    (SELECT COUNT(1) FROM CRM.Lead l INNER JOIN IAM.[User] u ON u.UserId = l.AssignedToUserId WHERE l.TenantId=@TenantId AND l.IsDeleted=0 AND u.BranchId=b.BranchId AND l.StatusCodeId NOT IN (3,4,5)) AS OpenLeads,
    (SELECT COUNT(1) FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND c.Status NOT IN ('Closed','Denied')) AS OpenClaims,
    ISNULL((SELECT SUM(iv.Amount - iv.AmountPaid) FROM Finance.ApInvoice iv INNER JOIN Sales.Agreement ag ON ag.AgreementId=iv.AgreementId WHERE iv.TenantId=@TenantId AND iv.IsDeleted=0 AND ag.BranchId=b.BranchId AND iv.StatusCode NOT IN ('Paid','Void','Cancelled')),0) AS OutstandingAr
FROM Core.Branch b
WHERE b.TenantId = @TenantId AND b.IsDeleted = 0
ORDER BY WrittenPremiumYtd DESC;";

        using var cn = await _db.CreateOpenConnectionAsync(ct);
        return (await cn.QueryAsync<BranchPerformanceDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct))).ToList();
    }

    // ── Producer Performance ─────────────────────────────────────────
    public async Task<List<ProducerPerformanceDto>> GetProducerPerformanceAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @Som DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);
DECLARE @Soy DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), 1, 1);

SELECT TOP 50
    u.UserId,
    ISNULL(u.DisplayName, u.Email) AS DisplayName,
    u.Email,
    b.BranchName,
    ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND ag.CreatedByUserId=u.UserId AND ag.CreatedDateUtc>=@Som),0) AS WrittenPremiumMtd,
    ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND ag.CreatedByUserId=u.UserId AND ag.CreatedDateUtc>=@Soy),0) AS WrittenPremiumYtd,
    (SELECT COUNT(1) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND ag.CreatedByUserId=u.UserId AND ag.CreatedDateUtc>=@Som) AS NewPoliciesMtd,
    (SELECT COUNT(1) FROM CRM.Lead l WHERE l.TenantId=@TenantId AND l.IsDeleted=0 AND l.AssignedToUserId=u.UserId AND l.StatusCodeId NOT IN (3,4,5)) AS OpenLeads,
    (SELECT COUNT(1) FROM CRM.Opportunity o WHERE o.TenantId=@TenantId AND o.IsDeleted=0 AND o.OwnerUserId=u.UserId AND o.StatusCodeId=1) AS OpenOpportunities,
    ISNULL(CAST((SELECT COUNT(1) FROM OPS.AgreementRenewal r INNER JOIN Sales.Agreement ag ON ag.AgreementId=r.AgreementId WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND ag.CreatedByUserId=u.UserId AND r.StatusCode IN ('Renewed','Complete','Won')) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM OPS.AgreementRenewal r2 INNER JOIN Sales.Agreement ag2 ON ag2.AgreementId=r2.AgreementId WHERE r2.TenantId=@TenantId AND r2.IsDeleted=0 AND ag2.CreatedByUserId=u.UserId),0) AS DECIMAL(5,2)),0) AS RetentionRate,
    (SELECT COUNT(1) FROM CRM.Quote q WHERE q.TenantId=@TenantId AND q.IsDeleted=0 AND q.CreatedByUserId=u.UserId AND q.CreatedDateUtc>=@Som) AS QuotesMtd,
    ISNULL(CAST((SELECT COUNT(1) FROM CRM.Quote q WHERE q.TenantId=@TenantId AND q.IsDeleted=0 AND q.CreatedByUserId=u.UserId AND q.StatusCode IN ('Bound','Accepted') AND q.CreatedDateUtc>=@Som) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM CRM.Quote q2 WHERE q2.TenantId=@TenantId AND q2.IsDeleted=0 AND q2.CreatedByUserId=u.UserId AND q2.CreatedDateUtc>=@Som),0) AS DECIMAL(5,2)),0) AS QuoteConversionRate
FROM IAM.[User] u
LEFT JOIN Core.Branch b ON b.BranchId = u.BranchId
WHERE u.TenantId = @TenantId AND u.IsDeleted = 0 AND u.IsActive = 1
ORDER BY WrittenPremiumYtd DESC;";

        using var cn = await _db.CreateOpenConnectionAsync(ct);
        return (await cn.QueryAsync<ProducerPerformanceDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct))).ToList();
    }

    // ── Renewal Pipeline ─────────────────────────────────────────────
    public async Task<RenewalPipelineDto> GetRenewalPipelineAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @Now DATE = CAST(GETUTCDATE() AS DATE);

SELECT
    r.RenewalId AS AgreementRenewalId,
    ag.AgreementNumber AS PolicyNumber,
    COALESCE(a.AccountName, 'Account') AS AccountName,
    COALESCE(u.DisplayName, u.FullName, u.Email) AS ProducerName,
    b.BranchName,
    CAST(r.NewStartDate AS DATETIME2) AS RenewalDate,
    COALESCE(r.TotalContractValue, ag.TotalContractValue, 0) AS CurrentPremium,
    r.StatusCode,
    DATEDIFF(day, @Now, r.NewStartDate) AS DaysUntilRenewal
FROM OPS.AgreementRenewal r
INNER JOIN Sales.Agreement ag ON ag.AgreementId = r.AgreementId
LEFT JOIN Client.Account a ON a.AccountId = ag.AccountId
LEFT JOIN IAM.[User] u ON u.UserId = ag.CreatedByUserId
LEFT JOIN Core.Branch b ON b.BranchId = ag.BranchId
WHERE r.TenantId=@TenantId AND r.IsDeleted=0
  AND r.StatusCode NOT IN ('Renewed','Complete','Won','Lost','Cancelled')
  AND r.NewStartDate <= DATEADD(day, 90, @Now)
ORDER BY r.NewStartDate;";

        using var cn = await _db.CreateOpenConnectionAsync(ct);
        var rows = (await cn.QueryAsync<RenewalPipelineRowDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct))).ToList();
        return new RenewalPipelineDto
        {
            Rows = rows,
            Overdue = rows.Count(r => r.DaysUntilRenewal < 0),
            PremiumOverdue = rows.Where(r => r.DaysUntilRenewal < 0).Sum(r => r.CurrentPremium),
            DueIn30Days = rows.Count(r => r.DaysUntilRenewal >= 0 && r.DaysUntilRenewal <= 30),
            PremiumDueIn30Days = rows.Where(r => r.DaysUntilRenewal >= 0 && r.DaysUntilRenewal <= 30).Sum(r => r.CurrentPremium),
            DueIn60Days = rows.Count(r => r.DaysUntilRenewal >= 31 && r.DaysUntilRenewal <= 60),
            PremiumDueIn60Days = rows.Where(r => r.DaysUntilRenewal >= 31 && r.DaysUntilRenewal <= 60).Sum(r => r.CurrentPremium),
            DueIn90Days = rows.Count(r => r.DaysUntilRenewal >= 61 && r.DaysUntilRenewal <= 90),
            PremiumDueIn90Days = rows.Where(r => r.DaysUntilRenewal >= 61 && r.DaysUntilRenewal <= 90).Sum(r => r.CurrentPremium),
        };
    }

    // ── Claims Summary ───────────────────────────────────────────────
    public async Task<ClaimsSummaryDto> GetClaimsSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @Som DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);

SELECT
    (SELECT COUNT(1) FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0 AND Status NOT IN ('Closed','Denied')) AS TotalOpenClaims,
    (SELECT COUNT(1) FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0 AND OpenedDateUtc>=@Som) AS NewClaimsMtd,
    (SELECT COUNT(1) FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0 AND Status='Closed') AS ClosedClaimsMtd,
    ISNULL((SELECT SUM(ReserveAmount) FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0 AND Status NOT IN ('Closed','Denied')),0) AS TotalReservedAmount,
    ISNULL((SELECT SUM(PaidAmount) FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0),0) AS TotalPaidMtd,
    (SELECT COUNT(1) FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0 AND Status NOT IN ('Closed','Denied') AND ReserveAmount >= 75000) AS LitigatedClaims,
    ISNULL(CAST((SELECT AVG(DATEDIFF(day, OpenedDateUtc, COALESCE(CreatedDateUtc, GETUTCDATE()))) FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0 AND Status='Closed') AS FLOAT), 0) AS AvgDaysToClose
FROM (VALUES(1)) AS _x(n);

SELECT Status AS StatusCode, COUNT(1) AS [Count], ISNULL(SUM(ReserveAmount),0) AS Reserved
FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0 AND Status NOT IN ('Closed','Denied')
GROUP BY Status;

SELECT LineOfBusiness AS LobName, COUNT(1) AS [Count], ISNULL(SUM(ReserveAmount),0) AS Reserved
FROM Claims.Claim
WHERE TenantId=@TenantId AND IsDeleted=0 AND Status NOT IN ('Closed','Denied')
GROUP BY LineOfBusiness;";

        using var cn = await _db.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, AnnualPremiumGoal }, cancellationToken: ct));
        var summary  = await multi.ReadSingleOrDefaultAsync<ClaimsSummaryDto>() ?? new ClaimsSummaryDto();
        summary.ByStatus = (await multi.ReadAsync<ClaimsByStatusDto>()).ToList();
        summary.ByLob    = (await multi.ReadAsync<ClaimsByLobDto>()).ToList();
        return summary;
    }

    // ── Billing Summary ──────────────────────────────────────────────
    public async Task<BillingSummaryDto> GetBillingSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @Now DATE = CAST(GETUTCDATE() AS DATE);
DECLARE @Som DATE = DATEFROMPARTS(YEAR(@Now), MONTH(@Now), 1);

SELECT
    ISNULL((SELECT SUM(Amount - AmountPaid) FROM Finance.ApInvoice WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode NOT IN ('Paid','Void','Cancelled')),0) AS OutstandingArTotal,
    ISNULL((SELECT SUM(AmountPaid) FROM Finance.ApInvoice WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode='Paid' AND DueDate>=@Som),0)                    AS CollectedMtd,
    ISNULL((SELECT SUM(Amount - AmountPaid) FROM Finance.ApInvoice WHERE TenantId=@TenantId AND IsDeleted=0 AND DueDate<@Now AND StatusCode NOT IN ('Paid','Void','Cancelled')),0) AS OverdueBalance,
    (SELECT COUNT(1) FROM Finance.ApInvoice WHERE TenantId=@TenantId AND IsDeleted=0 AND DueDate<@Now AND StatusCode NOT IN ('Paid','Void','Cancelled'))          AS OverdueInvoiceCount,
    (SELECT COUNT(1) FROM Finance.ApInvoice WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode NOT IN ('Paid','Void','Cancelled'))                          AS TotalOpenInvoices,
    ISNULL((SELECT SUM(CommissionAmount) FROM Commission.CommissionTransaction WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode='Pending'),0) AS PendingCommissions,
    ISNULL((SELECT SUM(CommissionAmount) FROM Commission.CommissionTransaction WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode='Paid'),0) AS PaidCommissionsMtd
FROM (VALUES(1)) AS _x(n);

SELECT
    CASE
        WHEN DueDate >= @Now                               THEN 'Current'
        WHEN DATEDIFF(DAY,DueDate,@Now) BETWEEN 1  AND 30 THEN '1-30 Days'
        WHEN DATEDIFF(DAY,DueDate,@Now) BETWEEN 31 AND 60 THEN '31-60 Days'
        WHEN DATEDIFF(DAY,DueDate,@Now) BETWEEN 61 AND 90 THEN '61-90 Days'
        ELSE '90+ Days'
    END AS BucketLabel,
    SUM(Amount - AmountPaid) AS Amount,
    COUNT(1) AS InvoiceCount
FROM Finance.ApInvoice
WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode NOT IN ('Paid','Void','Cancelled')
GROUP BY
    CASE
        WHEN DueDate >= @Now                               THEN 'Current'
        WHEN DATEDIFF(DAY,DueDate,@Now) BETWEEN 1  AND 30 THEN '1-30 Days'
        WHEN DATEDIFF(DAY,DueDate,@Now) BETWEEN 31 AND 60 THEN '31-60 Days'
        WHEN DATEDIFF(DAY,DueDate,@Now) BETWEEN 61 AND 90 THEN '61-90 Days'
        ELSE '90+ Days'
    END
ORDER BY MIN(DueDate);";

        using var cn = await _db.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        var summary  = await multi.ReadSingleOrDefaultAsync<BillingSummaryDto>() ?? new BillingSummaryDto();
        summary.ArAging = (await multi.ReadAsync<ArAgingBucketDto>()).ToList();
        return summary;
    }
}
