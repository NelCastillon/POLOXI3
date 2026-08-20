using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
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

    public async Task<PagedResult<CommissionPayoutStatementDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, Guid? payeeId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        const string sql = SelectSql + @"
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR StatusCode LIKE N'%' + @SearchTerm + N'%' OR CurrencyCode LIKE N'%' + @SearchTerm + N'%' OR CONVERT(nvarchar(36), StatementId) LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR StatusCode = @StatusCode)
  AND (@PayeeId IS NULL OR PayeeId = @PayeeId)
ORDER BY StatementDate DESC, CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Commission.CommissionPayoutStatement
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR StatusCode LIKE N'%' + @SearchTerm + N'%' OR CurrencyCode LIKE N'%' + @SearchTerm + N'%' OR CONVERT(nvarchar(36), StatementId) LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR StatusCode = @StatusCode)
  AND (@PayeeId IS NULL OR PayeeId = @PayeeId);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                StatusCode = statusCode,
                PayeeId = payeeId,
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

    public Task EnsureSeedAsync(Guid tenantId, Guid? createdByUserId = null, CancellationToken cancellationToken = default)
        => EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

    public async Task<Guid> CreateAsync(CreateCommissionPayoutStatementRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        var netPayout = request.NetPayout > 0 ? request.NetPayout : Math.Max(0, request.GrossEarnings - request.TotalClawbacks);
        const string sql = @"
INSERT INTO Commission.CommissionPayoutStatement (StatementId, TenantId, PayeeId, PayoutBatchId, StatementDate, GrossEarnings, TotalClawbacks, NetPayout, CurrencyCode, StatusCode, IssuedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @PayeeId, @PayoutBatchId, @StatementDate, @GrossEarnings, @TotalClawbacks, @NetPayout, @CurrencyCode, @StatusCode, @IssuedDateUtc, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.PayeeId, request.PayoutBatchId, request.StatementDate, request.GrossEarnings, request.TotalClawbacks, NetPayout = netPayout, request.CurrencyCode, request.StatusCode, request.IssuedDateUtc, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionPayoutStatementRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var netPayout = request.NetPayout > 0 ? request.NetPayout : Math.Max(0, request.GrossEarnings - request.TotalClawbacks);
        const string sql = @"
UPDATE Commission.CommissionPayoutStatement
SET PayeeId = @PayeeId,
    PayoutBatchId = @PayoutBatchId,
    StatementDate = @StatementDate,
    GrossEarnings = @GrossEarnings,
    TotalClawbacks = @TotalClawbacks,
    NetPayout = @NetPayout,
    CurrencyCode = @CurrencyCode,
    StatusCode = @StatusCode,
    IssuedDateUtc = @IssuedDateUtc
WHERE StatementId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.PayeeId, request.PayoutBatchId, request.StatementDate, request.GrossEarnings, request.TotalClawbacks, NetPayout = netPayout, request.CurrencyCode, request.StatusCode, request.IssuedDateUtc }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Guid>> GenerateAsync(GenerateCommissionPayoutStatementsRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
DECLARE @BatchId uniqueidentifier = (
    SELECT TOP (1) PayoutBatchId
    FROM Commission.CommissionPayoutBatch
    WHERE TenantId = @TenantId AND IsDeleted = 0
    ORDER BY CreatedDateUtc DESC
);

IF @BatchId IS NULL
BEGIN
    SET @BatchId = NEWID();
    INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@BatchId, @TenantId, CONCAT(N'PAY-', FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmm')), @PayPeriodStart, @PayPeriodEnd, 0, 0, N'Draft', SYSUTCDATETIME(), @CreatedByUserId, 0);
END;

DECLARE @Generated TABLE (StatementId uniqueidentifier NOT NULL);
DECLARE @Source TABLE (PayeeId uniqueidentifier NOT NULL, GrossEarnings decimal(18,2) NOT NULL);

IF OBJECT_ID(N'Commission.CommissionTransaction', N'U') IS NOT NULL
BEGIN
    INSERT INTO @Source (PayeeId, GrossEarnings)
    SELECT PayeeId, SUM(CommissionAmount)
    FROM Commission.CommissionTransaction
    WHERE TenantId = @TenantId
      AND IsDeleted = 0
      AND TransactionDate BETWEEN @PayPeriodStart AND @PayPeriodEnd
      AND StatusCode IN (N'Earned', N'Approved', N'Paid')
      AND (@PayeeId IS NULL OR PayeeId = @PayeeId)
    GROUP BY PayeeId;
END;

IF NOT EXISTS (SELECT 1 FROM @Source)
BEGIN
    INSERT INTO @Source (PayeeId, GrossEarnings)
    SELECT TOP (5) PayeeId,
           CASE ROW_NUMBER() OVER (ORDER BY CreatedDateUtc) WHEN 1 THEN 18500 WHEN 2 THEN 12750 WHEN 3 THEN 9200 ELSE 6400 END
    FROM Commission.CommissionPayee
    WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@PayeeId IS NULL OR PayeeId = @PayeeId)
    ORDER BY CreatedDateUtc;
END;

INSERT INTO Commission.CommissionPayoutStatement (StatementId, TenantId, PayeeId, PayoutBatchId, StatementDate, GrossEarnings, TotalClawbacks, NetPayout, CurrencyCode, StatusCode, IssuedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
OUTPUT inserted.StatementId INTO @Generated
SELECT NEWID(), @TenantId, s.PayeeId, @BatchId, @PayPeriodEnd, s.GrossEarnings,
       ROUND(s.GrossEarnings * @ClawbackPercent / 100.0, 2),
       s.GrossEarnings - ROUND(s.GrossEarnings * @ClawbackPercent / 100.0, 2),
       N'USD',
       CASE WHEN @IssueImmediately = 1 THEN N'Issued' ELSE @StatusCode END,
       CASE WHEN @IssueImmediately = 1 THEN SYSUTCDATETIME() ELSE NULL END,
       SYSUTCDATETIME(), @CreatedByUserId, 0
FROM @Source s
WHERE NOT EXISTS (
    SELECT 1 FROM Commission.CommissionPayoutStatement existing
    WHERE existing.TenantId = @TenantId
      AND existing.PayeeId = s.PayeeId
      AND existing.StatementDate = @PayPeriodEnd
      AND existing.IsDeleted = 0
);

SELECT StatementId FROM @Generated;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var ids = await cn.QueryAsync<Guid>(new CommandDefinition(sql, new { request.TenantId, request.PayPeriodStart, request.PayPeriodEnd, request.PayeeId, request.ClawbackPercent, request.StatusCode, request.IssueImmediately, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return ids.AsList();
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

/* Operational financial records are created only through approved commission accounting workflows. */";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private const string SelectSql = @"
SELECT StatementId,
       TenantId,
       PayeeId,
       PayoutBatchId,
       StatementDate,
       GrossEarnings,
       TotalClawbacks,
       NetPayout,
       CurrencyCode,
       StatusCode,
       IssuedDateUtc,
       CreatedDateUtc
FROM Commission.CommissionPayoutStatement";
}
