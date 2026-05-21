using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPayeeRepository : ICommissionPayeeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommissionPayeeRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CommissionPayeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = "SELECT PayeeId, TenantId, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc FROM Commission.CommissionPayee WHERE PayeeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPayeeDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPayeeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Commission.CommissionPayee", "PayeeId, TenantId, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc", "PayeeTypeCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CommissionPayeeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CommissionPayeeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCommissionPayeeRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Commission.CommissionPayee (PayeeId, TenantId, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @UserId, @CommissionPlanId, @PayeeTypeCode, @SplitPercentage, @EffectiveDate, @StatusCode, SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.UserId, request.CommissionPlanId, request.PayeeTypeCode, request.SplitPercentage, request.EffectiveDate, request.StatusCode }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionPayeeRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Commission.CommissionPayee
SET UserId = @UserId,
    CommissionPlanId = @CommissionPlanId,
    PayeeTypeCode = @PayeeTypeCode,
    SplitPercentage = @SplitPercentage,
    EffectiveDate = @EffectiveDate,
    StatusCode = @StatusCode
WHERE PayeeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.UserId, request.CommissionPlanId, request.PayeeTypeCode, request.SplitPercentage, request.EffectiveDate, request.StatusCode }, cancellationToken: cancellationToken));
    }

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Commission.CommissionPlan', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPlan (CommissionPlanId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PlanCode NVARCHAR(50) NOT NULL, PlanName NVARCHAR(200) NOT NULL, PlanTypeCode NVARCHAR(50) NOT NULL DEFAULT N'Standard', NewBusinessRatePct DECIMAL(9,4) NOT NULL DEFAULT 0, RenewalRatePct DECIMAL(9,4) NOT NULL DEFAULT 0, EffectiveStartDate DATE NOT NULL DEFAULT CONVERT(date, SYSUTCDATETIME()), StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft', AllowSplit BIT NOT NULL DEFAULT 0, HouseAccountRules BIT NOT NULL DEFAULT 0, BranchOverrideEligible BIT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Commission.CommissionPayee', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPayee (PayeeId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, UserId UNIQUEIDENTIFIER NULL, CommissionPlanId UNIQUEIDENTIFIER NOT NULL, PayeeTypeCode NVARCHAR(50) NOT NULL, SplitPercentage DECIMAL(9,4) NOT NULL DEFAULT 100, EffectiveDate DATE NOT NULL DEFAULT CONVERT(date, SYSUTCDATETIME()), StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Active', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'TenantId') IS NULL ALTER TABLE Commission.CommissionPayee ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionPayeeRepo_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'UserId') IS NULL ALTER TABLE Commission.CommissionPayee ADD UserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionPayee ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeTypeCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeTypeCode NVARCHAR(50) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'SplitPercentage') IS NULL ALTER TABLE Commission.CommissionPayee ADD SplitPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPayeeRepo_Split DEFAULT 100;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'EffectiveDate') IS NULL ALTER TABLE Commission.CommissionPayee ADD EffectiveDate DATE NOT NULL CONSTRAINT DF_CommissionPayeeRepo_Effective DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionPayee', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPayeeRepo_Status DEFAULT N'Active';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayee ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionPayeeRepo_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionPayee', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayee ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionPayeeRepo_IsDeleted DEFAULT 0;
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
       AND COL_LENGTH(N''Commission.CommissionPayee'', N''CommissionPayeeTypeId'') IS NULL
    BEGIN
        DECLARE @PlanId UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        IF @PlanId IS NOT NULL
        BEGIN
            IF COL_LENGTH(N''Commission.CommissionPayee'', N''PayeeCode'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayee'', N''PayeeName'') IS NOT NULL
            BEGIN
                INSERT INTO Commission.CommissionPayee (PayeeId, TenantId, PayeeCode, PayeeName, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc, IsDeleted)
                VALUES
                (NEWID(), @SeedTenantId, CONCAT(N''PAY-'', LEFT(CONVERT(nvarchar(36), NEWID()), 8)), N''Demo Producer'', NULL, @PlanId, N''Producer'', 100, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0),
                (NEWID(), @SeedTenantId, CONCAT(N''PAY-'', LEFT(CONVERT(nvarchar(36), NEWID()), 8)), N''Demo CSR'', NULL, @PlanId, N''CSR'', 15, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0),
                (NEWID(), @SeedTenantId, CONCAT(N''PAY-'', LEFT(CONVERT(nvarchar(36), NEWID()), 8)), N''Demo Branch Manager'', NULL, @PlanId, N''Branch Manager'', 5, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0);
            END
            ELSE IF COL_LENGTH(N''Commission.CommissionPayee'', N''PayeeCode'') IS NOT NULL
            BEGIN
                INSERT INTO Commission.CommissionPayee (PayeeId, TenantId, PayeeCode, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc, IsDeleted)
                VALUES
                (NEWID(), @SeedTenantId, CONCAT(N''PAY-'', LEFT(CONVERT(nvarchar(36), NEWID()), 8)), NULL, @PlanId, N''Producer'', 100, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0),
                (NEWID(), @SeedTenantId, CONCAT(N''PAY-'', LEFT(CONVERT(nvarchar(36), NEWID()), 8)), NULL, @PlanId, N''CSR'', 15, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0),
                (NEWID(), @SeedTenantId, CONCAT(N''PAY-'', LEFT(CONVERT(nvarchar(36), NEWID()), 8)), NULL, @PlanId, N''Branch Manager'', 5, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0);
            END
            ELSE
            BEGIN
                INSERT INTO Commission.CommissionPayee (PayeeId, TenantId, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc, IsDeleted)
                VALUES
                (NEWID(), @SeedTenantId, NULL, @PlanId, N''Producer'', 100, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0),
                (NEWID(), @SeedTenantId, NULL, @PlanId, N''CSR'', 15, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0),
                (NEWID(), @SeedTenantId, NULL, @PlanId, N''Branch Manager'', 5, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0);
            END
        END
    END', N'@SeedTenantId UNIQUEIDENTIFIER', @SeedTenantId = @TenantId;
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
