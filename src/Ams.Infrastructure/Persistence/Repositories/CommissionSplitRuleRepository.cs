using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionSplitRuleRepository : ICommissionSplitRuleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionSplitRuleRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionSplitRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = @"SELECT SplitRuleId, TenantId, CommissionPlanId, RuleName, SplitTypeCode, PayeeId, SplitPct, OverrideRatePct, Priority, EffectiveStartDate, EffectiveEndDate, StatusCode, CreatedDateUtc FROM Commission.CommissionSplitRule WHERE SplitRuleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionSplitRuleDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionSplitRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql(
            "Commission.CommissionSplitRule",
            "SplitRuleId, TenantId, CommissionPlanId, RuleName, SplitTypeCode, PayeeId, SplitPct, OverrideRatePct, Priority, EffectiveStartDate, EffectiveEndDate, StatusCode, CreatedDateUtc",
            "RuleName LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
            true);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<CommissionSplitRuleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionSplitRuleDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionSplitRuleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Commission.CommissionSplitRule (SplitRuleId, TenantId, CommissionPlanId, RuleName, SplitTypeCode, PayeeId, SplitPct, OverrideRatePct, Priority, EffectiveStartDate, EffectiveEndDate, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @CommissionPlanId, @RuleName, @SplitTypeCode, @PayeeId, @SplitPct, @OverrideRatePct, @Priority, @EffectiveStartDate, @EffectiveEndDate, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CommissionPlanId, request.RuleName, request.SplitTypeCode, request.PayeeId, request.SplitPct, request.OverrideRatePct, request.Priority, request.EffectiveStartDate, request.EffectiveEndDate, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionSplitRuleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Commission.CommissionSplitRule
SET CommissionPlanId = @CommissionPlanId,
    RuleName = @RuleName,
    SplitTypeCode = @SplitTypeCode,
    PayeeId = @PayeeId,
    SplitPct = @SplitPct,
    OverrideRatePct = @OverrideRatePct,
    Priority = @Priority,
    EffectiveStartDate = @EffectiveStartDate,
    EffectiveEndDate = @EffectiveEndDate,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SplitRuleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.CommissionPlanId, request.RuleName, request.SplitTypeCode, request.PayeeId, request.SplitPct, request.OverrideRatePct, request.Priority, request.EffectiveStartDate, request.EffectiveEndDate, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Commission.CommissionPlan', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPlan
    (
        CommissionPlanId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        PlanCode NVARCHAR(50) NOT NULL,
        PlanName NVARCHAR(200) NOT NULL,
        PlanTypeCode NVARCHAR(50) NOT NULL DEFAULT N'Standard',
        NewBusinessRatePct DECIMAL(9,4) NOT NULL DEFAULT 0,
        RenewalRatePct DECIMAL(9,4) NOT NULL DEFAULT 0,
        EffectiveStartDate DATE NOT NULL DEFAULT CONVERT(date, SYSUTCDATETIME()),
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft',
        AllowSplit BIT NOT NULL DEFAULT 0,
        HouseAccountRules BIT NOT NULL DEFAULT 0,
        BranchOverrideEligible BIT NOT NULL DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;

IF OBJECT_ID(N'Commission.CommissionSplitRule', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionSplitRule
    (
        SplitRuleId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CommissionPlanId UNIQUEIDENTIFIER NOT NULL,
        RuleName NVARCHAR(200) NOT NULL,
        SplitTypeCode NVARCHAR(50) NOT NULL,
        PayeeId UNIQUEIDENTIFIER NULL,
        SplitPct DECIMAL(9,4) NOT NULL DEFAULT 0,
        OverrideRatePct DECIMAL(9,4) NULL,
        Priority INT NOT NULL DEFAULT 100,
        EffectiveStartDate DATE NOT NULL DEFAULT CONVERT(date, SYSUTCDATETIME()),
        EffectiveEndDate DATE NULL,
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Active',
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000' AND NOT EXISTS (SELECT 1 FROM Commission.CommissionPlan WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Commission.CommissionPlan (CommissionPlanId, TenantId, PlanCode, PlanName, PlanTypeCode, NewBusinessRatePct, RenewalRatePct, EffectiveStartDate, StatusCode, AllowSplit, HouseAccountRules, BranchOverrideEligible, CreatedDateUtc, IsDeleted)
    VALUES (NEWID(), @TenantId, N'COMM-STD', N'Standard Producer Plan', N'Standard', 10, 8, DATEFROMPARTS(YEAR(GETUTCDATE()), 1, 1), N'Active', 1, 0, 0, SYSUTCDATETIME(), 0);
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000' AND NOT EXISTS (SELECT 1 FROM Commission.CommissionSplitRule WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    DECLARE @PlanId UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
    INSERT INTO Commission.CommissionSplitRule (SplitRuleId, TenantId, CommissionPlanId, RuleName, SplitTypeCode, SplitPct, OverrideRatePct, Priority, EffectiveStartDate, StatusCode, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, @PlanId, N'Primary Producer Split', N'Producer', 70, NULL, 10, DATEFROMPARTS(YEAR(GETUTCDATE()), 1, 1), N'Active', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PlanId, N'CSR Support Split', N'CSR', 15, NULL, 20, DATEFROMPARTS(YEAR(GETUTCDATE()), 1, 1), N'Active', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PlanId, N'House Account Retention Split', N'House Account', 100, NULL, 30, DATEFROMPARTS(YEAR(GETUTCDATE()), 1, 1), N'Active', SYSUTCDATETIME(), 0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
