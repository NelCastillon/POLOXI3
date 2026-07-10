using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Domain.Enums;
using Dapper;
using System.Data;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AgencyDashboardRepository : IAgencyDashboardRepository
{
    private readonly ISqlConnectionFactory _db;
    public AgencyDashboardRepository(ISqlConnectionFactory db) => _db = db;

    // ── Executive Overview ───────────────────────────────────────────
    public async Task<AgencyExecutiveOverviewDto> GetExecutiveOverviewAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _db.CreateOpenConnectionAsync(ct);
        var invoiceColumns = await GetApInvoiceAmountColumnsAsync(cn, ct);
        var invoiceOutstanding = invoiceColumns.Outstanding("iv");

        var sql = @$"
DECLARE @Now  DATE = CAST(GETUTCDATE() AS DATE);
DECLARE @Som  DATE = DATEFROMPARTS(YEAR(@Now), MONTH(@Now), 1);
DECLARE @Soy  DATE = DATEFROMPARTS(YEAR(@Now), 1, 1);
DECLARE @PriorYearStart DATE = DATEFROMPARTS(YEAR(@Now) - 1, 1, 1);
DECLARE @PriorYearEnd DATE = DATEFROMPARTS(YEAR(@Now), 1, 1);
DECLARE @WrittenPremiumYtd DECIMAL(18,2) = ISNULL((SELECT SUM(ag.TotalContractValue)
    FROM Sales.Agreement ag
    WHERE ag.TenantId = @TenantId AND ag.IsDeleted = 0 AND ag.CreatedDateUtc >= @Soy), 0);
DECLARE @PriorYearPremium DECIMAL(18,2) = ISNULL((SELECT SUM(ag.TotalContractValue)
    FROM Sales.Agreement ag
    WHERE ag.TenantId = @TenantId AND ag.IsDeleted = 0 AND ag.CreatedDateUtc >= @PriorYearStart AND ag.CreatedDateUtc < @PriorYearEnd), 0);
DECLARE @AnnualizedPremium DECIMAL(18,2) = CASE WHEN MONTH(@Now) > 0 THEN (@WrittenPremiumYtd / MONTH(@Now)) * 12 ELSE @WrittenPremiumYtd END;
DECLARE @WrittenPremiumGoal DECIMAL(18,2) = (SELECT MAX(v) FROM (VALUES (@WrittenPremiumYtd), (@PriorYearPremium), (@AnnualizedPremium)) AS goal(v));

SELECT
    t.TenantName                                                                        AS AgencyName,
    t.TenantCode                                                                        AS AgencyCode,

    -- Written premium
    ISNULL((SELECT SUM(ag.TotalContractValue)
            FROM Sales.Agreement ag
            WHERE ag.TenantId = @TenantId AND ag.IsDeleted = 0
              AND ag.CreatedDateUtc >= @Som), 0)                                   AS WrittenPremiumMtd,

    @WrittenPremiumYtd                                                              AS WrittenPremiumYtd,

    @WrittenPremiumGoal                                                            AS WrittenPremiumGoal,

    ISNULL(CAST((SELECT COUNT(1) FROM OPS.AgreementRenewal r WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND r.StatusCode IN ('Renewed','Complete','Won')) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM OPS.AgreementRenewal r2 WHERE r2.TenantId=@TenantId AND r2.IsDeleted=0),0) AS DECIMAL(5,2)), 0) AS RetentionRate,

    -- Lead conversion
    ISNULL(CAST(
        (SELECT COUNT(1) FROM CRM.Lead l
         WHERE l.TenantId = @TenantId AND l.IsDeleted = 0
           AND l.StatusCodeId = @ConvertedLeadStatusId
           AND l.CreatedDateUtc >= @Soy) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM CRM.Lead l2
         WHERE l2.TenantId = @TenantId AND l2.IsDeleted = 0
           AND l2.CreatedDateUtc >= @Soy), 0)
    AS DECIMAL(5,2)), 0)                                                            AS ConversionRate,

    (SELECT COUNT(1) FROM Sales.Agreement ag
     WHERE ag.TenantId = @TenantId AND ag.IsDeleted = 0
       AND ag.AgreementStatusCodeId = @ActiveAgreementStatusId)                     AS ActivePolicies,

    (SELECT COUNT(1) FROM Client.Account acc
     WHERE acc.TenantId = @TenantId AND acc.IsDeleted = 0)                         AS ActiveAccounts,

    (SELECT COUNT(1) FROM CRM.Lead l
     WHERE l.TenantId = @TenantId AND l.IsDeleted = 0
       AND l.StatusCodeId NOT IN (@ConvertedLeadStatusId,@DisqualifiedLeadStatusId)) AS OpenLeads,

    (SELECT COUNT(1) FROM CRM.Opportunity o
     WHERE o.TenantId = @TenantId AND o.IsDeleted = 0
        AND o.StatusCodeId = @OpenOpportunityStatusId)                               AS OpenOpportunities,

    (SELECT COUNT(1) FROM Claims.Claim c
     WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
       AND c.Status NOT IN ('Closed','Denied'))                                      AS OpenClaims,

    (SELECT COUNT(1) FROM OPS.AgreementRenewal r WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND r.StatusCode NOT IN ('Renewed','Complete','Won','Lost','Cancelled')) AS PendingRenewals,

    ISNULL((SELECT SUM({invoiceOutstanding})
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

        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, StatusParameters(tenantId), cancellationToken: ct));

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
DECLARE @RoleProducerCount INT = ISNULL((
    SELECT COUNT(DISTINCT u.UserId)
    FROM IAM.[User] u
    INNER JOIN IAM.UserRole ur ON ur.UserId = u.UserId AND ur.IsDeleted = 0 AND ur.IsActive = 1
    INNER JOIN IAM.Role r ON r.RoleId = ur.RoleId AND r.IsDeleted = 0 AND r.IsActive = 1
    WHERE u.TenantId = @TenantId
      AND u.IsDeleted = 0
      AND u.IsActive = 1
      AND (r.RoleCode LIKE '%PRODUCER%' OR r.RoleName LIKE '%Producer%')
), 0);
DECLARE @ActivityProducerCount INT = ISNULL((
    SELECT COUNT(DISTINCT ProducerUserId)
    FROM (
        SELECT CreatedByUserId AS ProducerUserId FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedByUserId IS NOT NULL
        UNION SELECT AssignedToUserId FROM CRM.Lead WHERE TenantId=@TenantId AND IsDeleted=0 AND AssignedToUserId IS NOT NULL
        UNION SELECT OwnerUserId FROM CRM.Opportunity WHERE TenantId=@TenantId AND IsDeleted=0 AND OwnerUserId IS NOT NULL
        UNION SELECT CreatedByUserId FROM CRM.Quote WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedByUserId IS NOT NULL
    ) p
), 0);

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

    ISNULL(CAST((SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Soy AND AgreementStatusCodeId<>@CancelledAgreementStatusId AND (EffectiveEndDate IS NULL OR EffectiveEndDate >= @Now))*100.0/
        NULLIF((SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Soy),0) AS DECIMAL(5,2)),0) AS NewBusinessRate,

    (SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementStatusCodeId<>@CancelledAgreementStatusId AND (EffectiveEndDate IS NULL OR EffectiveEndDate >= @Now))  AS TotalActivePolicies,
    (SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som) AS NewPoliciesMtd,
    (SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementStatusCodeId=@CancelledAgreementStatusId AND ModifiedDateUtc>=@Som) AS CancelledPoliciesMtd,
    CASE WHEN @RoleProducerCount > 0 THEN @RoleProducerCount ELSE @ActivityProducerCount END AS ActiveProducers,

    ISNULL(CAST((SELECT SUM(TotalContractValue) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementStatusCodeId<>@CancelledAgreementStatusId AND (EffectiveEndDate IS NULL OR EffectiveEndDate >= @Now))/
        NULLIF((SELECT COUNT(1) FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementStatusCodeId<>@CancelledAgreementStatusId AND (EffectiveEndDate IS NULL OR EffectiveEndDate >= @Now)),0) AS DECIMAL(18,2)),0) AS AvgPremiumPerPolicy,

    (SELECT COUNT(1) FROM CRM.Lead WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som) AS LeadsThisMonth,

    ISNULL(CAST((SELECT COUNT(1) FROM CRM.Lead WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCodeId=@ConvertedLeadStatusId AND CreatedDateUtc>=@Soy)*100.0/
        NULLIF((SELECT COUNT(1) FROM CRM.Lead WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Soy),0) AS DECIMAL(5,2)),0) AS LeadConversionRate,

    (SELECT COUNT(1) FROM CRM.Quote WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som) AS QuotesMtd,

    ISNULL(CAST((SELECT COUNT(1) FROM CRM.Quote WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode IN ('Bound','Accepted') AND CreatedDateUtc>=@Som)*100.0/
        NULLIF((SELECT COUNT(1) FROM CRM.Quote WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Som),0) AS DECIMAL(5,2)),0) AS QuoteConversionRate;";

        using var cn = await _db.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleAsync<AgencyKpiDto>(new CommandDefinition(sql, StatusParameters(tenantId), cancellationToken: ct));
    }

    // ── Branch Performance ───────────────────────────────────────────
    public async Task<List<BranchPerformanceDto>> GetBranchPerformanceAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _db.CreateOpenConnectionAsync(ct);
        var invoiceColumns = await GetApInvoiceAmountColumnsAsync(cn, ct);
        var invoiceOutstanding = invoiceColumns.Outstanding("iv");
        var branchStateProvince = await GetBranchStateProvinceExpressionAsync(cn, ct);
        var agreementBranchId = await GetAgreementBranchIdExpressionAsync(cn, "ag", ct);
        var agreementBranchId2 = await GetAgreementBranchIdExpressionAsync(cn, "ag2", ct);

        var sql = @$"
DECLARE @Som DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);
DECLARE @Soy DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), 1, 1);
DECLARE @Now DATE = CAST(GETUTCDATE() AS DATE);
DECLARE @TenantWrittenPremiumYtd DECIMAL(18,2) = ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND ag.CreatedDateUtc>=@Soy), 0);
DECLARE @TenantOutstandingAr DECIMAL(18,2) = ISNULL((SELECT SUM({invoiceOutstanding}) FROM Finance.ApInvoice iv WHERE iv.TenantId=@TenantId AND iv.IsDeleted=0 AND iv.StatusCode NOT IN ('Paid','Void','Cancelled')), 0);
DECLARE @HasProducerRoles BIT = CASE WHEN EXISTS (
    SELECT 1
    FROM IAM.[User] u
    INNER JOIN IAM.UserRole ur ON ur.UserId = u.UserId AND ur.IsDeleted = 0 AND ur.IsActive = 1
    INNER JOIN IAM.Role r ON r.RoleId = ur.RoleId AND r.IsDeleted = 0 AND r.IsActive = 1
    WHERE u.TenantId = @TenantId
      AND u.IsDeleted = 0
      AND u.IsActive = 1
      AND (r.RoleCode LIKE '%PRODUCER%' OR r.RoleName LIKE '%Producer%')
) THEN 1 ELSE 0 END;

SELECT
    b.BranchId,
    b.BranchName,
    b.BranchCode,
    b.City,
    {branchStateProvince} AS StateProvince,
    ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag
            WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementBranchId}=b.BranchId AND ag.CreatedDateUtc>=@Som),0) AS WrittenPremiumMtd,
    ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag
            WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementBranchId}=b.BranchId AND ag.CreatedDateUtc>=@Soy),0) AS WrittenPremiumYtd,
    (SELECT COUNT(1) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementBranchId}=b.BranchId AND ag.AgreementStatusCodeId<>@CancelledAgreementStatusId AND (ag.EffectiveEndDate IS NULL OR ag.EffectiveEndDate >= @Now)) AS ActivePolicies,
    CASE WHEN @HasProducerRoles = 1 THEN
        (SELECT COUNT(DISTINCT u.UserId) FROM IAM.[User] u INNER JOIN IAM.UserRole ur ON ur.UserId=u.UserId AND ur.IsDeleted=0 AND ur.IsActive=1 INNER JOIN IAM.Role r ON r.RoleId=ur.RoleId AND r.IsDeleted=0 AND r.IsActive=1 WHERE u.TenantId=@TenantId AND u.IsDeleted=0 AND u.BranchId=b.BranchId AND u.IsActive=1 AND (r.RoleCode LIKE '%PRODUCER%' OR r.RoleName LIKE '%Producer%'))
     ELSE
        (SELECT COUNT(DISTINCT p.UserId) FROM (
            SELECT ag.CreatedByUserId AS UserId FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementBranchId}=b.BranchId AND ag.CreatedByUserId IS NOT NULL
            UNION SELECT l.AssignedToUserId FROM CRM.Lead l INNER JOIN IAM.[User] lu ON lu.UserId=l.AssignedToUserId WHERE l.TenantId=@TenantId AND l.IsDeleted=0 AND lu.BranchId=b.BranchId AND l.AssignedToUserId IS NOT NULL
            UNION SELECT o.OwnerUserId FROM CRM.Opportunity o INNER JOIN IAM.[User] ou ON ou.UserId=o.OwnerUserId WHERE o.TenantId=@TenantId AND o.IsDeleted=0 AND ou.BranchId=b.BranchId AND o.OwnerUserId IS NOT NULL
        ) p)
     END AS ActiveProducers,
    ISNULL(CAST((SELECT COUNT(1) FROM OPS.AgreementRenewal r INNER JOIN Sales.Agreement ag ON ag.AgreementId = r.AgreementId WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND {agreementBranchId}=b.BranchId AND r.StatusCode IN ('Renewed','Complete','Won')) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM OPS.AgreementRenewal r2 INNER JOIN Sales.Agreement ag2 ON ag2.AgreementId = r2.AgreementId WHERE r2.TenantId=@TenantId AND r2.IsDeleted=0 AND {agreementBranchId2}=b.BranchId),0) AS DECIMAL(5,2)), 0) AS RetentionRate,
    (SELECT COUNT(1) FROM CRM.Lead l INNER JOIN IAM.[User] u ON u.UserId = l.AssignedToUserId WHERE l.TenantId=@TenantId AND l.IsDeleted=0 AND u.BranchId=b.BranchId AND l.StatusCodeId NOT IN (@ConvertedLeadStatusId,@DisqualifiedLeadStatusId)) AS OpenLeads,
    (SELECT COUNT(1) FROM Claims.Claim c INNER JOIN Sales.Agreement ag ON ag.TenantId=c.TenantId AND ag.AgreementNumber=c.PolicyNumber WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND c.Status NOT IN ('Closed','Denied') AND {agreementBranchId}=b.BranchId) AS OpenClaims,
    ISNULL(@TenantOutstandingAr * (ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementBranchId}=b.BranchId AND ag.CreatedDateUtc>=@Soy),0) / NULLIF(@TenantWrittenPremiumYtd,0)),0) AS OutstandingAr
FROM Core.Branch b
WHERE b.TenantId = @TenantId AND b.IsDeleted = 0
ORDER BY WrittenPremiumYtd DESC;";

        return (await cn.QueryAsync<BranchPerformanceDto>(new CommandDefinition(sql, StatusParameters(tenantId), cancellationToken: ct))).ToList();
    }

    // ── Producer Performance ─────────────────────────────────────────
    public async Task<List<ProducerPerformanceDto>> GetProducerPerformanceAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _db.CreateOpenConnectionAsync(ct);
        var userDisplayName = await GetUserDisplayNameExpressionAsync(cn, ct);
        var userActivePredicate = await GetUserActivePredicateAsync(cn, ct);
        var agreementProducerUserId = await GetProducerUserIdExpressionAsync(cn, "Sales.Agreement", "ag", new[] { "ProducerUserId", "ProducerId", "CreatedByUserId" }, ct);
        var agreementProducerUserId2 = await GetProducerUserIdExpressionAsync(cn, "Sales.Agreement", "ag2", new[] { "ProducerUserId", "ProducerId", "CreatedByUserId" }, ct);
        var leadProducerUserId = await GetProducerUserIdExpressionAsync(cn, "CRM.Lead", "l", new[] { "AssignedToUserId", "OwnerUserId", "ProducerUserId", "CreatedByUserId" }, ct);
        var opportunityProducerUserId = await GetProducerUserIdExpressionAsync(cn, "CRM.Opportunity", "o", new[] { "OwnerUserId", "AssignedToUserId", "ProducerUserId", "CreatedByUserId" }, ct);
        var quoteProducerUserId = await GetProducerUserIdExpressionAsync(cn, "CRM.Quote", "q", new[] { "ProducerUserId", "OwnerUserId", "CreatedByUserId" }, ct);
        var quoteProducerUserId2 = await GetProducerUserIdExpressionAsync(cn, "CRM.Quote", "q2", new[] { "ProducerUserId", "OwnerUserId", "CreatedByUserId" }, ct);

        var sql = @$"
DECLARE @Som DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);
DECLARE @Soy DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), 1, 1);
DECLARE @HasProducerRoles BIT = CASE WHEN EXISTS (
    SELECT 1
    FROM IAM.[User] u
    INNER JOIN IAM.UserRole ur ON ur.UserId = u.UserId AND ur.IsDeleted = 0 AND ur.IsActive = 1
    INNER JOIN IAM.Role r ON r.RoleId = ur.RoleId AND r.IsDeleted = 0 AND r.IsActive = 1
    WHERE u.TenantId = @TenantId
      AND u.IsDeleted = 0
      AND {userActivePredicate}
      AND (r.RoleCode LIKE '%PRODUCER%' OR r.RoleName LIKE '%Producer%')
) THEN 1 ELSE 0 END;

WITH ProducerUsers AS (
    SELECT DISTINCT u.UserId
    FROM IAM.[User] u
    INNER JOIN IAM.UserRole ur ON ur.UserId = u.UserId AND ur.IsDeleted = 0 AND ur.IsActive = 1
    INNER JOIN IAM.Role r ON r.RoleId = ur.RoleId AND r.IsDeleted = 0 AND r.IsActive = 1
    WHERE @HasProducerRoles = 1
      AND u.TenantId = @TenantId
      AND u.IsDeleted = 0
      AND {userActivePredicate}
      AND (r.RoleCode LIKE '%PRODUCER%' OR r.RoleName LIKE '%Producer%')

    UNION

    SELECT DISTINCT ProducerUserId
    FROM (
        SELECT {agreementProducerUserId} AS ProducerUserId FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementProducerUserId} IS NOT NULL
        UNION SELECT {leadProducerUserId} FROM CRM.Lead l WHERE l.TenantId=@TenantId AND l.IsDeleted=0 AND {leadProducerUserId} IS NOT NULL
        UNION SELECT {opportunityProducerUserId} FROM CRM.Opportunity o WHERE o.TenantId=@TenantId AND o.IsDeleted=0 AND {opportunityProducerUserId} IS NOT NULL
        UNION SELECT {quoteProducerUserId} FROM CRM.Quote q WHERE q.TenantId=@TenantId AND q.IsDeleted=0 AND {quoteProducerUserId} IS NOT NULL
    ) activity
    WHERE @HasProducerRoles = 0
)

SELECT
    u.UserId,
    {userDisplayName} AS DisplayName,
    u.Email,
    b.BranchName,
    ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementProducerUserId}=u.UserId AND ag.CreatedDateUtc>=@Som),0) AS WrittenPremiumMtd,
    ISNULL((SELECT SUM(ag.TotalContractValue) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementProducerUserId}=u.UserId AND ag.CreatedDateUtc>=@Soy),0) AS WrittenPremiumYtd,
    (SELECT COUNT(1) FROM Sales.Agreement ag WHERE ag.TenantId=@TenantId AND ag.IsDeleted=0 AND {agreementProducerUserId}=u.UserId AND ag.CreatedDateUtc>=@Som) AS NewPoliciesMtd,
    (SELECT COUNT(1) FROM CRM.Lead l WHERE l.TenantId=@TenantId AND l.IsDeleted=0 AND {leadProducerUserId}=u.UserId AND l.StatusCodeId NOT IN (@ConvertedLeadStatusId,@DisqualifiedLeadStatusId)) AS OpenLeads,
    (SELECT COUNT(1) FROM CRM.Opportunity o WHERE o.TenantId=@TenantId AND o.IsDeleted=0 AND {opportunityProducerUserId}=u.UserId AND o.StatusCodeId=@OpenOpportunityStatusId) AS OpenOpportunities,
    ISNULL(CAST((SELECT COUNT(1) FROM OPS.AgreementRenewal r INNER JOIN Sales.Agreement ag ON ag.AgreementId=r.AgreementId WHERE r.TenantId=@TenantId AND r.IsDeleted=0 AND {agreementProducerUserId}=u.UserId AND r.StatusCode IN ('Renewed','Complete','Won')) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM OPS.AgreementRenewal r2 INNER JOIN Sales.Agreement ag2 ON ag2.AgreementId=r2.AgreementId WHERE r2.TenantId=@TenantId AND r2.IsDeleted=0 AND {agreementProducerUserId2}=u.UserId),0) AS DECIMAL(5,2)),0) AS RetentionRate,
    (SELECT COUNT(1) FROM CRM.Quote q WHERE q.TenantId=@TenantId AND q.IsDeleted=0 AND {quoteProducerUserId}=u.UserId AND q.CreatedDateUtc>=@Som) AS QuotesMtd,
    ISNULL(CAST((SELECT COUNT(1) FROM CRM.Quote q WHERE q.TenantId=@TenantId AND q.IsDeleted=0 AND {quoteProducerUserId}=u.UserId AND q.StatusCode IN ('Bound','Accepted') AND q.CreatedDateUtc>=@Som) * 100.0 /
        NULLIF((SELECT COUNT(1) FROM CRM.Quote q2 WHERE q2.TenantId=@TenantId AND q2.IsDeleted=0 AND {quoteProducerUserId2}=u.UserId AND q2.CreatedDateUtc>=@Som),0) AS DECIMAL(5,2)),0) AS QuoteConversionRate
FROM ProducerUsers pu
INNER JOIN IAM.[User] u ON u.UserId = pu.UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0 AND {userActivePredicate}
LEFT JOIN Core.Branch b ON b.BranchId = u.BranchId AND b.TenantId = u.TenantId AND b.IsDeleted = 0
ORDER BY WrittenPremiumYtd DESC, WrittenPremiumMtd DESC, DisplayName;";

        return (await cn.QueryAsync<ProducerPerformanceDto>(new CommandDefinition(sql, StatusParameters(tenantId), cancellationToken: ct))).ToList();
    }

    // ── Renewal Pipeline ─────────────────────────────────────────────
    public async Task<RenewalPipelineDto> GetRenewalPipelineAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _db.CreateOpenConnectionAsync(ct);
        var userDisplayName = await GetUserDisplayNameExpressionAsync(cn, ct);
        var agreementBranchId = await GetAgreementBranchIdExpressionAsync(cn, "ag", ct);
        var agreementProducerUserId = await GetProducerUserIdExpressionAsync(cn, "Sales.Agreement", "ag", new[] { "ProducerUserId", "ProducerId", "CreatedByUserId" }, ct);
        var renewalDate = await GetColumnExpressionAsync(cn, "OPS.AgreementRenewal", "r", new[] { "NewStartDate", "RenewalDate", "EffectiveStartDate" }, "CAST(r.[CreatedDateUtc] AS DATE)", ct);
        var renewalPremium = await GetDecimalColumnExpressionAsync(cn, "OPS.AgreementRenewal", "r", new[] { "TotalContractValue", "CurrentPremium", "PremiumAmount", "RenewalPremium" }, ct);
        var agreementPremium = await GetDecimalColumnExpressionAsync(cn, "Sales.Agreement", "ag", new[] { "TotalContractValue", "PremiumAmount", "AnnualPremium" }, ct);

        var sql = @$"
DECLARE @Now DATE = CAST(GETUTCDATE() AS DATE);

SELECT
    r.RenewalId AS AgreementRenewalId,
    ag.AgreementNumber AS PolicyNumber,
    COALESCE(a.AccountName, 'Account') AS AccountName,
    {userDisplayName} AS ProducerName,
    b.BranchName,
    CAST({renewalDate} AS DATETIME2) AS RenewalDate,
    COALESCE({renewalPremium}, {agreementPremium}, 0) AS CurrentPremium,
    r.StatusCode,
    DATEDIFF(day, @Now, {renewalDate}) AS DaysUntilRenewal
FROM OPS.AgreementRenewal r
INNER JOIN Sales.Agreement ag ON ag.AgreementId = r.AgreementId
LEFT JOIN Client.Account a ON a.AccountId = ag.AccountId
LEFT JOIN IAM.[User] u ON u.UserId = {agreementProducerUserId} AND u.TenantId = ag.TenantId AND u.IsDeleted = 0
LEFT JOIN Core.Branch b ON b.BranchId = {agreementBranchId} AND b.TenantId = ag.TenantId AND b.IsDeleted = 0
WHERE r.TenantId=@TenantId AND r.IsDeleted=0
  AND r.StatusCode NOT IN ('Renewed','Complete','Won','Lost','Cancelled')
  AND {renewalDate} <= DATEADD(day, 90, @Now)
ORDER BY {renewalDate};";

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
        using var cn = await _db.CreateOpenConnectionAsync(ct);
        var claimStatus = await GetColumnExpressionAsync(cn, "Claims.Claim", "c", new[] { "Status", "StatusCode" }, "N'Open'", ct);
        var reportedDate = await GetColumnExpressionAsync(cn, "Claims.Claim", "c", new[] { "DateReported", "OpenedDateUtc", "CreatedDateUtc" }, "c.[CreatedDateUtc]", ct);
        var closedDate = await GetColumnExpressionAsync(cn, "Claims.Claim", "c", new[] { "ClosedDate", "ClosedDateUtc", "ModifiedDateUtc" }, "NULL", ct);
        var reserveAmount = await GetDecimalColumnExpressionAsync(cn, "Claims.Claim", "c", new[] { "TotalReserves", "ReserveAmount", "ReservedAmount" }, ct);
        var paidAmount = await GetDecimalColumnExpressionAsync(cn, "Claims.Claim", "c", new[] { "TotalPaid", "PaidAmount" }, ct);
        var litigationPredicate = await GetBitPredicateExpressionAsync(cn, "Claims.Claim", "c", new[] { "IsLitigation", "IsLitigated" }, ct);
        var lob = await GetColumnExpressionAsync(cn, "Claims.Claim", "c", new[] { "Lob", "LineOfBusiness", "LobName" }, "N'Unassigned'", ct);

        var sql = @$"
DECLARE @Som DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);

SELECT
    (SELECT COUNT(1) FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND {claimStatus} NOT IN ('Closed','Denied')) AS TotalOpenClaims,
    (SELECT COUNT(1) FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND CAST({reportedDate} AS DATE)>=@Som) AS NewClaimsMtd,
    (SELECT COUNT(1) FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND {claimStatus}='Closed' AND {closedDate} IS NOT NULL AND CAST({closedDate} AS DATE)>=@Som) AS ClosedClaimsMtd,
    ISNULL((SELECT SUM({reserveAmount}) FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND {claimStatus} NOT IN ('Closed','Denied')),0) AS TotalReservedAmount,
    ISNULL((SELECT SUM({paidAmount}) FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND CAST({reportedDate} AS DATE)>=@Som),0) AS TotalPaidMtd,
    (SELECT COUNT(1) FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND {claimStatus} NOT IN ('Closed','Denied') AND {litigationPredicate}) AS LitigatedClaims,
    ISNULL(CAST((SELECT AVG(DATEDIFF(day, CAST({reportedDate} AS DATE), COALESCE(CAST({closedDate} AS DATE), CAST(GETUTCDATE() AS DATE)))) FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND {claimStatus}='Closed') AS FLOAT), 0) AS AvgDaysToClose
FROM (VALUES(1)) AS _x(n);

SELECT CAST({claimStatus} AS NVARCHAR(100)) AS StatusCode, COUNT(1) AS [Count], ISNULL(SUM({reserveAmount}),0) AS Reserved
FROM Claims.Claim c WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND {claimStatus} NOT IN ('Closed','Denied')
GROUP BY CAST({claimStatus} AS NVARCHAR(100));

SELECT CAST({lob} AS NVARCHAR(100)) AS LobName, COUNT(1) AS [Count], ISNULL(SUM({reserveAmount}),0) AS Reserved
FROM Claims.Claim c
WHERE c.TenantId=@TenantId AND c.IsDeleted=0 AND {claimStatus} NOT IN ('Closed','Denied')
GROUP BY CAST({lob} AS NVARCHAR(100));";

        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        var summary  = await multi.ReadSingleOrDefaultAsync<ClaimsSummaryDto>() ?? new ClaimsSummaryDto();
        summary.ByStatus = (await multi.ReadAsync<ClaimsByStatusDto>()).ToList();
        summary.ByLob    = (await multi.ReadAsync<ClaimsByLobDto>()).ToList();
        return summary;
    }

    // ── Billing Summary ──────────────────────────────────────────────
    public async Task<BillingSummaryDto> GetBillingSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _db.CreateOpenConnectionAsync(ct);
        var invoiceColumns = await GetApInvoiceAmountColumnsAsync(cn, ct);
        var invoiceOutstanding = invoiceColumns.Outstanding("iv");
        var invoicePaid = invoiceColumns.Paid("iv");
        var invoiceDueDate = await GetColumnExpressionAsync(cn, "Finance.ApInvoice", "iv", new[] { "DueDate", "InvoiceDate", "CreatedDateUtc" }, "iv.[CreatedDateUtc]", ct);
        var invoiceStatus = await GetColumnExpressionAsync(cn, "Finance.ApInvoice", "iv", new[] { "StatusCode", "Status" }, "N'Open'", ct);
        var commissionAmount = await GetDecimalColumnExpressionAsync(cn, "Commission.CommissionTransaction", "ct", new[] { "CommissionAmount", "Amount", "NetAmount", "GrossAmount" }, ct);
        var commissionStatus = await GetColumnExpressionAsync(cn, "Commission.CommissionTransaction", "ct", new[] { "StatusCode", "Status" }, "N'Pending'", ct);
        var commissionDate = await GetColumnExpressionAsync(cn, "Commission.CommissionTransaction", "ct", new[] { "TransactionDate", "PayoutDate", "CreatedDateUtc" }, "ct.[CreatedDateUtc]", ct);

        var sql = @$"
DECLARE @Now DATE = CAST(GETUTCDATE() AS DATE);
DECLARE @Som DATE = DATEFROMPARTS(YEAR(@Now), MONTH(@Now), 1);

SELECT
    ISNULL((SELECT SUM({invoiceOutstanding}) FROM Finance.ApInvoice iv WHERE iv.TenantId=@TenantId AND iv.IsDeleted=0 AND {invoiceStatus} NOT IN ('Paid','Void','Cancelled')),0) AS OutstandingArTotal,
    ISNULL((SELECT SUM({invoicePaid}) FROM Finance.ApInvoice iv WHERE iv.TenantId=@TenantId AND iv.IsDeleted=0 AND ({invoiceStatus}='Paid' OR {invoicePaid} > 0) AND CAST({invoiceDueDate} AS DATE)>=@Som),0) AS CollectedMtd,
    ISNULL((SELECT SUM({invoiceOutstanding}) FROM Finance.ApInvoice iv WHERE iv.TenantId=@TenantId AND iv.IsDeleted=0 AND CAST({invoiceDueDate} AS DATE)<@Now AND {invoiceStatus} NOT IN ('Paid','Void','Cancelled')),0) AS OverdueBalance,
    (SELECT COUNT(1) FROM Finance.ApInvoice iv WHERE iv.TenantId=@TenantId AND iv.IsDeleted=0 AND CAST({invoiceDueDate} AS DATE)<@Now AND {invoiceStatus} NOT IN ('Paid','Void','Cancelled')) AS OverdueInvoiceCount,
    (SELECT COUNT(1) FROM Finance.ApInvoice iv WHERE iv.TenantId=@TenantId AND iv.IsDeleted=0 AND {invoiceStatus} NOT IN ('Paid','Void','Cancelled')) AS TotalOpenInvoices,
    ISNULL((SELECT SUM({commissionAmount}) FROM Commission.CommissionTransaction ct WHERE ct.TenantId=@TenantId AND ct.IsDeleted=0 AND {commissionStatus} IN ('Pending','Draft','Accrued')),0) AS PendingCommissions,
    ISNULL((SELECT SUM({commissionAmount}) FROM Commission.CommissionTransaction ct WHERE ct.TenantId=@TenantId AND ct.IsDeleted=0 AND {commissionStatus}='Paid' AND CAST({commissionDate} AS DATE)>=@Som),0) AS PaidCommissionsMtd
FROM (VALUES(1)) AS _x(n);

SELECT
    CASE
        WHEN CAST({invoiceDueDate} AS DATE) >= @Now THEN 'Current'
        WHEN DATEDIFF(DAY,CAST({invoiceDueDate} AS DATE),@Now) BETWEEN 1  AND 30 THEN '1-30 Days'
        WHEN DATEDIFF(DAY,CAST({invoiceDueDate} AS DATE),@Now) BETWEEN 31 AND 60 THEN '31-60 Days'
        WHEN DATEDIFF(DAY,CAST({invoiceDueDate} AS DATE),@Now) BETWEEN 61 AND 90 THEN '61-90 Days'
        ELSE '90+ Days'
    END AS BucketLabel,
    SUM({invoiceOutstanding}) AS Amount,
    COUNT(1) AS InvoiceCount
FROM Finance.ApInvoice iv
WHERE iv.TenantId=@TenantId AND iv.IsDeleted=0 AND {invoiceStatus} NOT IN ('Paid','Void','Cancelled')
GROUP BY
    CASE
        WHEN CAST({invoiceDueDate} AS DATE) >= @Now THEN 'Current'
        WHEN DATEDIFF(DAY,CAST({invoiceDueDate} AS DATE),@Now) BETWEEN 1  AND 30 THEN '1-30 Days'
        WHEN DATEDIFF(DAY,CAST({invoiceDueDate} AS DATE),@Now) BETWEEN 31 AND 60 THEN '31-60 Days'
        WHEN DATEDIFF(DAY,CAST({invoiceDueDate} AS DATE),@Now) BETWEEN 61 AND 90 THEN '61-90 Days'
        ELSE '90+ Days'
    END
ORDER BY MIN(CAST({invoiceDueDate} AS DATE));";

        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        var summary  = await multi.ReadSingleOrDefaultAsync<BillingSummaryDto>() ?? new BillingSummaryDto();
        summary.ArAging = (await multi.ReadAsync<ArAgingBucketDto>()).ToList();
        return summary;
    }

    private static async Task<string> GetUserDisplayNameExpressionAsync(IDbConnection cn, CancellationToken ct)
    {
        const string sql = @"
SELECT [name]
FROM sys.columns
WHERE object_id = OBJECT_ID(N'IAM.[User]')
  AND [name] IN (N'DisplayName', N'FullName', N'UserName', N'Email');";

        var columns = (await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>();
        if (columns.Contains("DisplayName")) parts.Add("u.[DisplayName]");
        if (columns.Contains("FullName")) parts.Add("u.[FullName]");
        if (columns.Contains("UserName")) parts.Add("u.[UserName]");
        if (columns.Contains("Email")) parts.Add("u.[Email]");
        return parts.Count == 0 ? "N'Producer'" : $"COALESCE({string.Join(", ", parts)})";
    }

    private static async Task<string> GetUserActivePredicateAsync(IDbConnection cn, CancellationToken ct)
    {
        const string sql = @"
SELECT [name]
FROM sys.columns
WHERE object_id = OBJECT_ID(N'IAM.[User]')
  AND [name] IN (N'IsActive', N'StatusCode');";

        var columns = (await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (columns.Contains("IsActive")) return "u.[IsActive] = 1";
        if (columns.Contains("StatusCode")) return "u.[StatusCode] = N'Active'";
        return "1 = 1";
    }

    private static async Task<string> GetProducerUserIdExpressionAsync(IDbConnection cn, string tableName, string alias, string[] preferredColumns, CancellationToken ct)
    {
        const string sql = @"
SELECT [name]
FROM sys.columns
WHERE object_id = OBJECT_ID(@TableName);";

        var columns = (await cn.QueryAsync<string>(new CommandDefinition(sql, new { TableName = tableName }, cancellationToken: ct))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var column in preferredColumns)
        {
            if (columns.Contains(column))
            {
                return $"{alias}.[{column}]";
            }
        }

        return "CAST(NULL AS UNIQUEIDENTIFIER)";
    }

    private static async Task<string> GetColumnExpressionAsync(IDbConnection cn, string tableName, string alias, string[] preferredColumns, string fallbackExpression, CancellationToken ct)
    {
        var column = await GetFirstExistingColumnAsync(cn, tableName, preferredColumns, ct);
        return column is null ? fallbackExpression : $"{alias}.[{column}]";
    }

    private static async Task<string> GetDecimalColumnExpressionAsync(IDbConnection cn, string tableName, string alias, string[] preferredColumns, CancellationToken ct)
    {
        var column = await GetFirstExistingColumnAsync(cn, tableName, preferredColumns, ct);
        return column is null ? "CAST(NULL AS DECIMAL(18,2))" : $"{alias}.[{column}]";
    }

    private static async Task<string> GetBitPredicateExpressionAsync(IDbConnection cn, string tableName, string alias, string[] preferredColumns, CancellationToken ct)
    {
        var column = await GetFirstExistingColumnAsync(cn, tableName, preferredColumns, ct);
        return column is null ? "1 = 0" : $"{alias}.[{column}] = 1";
    }

    private static async Task<string?> GetFirstExistingColumnAsync(IDbConnection cn, string tableName, string[] preferredColumns, CancellationToken ct)
    {
        const string sql = @"
SELECT [name]
FROM sys.columns
WHERE object_id = OBJECT_ID(@TableName);";

        var columns = (await cn.QueryAsync<string>(new CommandDefinition(sql, new { TableName = tableName }, cancellationToken: ct))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return preferredColumns.FirstOrDefault(columns.Contains);
    }

    private static async Task<string> GetBranchStateProvinceExpressionAsync(IDbConnection cn, CancellationToken ct)
    {
        const string sql = @"
SELECT [name]
FROM sys.columns
WHERE object_id = OBJECT_ID(N'Core.Branch')
  AND [name] IN (N'StateProvince', N'StateCode');";

        var columns = (await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (columns.Contains("StateProvince")) return "b.[StateProvince]";
        if (columns.Contains("StateCode")) return "b.[StateCode]";
        return "CAST(NULL AS NVARCHAR(100))";
    }

    private static async Task<string> GetAgreementBranchIdExpressionAsync(IDbConnection cn, string alias, CancellationToken ct)
    {
        const string sql = @"
SELECT [name]
FROM sys.columns
WHERE object_id = OBJECT_ID(N'Sales.Agreement')
  AND [name] = N'BranchId';";

        var hasBranchId = await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql.Replace("SELECT [name]", "SELECT COUNT(1)"), cancellationToken: ct)) > 0;
        return hasBranchId
            ? $"{alias}.[BranchId]"
            : $"(SELECT TOP 1 u.[BranchId] FROM IAM.[User] u WHERE u.[UserId] = {alias}.[CreatedByUserId] AND u.[TenantId] = {alias}.[TenantId] AND u.[IsDeleted] = 0)";
    }

    private static async Task<ApInvoiceAmountColumns> GetApInvoiceAmountColumnsAsync(IDbConnection cn, CancellationToken ct)
    {
        const string sql = @"
SELECT [name]
FROM sys.columns
WHERE object_id = OBJECT_ID(N'Finance.ApInvoice')
  AND [name] IN (N'Amount', N'TotalAmount', N'AmountPaid', N'PaidAmount');";

        var columns = (await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var totalColumn = columns.Contains("Amount") ? "Amount" : columns.Contains("TotalAmount") ? "TotalAmount" : null;
        var paidColumn = columns.Contains("AmountPaid") ? "AmountPaid" : columns.Contains("PaidAmount") ? "PaidAmount" : null;
        return new ApInvoiceAmountColumns(totalColumn, paidColumn);
    }

    private static object StatusParameters(Guid tenantId) => new
    {
        TenantId = tenantId,
        ConvertedLeadStatusId = (int)LeadStatus.Converted,
        DisqualifiedLeadStatusId = (int)LeadStatus.Disqualified,
        OpenOpportunityStatusId = (int)OpportunityStatus.Open,
        ActiveAgreementStatusId = (int)AgreementStatus.Active,
        CancelledAgreementStatusId = (int)AgreementStatus.Cancelled
    };

    private sealed record ApInvoiceAmountColumns(string? TotalColumn, string? PaidColumn)
    {
        public string Outstanding(string? alias = null) => $"{Column(alias, TotalColumn)} - {Column(alias, PaidColumn)}";
        public string Paid(string? alias = null) => Column(alias, PaidColumn);

        private static string Column(string? alias, string? columnName)
            => columnName is null
                ? "CAST(0 AS DECIMAL(18,2))"
                : string.IsNullOrWhiteSpace(alias) ? $"[{columnName}]" : $"{alias}.[{columnName}]";
    }
}
