using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPlannerScenarioRepository : ICommissionPlannerScenarioRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionPlannerScenarioRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionPlannerScenarioDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = SelectSql + @"
WHERE s.ScenarioId = @Id AND s.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPlannerScenarioDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPlannerScenarioDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? scenarioTypeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        const string sql = SelectSql + @"
WHERE s.TenantId = @TenantId
  AND s.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR s.ScenarioNumber LIKE N'%' + @SearchTerm + N'%' OR s.ScenarioName LIKE N'%' + @SearchTerm + N'%' OR s.ScenarioTypeCode LIKE N'%' + @SearchTerm + N'%' OR s.SplitTypeCode LIKE N'%' + @SearchTerm + N'%' OR s.StatusCode LIKE N'%' + @SearchTerm + N'%' OR s.Notes LIKE N'%' + @SearchTerm + N'%' OR p.PayeeTypeCode LIKE N'%' + @SearchTerm + N'%' OR cp.PlanName LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR s.StatusCode = @StatusCode)
  AND (@ScenarioTypeCode IS NULL OR @ScenarioTypeCode = N'' OR s.ScenarioTypeCode = @ScenarioTypeCode)
ORDER BY s.CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Commission.CommissionPlannerScenario s
LEFT JOIN Commission.CommissionPayee p ON p.PayeeId = s.PayeeId
LEFT JOIN Commission.CommissionPlan cp ON cp.CommissionPlanId = s.CommissionPlanId
WHERE s.TenantId = @TenantId
  AND s.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR s.ScenarioNumber LIKE N'%' + @SearchTerm + N'%' OR s.ScenarioName LIKE N'%' + @SearchTerm + N'%' OR s.ScenarioTypeCode LIKE N'%' + @SearchTerm + N'%' OR s.SplitTypeCode LIKE N'%' + @SearchTerm + N'%' OR s.StatusCode LIKE N'%' + @SearchTerm + N'%' OR s.Notes LIKE N'%' + @SearchTerm + N'%' OR p.PayeeTypeCode LIKE N'%' + @SearchTerm + N'%' OR cp.PlanName LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR s.StatusCode = @StatusCode)
  AND (@ScenarioTypeCode IS NULL OR @ScenarioTypeCode = N'' OR s.ScenarioTypeCode = @ScenarioTypeCode);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            StatusCode = statusCode,
            ScenarioTypeCode = scenarioTypeCode,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<CommissionPlannerScenarioDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionPlannerScenarioDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionPlannerScenarioRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Commission.CommissionPlannerScenario
(
    ScenarioId, TenantId, CommissionPlanId, PayeeId, ScenarioNumber, ScenarioName, ScenarioTypeCode,
    NewBusinessPremium, RenewalPremium, PolicyCount, NewBusinessRatePct, RenewalRatePct, OverrideRatePct,
    SplitTypeCode, PrimarySplitPct, SecondarySplitPct, BranchOverride, HouseAccount, SharedClawbacks,
    CancellationRatePct, NsfRatePct, NewBusinessCommission, RenewalCommission, OverrideCommission,
    TotalCommission, ProjectedClawbacks, NetPayout, PrimaryNetPayout, SecondaryNetPayout, BranchNetPayout,
    StatusCode, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @Id, @TenantId, @CommissionPlanId, @PayeeId, @ScenarioNumber, @ScenarioName, @ScenarioTypeCode,
    @NewBusinessPremium, @RenewalPremium, @PolicyCount, @NewBusinessRatePct, @RenewalRatePct, @OverrideRatePct,
    @SplitTypeCode, @PrimarySplitPct, @SecondarySplitPct, @BranchOverride, @HouseAccount, @SharedClawbacks,
    @CancellationRatePct, @NsfRatePct, @NewBusinessCommission, @RenewalCommission, @OverrideCommission,
    @TotalCommission, @ProjectedClawbacks, @NetPayout, @PrimaryNetPayout, @SecondaryNetPayout, @BranchNetPayout,
    @StatusCode, @Notes, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CommissionPlanId, request.PayeeId, request.ScenarioNumber, request.ScenarioName, request.ScenarioTypeCode, request.NewBusinessPremium, request.RenewalPremium, request.PolicyCount, request.NewBusinessRatePct, request.RenewalRatePct, request.OverrideRatePct, request.SplitTypeCode, request.PrimarySplitPct, request.SecondarySplitPct, request.BranchOverride, request.HouseAccount, request.SharedClawbacks, request.CancellationRatePct, request.NsfRatePct, request.NewBusinessCommission, request.RenewalCommission, request.OverrideCommission, request.TotalCommission, request.ProjectedClawbacks, request.NetPayout, request.PrimaryNetPayout, request.SecondaryNetPayout, request.BranchNetPayout, request.StatusCode, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionPlannerScenarioRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Commission.CommissionPlannerScenario
SET CommissionPlanId = @CommissionPlanId,
    PayeeId = @PayeeId,
    ScenarioNumber = @ScenarioNumber,
    ScenarioName = @ScenarioName,
    ScenarioTypeCode = @ScenarioTypeCode,
    NewBusinessPremium = @NewBusinessPremium,
    RenewalPremium = @RenewalPremium,
    PolicyCount = @PolicyCount,
    NewBusinessRatePct = @NewBusinessRatePct,
    RenewalRatePct = @RenewalRatePct,
    OverrideRatePct = @OverrideRatePct,
    SplitTypeCode = @SplitTypeCode,
    PrimarySplitPct = @PrimarySplitPct,
    SecondarySplitPct = @SecondarySplitPct,
    BranchOverride = @BranchOverride,
    HouseAccount = @HouseAccount,
    SharedClawbacks = @SharedClawbacks,
    CancellationRatePct = @CancellationRatePct,
    NsfRatePct = @NsfRatePct,
    NewBusinessCommission = @NewBusinessCommission,
    RenewalCommission = @RenewalCommission,
    OverrideCommission = @OverrideCommission,
    TotalCommission = @TotalCommission,
    ProjectedClawbacks = @ProjectedClawbacks,
    NetPayout = @NetPayout,
    PrimaryNetPayout = @PrimaryNetPayout,
    SecondaryNetPayout = @SecondaryNetPayout,
    BranchNetPayout = @BranchNetPayout,
    StatusCode = @StatusCode,
    Notes = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ScenarioId = @Id AND TenantId = @TenantId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CommissionPlanId, request.PayeeId, request.ScenarioNumber, request.ScenarioName, request.ScenarioTypeCode, request.NewBusinessPremium, request.RenewalPremium, request.PolicyCount, request.NewBusinessRatePct, request.RenewalRatePct, request.OverrideRatePct, request.SplitTypeCode, request.PrimarySplitPct, request.SecondarySplitPct, request.BranchOverride, request.HouseAccount, request.SharedClawbacks, request.CancellationRatePct, request.NsfRatePct, request.NewBusinessCommission, request.RenewalCommission, request.OverrideCommission, request.TotalCommission, request.ProjectedClawbacks, request.NetPayout, request.PrimaryNetPayout, request.SecondaryNetPayout, request.BranchNetPayout, request.StatusCode, request.Notes, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Commission.CommissionPlannerScenario', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPlannerScenario
    (
        ScenarioId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CommissionPlanId UNIQUEIDENTIFIER NULL,
        PayeeId UNIQUEIDENTIFIER NULL,
        ScenarioNumber NVARCHAR(80) NOT NULL,
        ScenarioName NVARCHAR(200) NOT NULL,
        ScenarioTypeCode NVARCHAR(50) NOT NULL DEFAULT N'What-If',
        NewBusinessPremium DECIMAL(18,2) NOT NULL DEFAULT 0,
        RenewalPremium DECIMAL(18,2) NOT NULL DEFAULT 0,
        PolicyCount INT NOT NULL DEFAULT 0,
        NewBusinessRatePct DECIMAL(9,2) NOT NULL DEFAULT 0,
        RenewalRatePct DECIMAL(9,2) NOT NULL DEFAULT 0,
        OverrideRatePct DECIMAL(9,2) NOT NULL DEFAULT 0,
        SplitTypeCode NVARCHAR(50) NOT NULL DEFAULT N'60/40',
        PrimarySplitPct DECIMAL(9,2) NOT NULL DEFAULT 60,
        SecondarySplitPct DECIMAL(9,2) NOT NULL DEFAULT 40,
        BranchOverride BIT NOT NULL DEFAULT 0,
        HouseAccount BIT NOT NULL DEFAULT 0,
        SharedClawbacks BIT NOT NULL DEFAULT 1,
        CancellationRatePct DECIMAL(9,2) NOT NULL DEFAULT 0,
        NsfRatePct DECIMAL(9,2) NOT NULL DEFAULT 0,
        NewBusinessCommission DECIMAL(18,2) NOT NULL DEFAULT 0,
        RenewalCommission DECIMAL(18,2) NOT NULL DEFAULT 0,
        OverrideCommission DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalCommission DECIMAL(18,2) NOT NULL DEFAULT 0,
        ProjectedClawbacks DECIMAL(18,2) NOT NULL DEFAULT 0,
        NetPayout DECIMAL(18,2) NOT NULL DEFAULT 0,
        PrimaryNetPayout DECIMAL(18,2) NOT NULL DEFAULT 0,
        SecondaryNetPayout DECIMAL(18,2) NOT NULL DEFAULT 0,
        BranchNetPayout DECIMAL(18,2) NOT NULL DEFAULT 0,
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft',
        Notes NVARCHAR(1000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000' AND NOT EXISTS (SELECT 1 FROM Commission.CommissionPlannerScenario WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    DECLARE @PlanId UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);
    DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);

    INSERT INTO Commission.CommissionPlannerScenario (ScenarioId, TenantId, CommissionPlanId, PayeeId, ScenarioNumber, ScenarioName, ScenarioTypeCode, NewBusinessPremium, RenewalPremium, PolicyCount, NewBusinessRatePct, RenewalRatePct, OverrideRatePct, SplitTypeCode, PrimarySplitPct, SecondarySplitPct, BranchOverride, HouseAccount, SharedClawbacks, CancellationRatePct, NsfRatePct, NewBusinessCommission, RenewalCommission, OverrideCommission, TotalCommission, ProjectedClawbacks, NetPayout, PrimaryNetPayout, SecondaryNetPayout, BranchNetPayout, StatusCode, Notes, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, @PlanId, @PayeeId, CONCAT(N'WP-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-BASE'), N'Base Producer 60/40 Scenario', N'What-If', 500000, 300000, 120, 10, 8, 0, N'60/40', 60, 40, 0, 0, 1, 3, .5, 50000, 24000, 0, 74000, 2590, 71410, 42846, 28564, 0, N'Active', N'Current producer split model synced to active commission workflow.', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PlanId, @PayeeId, CONCAT(N'WP-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-BRANCH'), N'Branch Override Growth Scenario', N'Branch Override', 720000, 360000, 168, 11, 8.5, 2, N'Branch Override', 70, 20, 1, 0, 1, 2.5, .25, 79200, 30600, 21600, 131400, 3613.50, 127786.50, 89450.55, 25557.30, 21600, N'Draft', N'Models branch override impact before creating a formal split rule.', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PlanId, @PayeeId, CONCAT(N'WP-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-HOUSE'), N'House Account Renewal Scenario', N'House Account', 125000, 640000, 94, 6, 7.5, 0, N'House Account', 100, 0, 0, 1, 1, 1.5, .15, 7500, 48000, 0, 55500, 915.75, 54584.25, 54584.25, 0, 0, N'Approved', N'House account workflow scenario used to compare retained agency payout.', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PlanId, @PayeeId, CONCAT(N'WP-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-RISK'), N'Cancellation Risk Stress Test', N'Risk Model', 420000, 275000, 82, 10, 8, 0, N'50/50', 50, 50, 0, 0, 1, 8, 1.5, 42000, 22000, 0, 64000, 6080, 57920, 28960, 28960, 0, N'Draft', N'Stress test for projected clawbacks and payout exception exposure.', SYSUTCDATETIME(), 0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private const string SelectSql = @"
SELECT s.ScenarioId,
       s.TenantId,
       s.CommissionPlanId,
       COALESCE(cp.PlanName, N'Unassigned plan') AS PlanName,
       s.PayeeId,
       COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(p.PayeeTypeCode, N' ', CONVERT(nvarchar(36), p.PayeeId)))), N''), N'All payees') AS PayeeName,
       s.ScenarioNumber,
       s.ScenarioName,
       s.ScenarioTypeCode,
       s.NewBusinessPremium,
       s.RenewalPremium,
       s.PolicyCount,
       s.NewBusinessRatePct,
       s.RenewalRatePct,
       s.OverrideRatePct,
       s.SplitTypeCode,
       s.PrimarySplitPct,
       s.SecondarySplitPct,
       s.BranchOverride,
       s.HouseAccount,
       s.SharedClawbacks,
       s.CancellationRatePct,
       s.NsfRatePct,
       s.NewBusinessCommission,
       s.RenewalCommission,
       s.OverrideCommission,
       s.TotalCommission,
       s.ProjectedClawbacks,
       s.NetPayout,
       s.PrimaryNetPayout,
       s.SecondaryNetPayout,
       s.BranchNetPayout,
       s.StatusCode,
       COALESCE(s.Notes, N'') AS Notes,
       s.CreatedDateUtc
FROM Commission.CommissionPlannerScenario s
LEFT JOIN Commission.CommissionPayee p ON p.PayeeId = s.PayeeId
LEFT JOIN Commission.CommissionPlan cp ON cp.CommissionPlanId = s.CommissionPlanId";
}
