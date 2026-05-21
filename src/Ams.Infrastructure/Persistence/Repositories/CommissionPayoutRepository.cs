using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPayoutRepository : ICommissionPayoutRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommissionPayoutRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CommissionPayoutDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);

        const string sql = "SELECT PayoutId, TenantId, PayeeId, PayoutDate, TotalAmount, StatusCode, ProcessedDateUtc, Notes, CreatedDateUtc FROM Commission.CommissionPayout WHERE PayoutId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPayoutDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPayoutDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var sql = RepositorySql.BuildPagedSearchSql("Commission.CommissionPayout", "PayoutId, TenantId, PayeeId, PayoutDate, TotalAmount, StatusCode, ProcessedDateUtc, Notes, CreatedDateUtc", "Notes LIKE '%' + @SearchTerm + '%'", "PayoutDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CommissionPayoutDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CommissionPayoutDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCommissionPayoutRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Commission.CommissionPayout (PayoutId, TenantId, PayeeId, PayoutDate, TotalAmount, StatusCode, ProcessedDateUtc, Notes, CreatedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @PayeeId, @PayoutDate, @TotalAmount, @StatusCode, @ProcessedDateUtc, @Notes, SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.PayeeId, request.PayoutDate, request.TotalAmount, request.StatusCode, request.ProcessedDateUtc, request.Notes }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionPayoutRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Commission.CommissionPayout
SET PayeeId = @PayeeId,
    PayoutDate = @PayoutDate,
    TotalAmount = @TotalAmount,
    StatusCode = @StatusCode,
    ProcessedDateUtc = @ProcessedDateUtc,
    Notes = @Notes
WHERE PayoutId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.PayeeId, request.PayoutDate, request.TotalAmount, request.StatusCode, request.ProcessedDateUtc, request.Notes }, cancellationToken: cancellationToken));
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
END;

IF OBJECT_ID(N'Commission.CommissionPayout', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPayout (PayoutId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, PayoutDate DATE NOT NULL DEFAULT CONVERT(date, SYSUTCDATETIME()), TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft', ProcessedDateUtc DATETIME2 NULL, Notes NVARCHAR(1000) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionPayout', N'PayoutId') IS NULL ALTER TABLE Commission.CommissionPayout ADD PayoutId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayout', N'TenantId') IS NULL ALTER TABLE Commission.CommissionPayout ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionPayout_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionPayout', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionPayout ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayout', N'PayoutDate') IS NULL ALTER TABLE Commission.CommissionPayout ADD PayoutDate DATE NOT NULL CONSTRAINT DF_CommissionPayout_Date DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionPayout', N'TotalAmount') IS NULL ALTER TABLE Commission.CommissionPayout ADD TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionPayout_Total DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPayout', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayout ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPayout_Status DEFAULT N'Draft';
    IF COL_LENGTH(N'Commission.CommissionPayout', N'ProcessedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayout ADD ProcessedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionPayout', N'Notes') IS NULL ALTER TABLE Commission.CommissionPayout ADD Notes NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayout', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayout ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionPayout_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionPayout', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayout ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionPayout_IsDeleted DEFAULT 0;
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPayout WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        IF @PayeeId IS NOT NULL
        BEGIN
            INSERT INTO Commission.CommissionPayout (PayoutId, TenantId, PayeeId, PayoutDate, TotalAmount, StatusCode, ProcessedDateUtc, Notes, CreatedDateUtc, IsDeleted)
            VALUES
            (NEWID(), @SeedTenantId, @PayeeId, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), 4500, N''Processed'', DATEADD(day, -13, SYSUTCDATETIME()), N''Seed payout for earned new business commissions'', SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @PayeeId, DATEADD(day, -7, CONVERT(date, SYSUTCDATETIME())), 3100, N''Approved'', NULL, N''Approved payout awaiting payment run'', SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @PayeeId, CONVERT(date, SYSUTCDATETIME()), 2240, N''Draft'', NULL, N''Draft payout for renewal commissions'', SYSUTCDATETIME(), 0);
        END
    END', N'@SeedTenantId UNIQUEIDENTIFIER', @SeedTenantId = @TenantId;
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
