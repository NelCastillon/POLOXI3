using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPayoutStatementRepository : ICommissionPayoutStatementRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionPayoutStatementRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionPayoutStatementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = @"SELECT StatementId, TenantId, PayeeId, PayoutBatchId, StatementDate, GrossEarnings, TotalClawbacks, NetPayout, CurrencyCode, StatusCode, IssuedDateUtc, CreatedDateUtc FROM Commission.CommissionPayoutStatement WHERE StatementId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPayoutStatementDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPayoutStatementDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql(
            "Commission.CommissionPayoutStatement",
            "StatementId, TenantId, PayeeId, PayoutBatchId, StatementDate, GrossEarnings, TotalClawbacks, NetPayout, CurrencyCode, StatusCode, IssuedDateUtc, CreatedDateUtc",
            "StatusCode LIKE '%' + @SearchTerm + '%'",
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

        var items = (await multi.ReadAsync<CommissionPayoutStatementDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionPayoutStatementDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
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
    IF COL_LENGTH(N'Commission.CommissionPayee', N'TenantId') IS NULL ALTER TABLE Commission.CommissionPayee ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CpsPayee_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'UserId') IS NULL ALTER TABLE Commission.CommissionPayee ADD UserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionPayee ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeTypeCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeTypeCode NVARCHAR(50) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'SplitPercentage') IS NULL ALTER TABLE Commission.CommissionPayee ADD SplitPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_CpsPayee_Split DEFAULT 100;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'EffectiveDate') IS NULL ALTER TABLE Commission.CommissionPayee ADD EffectiveDate DATE NOT NULL CONSTRAINT DF_CpsPayee_Effective DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionPayee', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CpsPayee_Status DEFAULT N'Active';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayee ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CpsPayee_IsDeleted DEFAULT 0;
END;

IF OBJECT_ID(N'Commission.CommissionPayoutBatch', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPayoutBatch (PayoutBatchId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, BatchReference NVARCHAR(80) NOT NULL, PayPeriodStart DATE NOT NULL, PayPeriodEnd DATE NOT NULL, TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0, PayoutCount INT NOT NULL DEFAULT 0, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft', ProcessedByUserId UNIQUEIDENTIFIER NULL, ProcessedDateUtc DATETIME2 NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'PayoutBatchId') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD PayoutBatchId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'TenantId') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CpsBatch_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'BatchReference') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD BatchReference NVARCHAR(80) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'PayPeriodStart') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD PayPeriodStart DATE NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'PayPeriodEnd') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD PayPeriodEnd DATE NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'PeriodStartDate') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD PeriodStartDate DATE NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'PeriodEndDate') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD PeriodEndDate DATE NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'TotalAmount') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CpsBatch_Total DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'PayoutCount') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD PayoutCount INT NOT NULL CONSTRAINT DF_CpsBatch_Count DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'CurrencyCode') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD CurrencyCode NVARCHAR(3) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CpsBatch_Status DEFAULT N'Draft';
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'StatusCodeId') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD StatusCodeId INT NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'ProcessedByUserId') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD ProcessedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'ProcessedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD ProcessedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'CreatedByUserId') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'ModifiedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'ModifiedByUserId') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayoutBatch ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CpsBatch_IsDeleted DEFAULT 0;
END;

IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'BatchId') IS NOT NULL
BEGIN
    EXEC(N'UPDATE Commission.CommissionPayoutBatch SET PayoutBatchId = BatchId WHERE PayoutBatchId IS NULL');
END;
IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'BatchNumber') IS NOT NULL
BEGIN
    EXEC(N'UPDATE Commission.CommissionPayoutBatch SET BatchReference = BatchNumber WHERE BatchReference IS NULL');
END;
IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'BatchDate') IS NOT NULL
BEGIN
    EXEC(N'UPDATE Commission.CommissionPayoutBatch SET PayPeriodStart = CONVERT(date, BatchDate), PayPeriodEnd = CONVERT(date, BatchDate) WHERE PayPeriodStart IS NULL OR PayPeriodEnd IS NULL');
END;
IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'PayeeCount') IS NOT NULL
BEGIN
    EXEC(N'UPDATE Commission.CommissionPayoutBatch SET PayoutCount = PayeeCount WHERE PayoutCount = 0');
END;
EXEC(N'UPDATE Commission.CommissionPayoutBatch SET PayoutBatchId = NEWID() WHERE PayoutBatchId IS NULL');
EXEC(N'UPDATE Commission.CommissionPayoutBatch SET BatchReference = CONCAT(N''PAY-'', LEFT(CONVERT(nvarchar(36), PayoutBatchId), 8)) WHERE BatchReference IS NULL');
EXEC(N'UPDATE Commission.CommissionPayoutBatch SET PayPeriodStart = CONVERT(date, SYSUTCDATETIME()) WHERE PayPeriodStart IS NULL');
EXEC(N'UPDATE Commission.CommissionPayoutBatch SET PayPeriodEnd = PayPeriodStart WHERE PayPeriodEnd IS NULL');
EXEC(N'UPDATE Commission.CommissionPayoutBatch SET PeriodStartDate = PayPeriodStart WHERE PeriodStartDate IS NULL');
EXEC(N'UPDATE Commission.CommissionPayoutBatch SET PeriodEndDate = PayPeriodEnd WHERE PeriodEndDate IS NULL');
EXEC(N'UPDATE Commission.CommissionPayoutBatch SET CurrencyCode = N''USD'' WHERE CurrencyCode IS NULL');
EXEC(N'UPDATE Commission.CommissionPayoutBatch SET StatusCodeId = CASE WHEN StatusCode = N''Processed'' THEN 2 WHEN StatusCode = N''Paid'' THEN 3 ELSE 1 END WHERE StatusCodeId IS NULL');

IF OBJECT_ID(N'Commission.CommissionPayoutStatement', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPayoutStatement (StatementId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, PayoutBatchId UNIQUEIDENTIFIER NULL, StatementDate DATE NOT NULL, GrossEarnings DECIMAL(18,2) NOT NULL DEFAULT 0, TotalClawbacks DECIMAL(18,2) NOT NULL DEFAULT 0, NetPayout DECIMAL(18,2) NOT NULL DEFAULT 0, CurrencyCode NVARCHAR(3) NOT NULL DEFAULT N'USD', StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft', IssuedDateUtc DATETIME2 NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'TenantId') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CpsStatement_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'PayoutBatchId') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD PayoutBatchId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'StatementDate') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD StatementDate DATE NOT NULL CONSTRAINT DF_CpsStatement_Date DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'GrossEarnings') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD GrossEarnings DECIMAL(18,2) NOT NULL CONSTRAINT DF_CpsStatement_Gross DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'TotalClawbacks') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD TotalClawbacks DECIMAL(18,2) NOT NULL CONSTRAINT DF_CpsStatement_Clawbacks DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'NetPayout') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD NetPayout DECIMAL(18,2) NOT NULL CONSTRAINT DF_CpsStatement_Net DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'CurrencyCode') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_CpsStatement_Currency DEFAULT N'USD';
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CpsStatement_Status DEFAULT N'Draft';
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'IssuedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD IssuedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CpsStatement_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'CreatedByUserId') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayoutStatement', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayoutStatement ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CpsStatement_IsDeleted DEFAULT 0;
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPayoutBatch WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @BatchNumber NVARCHAR(80) = CONCAT(N''PAY-'', FORMAT(SYSUTCDATETIME(), ''yyyyMM''), N''-001'');
        IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''BatchNumber'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''PeriodStartDate'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''PeriodEndDate'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''StatusCodeId'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''CurrencyCode'') IS NOT NULL
        BEGIN
            INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchNumber, BatchReference, PayPeriodStart, PayPeriodEnd, PeriodStartDate, PeriodEndDate, TotalAmount, PayoutCount, CurrencyCode, StatusCode, StatusCodeId, CreatedDateUtc, IsDeleted)
            VALUES (NEWID(), @SeedTenantId, @BatchNumber, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''USD'', N''Draft'', 1, SYSUTCDATETIME(), 0);
        END
        ELSE IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''BatchNumber'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''PeriodStartDate'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''PeriodEndDate'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''CurrencyCode'') IS NOT NULL
        BEGIN
            INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchNumber, BatchReference, PayPeriodStart, PayPeriodEnd, PeriodStartDate, PeriodEndDate, TotalAmount, PayoutCount, CurrencyCode, StatusCode, CreatedDateUtc, IsDeleted)
            VALUES (NEWID(), @SeedTenantId, @BatchNumber, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''USD'', N''Draft'', SYSUTCDATETIME(), 0);
        END
        ELSE IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''BatchNumber'') IS NOT NULL AND COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''StatusCodeId'') IS NOT NULL
        BEGIN
            IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''CurrencyCode'') IS NOT NULL
                INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchNumber, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, CurrencyCode, StatusCode, StatusCodeId, CreatedDateUtc, IsDeleted)
                VALUES (NEWID(), @SeedTenantId, @BatchNumber, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''USD'', N''Draft'', 1, SYSUTCDATETIME(), 0);
            ELSE
                INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchNumber, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, StatusCodeId, CreatedDateUtc, IsDeleted)
                VALUES (NEWID(), @SeedTenantId, @BatchNumber, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''Draft'', 1, SYSUTCDATETIME(), 0);
        END
        ELSE IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''StatusCodeId'') IS NOT NULL
        BEGIN
            IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''CurrencyCode'') IS NOT NULL
                INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, CurrencyCode, StatusCode, StatusCodeId, CreatedDateUtc, IsDeleted)
                VALUES (NEWID(), @SeedTenantId, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''USD'', N''Draft'', 1, SYSUTCDATETIME(), 0);
            ELSE
                INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, StatusCodeId, CreatedDateUtc, IsDeleted)
                VALUES (NEWID(), @SeedTenantId, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''Draft'', 1, SYSUTCDATETIME(), 0);
        END
        ELSE IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''BatchNumber'') IS NOT NULL
        BEGIN
            IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''CurrencyCode'') IS NOT NULL
                INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchNumber, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, CurrencyCode, StatusCode, CreatedDateUtc, IsDeleted)
                VALUES (NEWID(), @SeedTenantId, @BatchNumber, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''USD'', N''Draft'', SYSUTCDATETIME(), 0);
            ELSE
                INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchNumber, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, CreatedDateUtc, IsDeleted)
                VALUES (NEWID(), @SeedTenantId, @BatchNumber, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''Draft'', SYSUTCDATETIME(), 0);
        END
        ELSE
        BEGIN
            IF COL_LENGTH(N''Commission.CommissionPayoutBatch'', N''CurrencyCode'') IS NOT NULL
                INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, CurrencyCode, StatusCode, CreatedDateUtc, IsDeleted)
                VALUES (NEWID(), @SeedTenantId, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''USD'', N''Draft'', SYSUTCDATETIME(), 0);
            ELSE
                INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, CreatedDateUtc, IsDeleted)
                VALUES (NEWID(), @SeedTenantId, @BatchNumber, DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N''Draft'', SYSUTCDATETIME(), 0);
        END
    END;

    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPayoutStatement WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        DECLARE @BatchId UNIQUEIDENTIFIER = (SELECT TOP 1 PayoutBatchId FROM Commission.CommissionPayoutBatch WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        IF @PayeeId IS NOT NULL AND @BatchId IS NOT NULL
        BEGIN
            INSERT INTO Commission.CommissionPayoutStatement (StatementId, TenantId, PayeeId, PayoutBatchId, StatementDate, GrossEarnings, TotalClawbacks, NetPayout, CurrencyCode, StatusCode, IssuedDateUtc, CreatedDateUtc, IsDeleted)
            VALUES
            (NEWID(), @SeedTenantId, @PayeeId, @BatchId, CONVERT(date, SYSUTCDATETIME()), 18500, 620, 17880, N''USD'', N''Pending'', NULL, SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @PayeeId, @BatchId, DATEADD(month, -1, CONVERT(date, SYSUTCDATETIME())), 24250, 350, 23900, N''USD'', N''Issued'', DATEADD(day, -25, SYSUTCDATETIME()), SYSUTCDATETIME(), 0);
        END
    END;', N'@SeedTenantId UNIQUEIDENTIFIER', @SeedTenantId = @TenantId;
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
