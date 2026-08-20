using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPlanRepository : ICommissionPlanRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionPlanRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionPlanDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = @"SELECT CommissionPlanId, TenantId, PlanCode, PlanName, PlanTypeCode, NewBusinessRatePct, RenewalRatePct, EffectiveStartDate, StatusCode, AllowSplit, HouseAccountRules, BranchOverrideEligible, CreatedDateUtc FROM Commission.CommissionPlan WHERE CommissionPlanId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPlanDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPlanDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql(
            "Commission.CommissionPlan",
            "CommissionPlanId, TenantId, PlanCode, PlanName, PlanTypeCode, NewBusinessRatePct, RenewalRatePct, EffectiveStartDate, StatusCode, AllowSplit, HouseAccountRules, BranchOverrideEligible, CreatedDateUtc",
            "PlanCode LIKE '%' + @SearchTerm + '%' OR PlanName LIKE '%' + @SearchTerm + '%'",
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

        var items = (await multi.ReadAsync<CommissionPlanDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionPlanDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionPlanRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string hasStatusCodeIdSql = "SELECT CASE WHEN COL_LENGTH(N'Commission.CommissionPlan', N'StatusCodeId') IS NULL THEN 0 ELSE 1 END;";
        const string sql = @"
INSERT INTO Commission.CommissionPlan (CommissionPlanId, TenantId, PlanCode, PlanName, PlanTypeCode, NewBusinessRatePct, RenewalRatePct, EffectiveStartDate, StatusCode, AllowSplit, HouseAccountRules, BranchOverrideEligible, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @PlanCode, @PlanName, @PlanTypeCode, @NewBusinessRatePct, @RenewalRatePct, @EffectiveStartDate, @StatusCode, @AllowSplit, @HouseAccountRules, @BranchOverrideEligible, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        const string sqlWithStatusCodeId = @"
INSERT INTO Commission.CommissionPlan (CommissionPlanId, TenantId, PlanCode, PlanName, PlanTypeCode, NewBusinessRatePct, RenewalRatePct, EffectiveStartDate, StatusCode, StatusCodeId, AllowSplit, HouseAccountRules, BranchOverrideEligible, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @PlanCode, @PlanName, @PlanTypeCode, @NewBusinessRatePct, @RenewalRatePct, @EffectiveStartDate, @StatusCode, @StatusCodeId, @AllowSplit, @HouseAccountRules, @BranchOverrideEligible, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var hasStatusCodeId = await cn.ExecuteScalarAsync<int>(new CommandDefinition(hasStatusCodeIdSql, cancellationToken: cancellationToken)) == 1;
        await cn.ExecuteAsync(new CommandDefinition(hasStatusCodeId ? sqlWithStatusCodeId : sql, new { Id = id, request.TenantId, request.PlanCode, request.PlanName, request.PlanTypeCode, request.NewBusinessRatePct, request.RenewalRatePct, request.EffectiveStartDate, request.StatusCode, StatusCodeId = ToStatusCodeId(request.StatusCode), request.AllowSplit, request.HouseAccountRules, request.BranchOverrideEligible, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionPlanRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Commission.CommissionPlan
SET PlanCode = @PlanCode,
    PlanName = @PlanName,
    PlanTypeCode = @PlanTypeCode,
    NewBusinessRatePct = @NewBusinessRatePct,
    RenewalRatePct = @RenewalRatePct,
    EffectiveStartDate = @EffectiveStartDate,
    StatusCode = @StatusCode,
    AllowSplit = @AllowSplit,
    HouseAccountRules = @HouseAccountRules,
    BranchOverrideEligible = @BranchOverrideEligible,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CommissionPlanId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.PlanCode, request.PlanName, request.PlanTypeCode, request.NewBusinessRatePct, request.RenewalRatePct, request.EffectiveStartDate, request.StatusCode, request.AllowSplit, request.HouseAccountRules, request.BranchOverrideEligible, request.ModifiedByUserId }, cancellationToken: cancellationToken));
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
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionPlan', N'PlanTypeCode') IS NULL ALTER TABLE Commission.CommissionPlan ADD PlanTypeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPlan_Type DEFAULT N'Standard';
    IF COL_LENGTH(N'Commission.CommissionPlan', N'NewBusinessRatePct') IS NULL ALTER TABLE Commission.CommissionPlan ADD NewBusinessRatePct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPlan_NewRate DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPlan', N'RenewalRatePct') IS NULL ALTER TABLE Commission.CommissionPlan ADD RenewalRatePct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPlan_RenewRate DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPlan', N'EffectiveStartDate') IS NULL ALTER TABLE Commission.CommissionPlan ADD EffectiveStartDate DATE NOT NULL CONSTRAINT DF_CommissionPlan_Start DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionPlan', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPlan ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPlan_Status DEFAULT N'Draft';
    IF COL_LENGTH(N'Commission.CommissionPlan', N'AllowSplit') IS NULL ALTER TABLE Commission.CommissionPlan ADD AllowSplit BIT NOT NULL CONSTRAINT DF_CommissionPlan_AllowSplit DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPlan', N'HouseAccountRules') IS NULL ALTER TABLE Commission.CommissionPlan ADD HouseAccountRules BIT NOT NULL CONSTRAINT DF_CommissionPlan_House DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPlan', N'BranchOverrideEligible') IS NULL ALTER TABLE Commission.CommissionPlan ADD BranchOverrideEligible BIT NOT NULL CONSTRAINT DF_CommissionPlan_Branch DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPlan', N'CreatedByUserId') IS NULL ALTER TABLE Commission.CommissionPlan ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPlan', N'ModifiedDateUtc') IS NULL ALTER TABLE Commission.CommissionPlan ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionPlan', N'ModifiedByUserId') IS NULL ALTER TABLE Commission.CommissionPlan ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPlan', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPlan ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionPlan_IsDeleted DEFAULT 0;
END";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private static int ToStatusCodeId(string? statusCode) => statusCode switch
    {
        "Draft" => 0,
        "Archived" => 2,
        _ => 1
    };
}
