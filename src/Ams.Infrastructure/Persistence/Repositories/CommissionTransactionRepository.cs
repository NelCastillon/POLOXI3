using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionTransactionRepository : ICommissionTransactionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommissionTransactionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CommissionTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = "SELECT TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, PayoutId, CreatedDateUtc FROM Commission.CommissionTransaction WHERE TransactionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionTransactionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionTransactionDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Commission.CommissionTransaction", "TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, PayoutId, CreatedDateUtc", "SourceEntityName LIKE '%' + @SearchTerm + '%'", "TransactionDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CommissionTransactionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CommissionTransactionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
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
    IF COL_LENGTH(N'Commission.CommissionPayee', N'TenantId') IS NULL ALTER TABLE Commission.CommissionPayee ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionTxPayee_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'UserId') IS NULL ALTER TABLE Commission.CommissionPayee ADD UserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionPayee ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeTypeCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeTypeCode NVARCHAR(50) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'SplitPercentage') IS NULL ALTER TABLE Commission.CommissionPayee ADD SplitPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionTxPayee_Split DEFAULT 100;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'EffectiveDate') IS NULL ALTER TABLE Commission.CommissionPayee ADD EffectiveDate DATE NOT NULL CONSTRAINT DF_CommissionTxPayee_Effective DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionPayee', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionTxPayee_Status DEFAULT N'Active';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayee ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionTxPayee_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionPayee', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayee ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionTxPayee_IsDeleted DEFAULT 0;
END;

IF OBJECT_ID(N'Commission.CommissionTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionTransaction (TransactionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, CommissionPlanId UNIQUEIDENTIFIER NOT NULL, SourceEntityName NVARCHAR(100) NOT NULL, SourceEntityId UNIQUEIDENTIFIER NOT NULL, TransactionDate DATE NOT NULL, GrossAmount DECIMAL(18,2) NOT NULL DEFAULT 0, CommissionRate DECIMAL(9,4) NOT NULL DEFAULT 0, CommissionAmount DECIMAL(18,2) NOT NULL DEFAULT 0, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending', PayoutId UNIQUEIDENTIFIER NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'TransactionId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD TransactionId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'TenantId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionTx_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'SourceEntityName') IS NULL ALTER TABLE Commission.CommissionTransaction ADD SourceEntityName NVARCHAR(100) NOT NULL CONSTRAINT DF_CommissionTx_SourceEntityName DEFAULT N'Policy';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'SourceEntityId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD SourceEntityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionTx_SourceEntityId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'TransactionDate') IS NULL ALTER TABLE Commission.CommissionTransaction ADD TransactionDate DATE NOT NULL CONSTRAINT DF_CommissionTx_Date DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'GrossAmount') IS NULL ALTER TABLE Commission.CommissionTransaction ADD GrossAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionTx_Gross DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionRate') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionRate DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionTx_Rate DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionAmount') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionTx_Amount DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionTransaction ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionTx_Status DEFAULT N'Pending';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'PayoutId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD PayoutId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionTx_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionTransaction ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionTx_IsDeleted DEFAULT 0;
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionTransaction WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        DECLARE @Plan UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        IF @PayeeId IS NOT NULL AND @Plan IS NOT NULL
        BEGIN
            INSERT INTO Commission.CommissionTransaction (TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, CreatedDateUtc, IsDeleted)
            VALUES
            (NEWID(), @SeedTenantId, @PayeeId, @Plan, N''Policy POL-2025-00182'', NEWID(), DATEADD(day, -20, CONVERT(date, SYSUTCDATETIME())), 45000, 10, 4500, N''Earned'', SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @PayeeId, @Plan, N''Policy POL-2025-00211'', NEWID(), DATEADD(day, -12, CONVERT(date, SYSUTCDATETIME())), 31000, 10, 3100, N''Earned'', SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @PayeeId, @Plan, N''Renewal POL-2024-09912'', NEWID(), DATEADD(day, -5, CONVERT(date, SYSUTCDATETIME())), 28000, 8, 2240, N''Earned'', SYSUTCDATETIME(), 0);
        END
    END', N'@SeedTenantId UNIQUEIDENTIFIER', @SeedTenantId = @TenantId;
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
