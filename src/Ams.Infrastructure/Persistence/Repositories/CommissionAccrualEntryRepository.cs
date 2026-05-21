using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionAccrualEntryRepository : ICommissionAccrualEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionAccrualEntryRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionAccrualEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = SelectSql + @"
WHERE AccrualEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionAccrualEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionAccrualEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        const string sql = SelectSql + @"
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR StatusCode LIKE N'%' + @SearchTerm + N'%' OR CONVERT(nvarchar(36), AccrualEntryId) LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR StatusCode = @StatusCode)
ORDER BY AccrualDate DESC, CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Commission.CommissionAccrualEntry
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR StatusCode LIKE N'%' + @SearchTerm + N'%' OR CONVERT(nvarchar(36), AccrualEntryId) LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR StatusCode = @StatusCode);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            StatusCode = statusCode,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<CommissionAccrualEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionAccrualEntryDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionAccrualEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        var transactionId = await ResolveTransactionIdAsync(request.TenantId, request.TransactionId, cancellationToken);
        const string sql = @"
INSERT INTO Commission.CommissionAccrualEntry (AccrualEntryId, TenantId, TransactionId, GLAccountId, AccrualDate, AccruedAmount, ReversalDate, ReversedAmount, JournalEntryId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @TransactionId, @GLAccountId, @AccrualDate, @AccruedAmount, @ReversalDate, @ReversedAmount, @JournalEntryId, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, TransactionId = transactionId, request.GLAccountId, request.AccrualDate, request.AccruedAmount, request.ReversalDate, request.ReversedAmount, request.JournalEntryId, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionAccrualEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var transactionId = await ResolveTransactionIdAsync(request.TenantId, request.TransactionId, cancellationToken);
        const string sql = @"
UPDATE Commission.CommissionAccrualEntry
SET TransactionId = @TransactionId,
    GLAccountId = @GLAccountId,
    AccrualDate = @AccrualDate,
    AccruedAmount = @AccruedAmount,
    ReversalDate = @ReversalDate,
    ReversedAmount = @ReversedAmount,
    JournalEntryId = @JournalEntryId,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE AccrualEntryId = @Id AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, TransactionId = transactionId, request.GLAccountId, request.AccrualDate, request.AccruedAmount, request.ReversalDate, request.ReversedAmount, request.JournalEntryId, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

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

IF OBJECT_ID(N'Commission.CommissionTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionTransaction (TransactionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, CommissionPlanId UNIQUEIDENTIFIER NOT NULL, SourceEntityName NVARCHAR(100) NOT NULL, SourceEntityId UNIQUEIDENTIFIER NOT NULL, TransactionDate DATE NOT NULL, GrossAmount DECIMAL(18,2) NOT NULL DEFAULT 0, CommissionRate DECIMAL(9,4) NOT NULL DEFAULT 0, CommissionAmount DECIMAL(18,2) NOT NULL DEFAULT 0, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending', PayoutId UNIQUEIDENTIFIER NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Commission.CommissionAccrualEntry', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionAccrualEntry (AccrualEntryId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, TransactionId UNIQUEIDENTIFIER NOT NULL, GLAccountId UNIQUEIDENTIFIER NULL, AccrualDate DATE NOT NULL, AccruedAmount DECIMAL(18,2) NOT NULL, ReversalDate DATE NULL, ReversedAmount DECIMAL(18,2) NULL, JournalEntryId UNIQUEIDENTIFIER NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'AccrualEntryId') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD AccrualEntryId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'TenantId') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionAccrual_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'TransactionId') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD TransactionId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'GLAccountId') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD GLAccountId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'AccrualDate') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD AccrualDate DATE NOT NULL CONSTRAINT DF_CommissionAccrual_Date DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'AccruedAmount') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD AccruedAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionAccrual_Amount DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'ReversalDate') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD ReversalDate DATE NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'ReversedAmount') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD ReversedAmount DECIMAL(18,2) NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'JournalEntryId') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD JournalEntryId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionAccrual_Status DEFAULT N'Pending';
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionAccrual_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'CreatedByUserId') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'ModifiedDateUtc') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'ModifiedByUserId') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionAccrualEntry', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionAccrualEntry ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionAccrual_IsDeleted DEFAULT 0;
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionAccrualEntry WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @SeedTx UNIQUEIDENTIFIER = (SELECT TOP 1 TransactionId FROM Commission.CommissionTransaction WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        IF @SeedTx IS NOT NULL
        BEGIN
            INSERT INTO Commission.CommissionAccrualEntry (AccrualEntryId, TenantId, TransactionId, AccrualDate, AccruedAmount, ReversalDate, ReversedAmount, StatusCode, CreatedDateUtc, IsDeleted)
            VALUES
            (NEWID(), @SeedTenantId, @SeedTx, CONVERT(date, SYSUTCDATETIME()), 1200, DATEADD(month, 1, CONVERT(date, SYSUTCDATETIME())), 0, N''Posted'', SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @SeedTx, DATEADD(month, -1, CONVERT(date, SYSUTCDATETIME())), 1850, CONVERT(date, SYSUTCDATETIME()), 1850, N''Reversed'', SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @SeedTx, DATEADD(day, -7, CONVERT(date, SYSUTCDATETIME())), 2800, DATEADD(day, 21, CONVERT(date, SYSUTCDATETIME())), 0, N''Pending'', SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @SeedTx, DATEADD(day, -36, CONVERT(date, SYSUTCDATETIME())), 950, DATEADD(day, -6, CONVERT(date, SYSUTCDATETIME())), 0, N''Pending'', SYSUTCDATETIME(), 0),
            (NEWID(), @SeedTenantId, @SeedTx, DATEADD(day, -60, CONVERT(date, SYSUTCDATETIME())), 1625, DATEADD(day, -30, CONVERT(date, SYSUTCDATETIME())), 1625, N''Settled'', SYSUTCDATETIME(), 0);
        END
    END', N'@SeedTenantId UNIQUEIDENTIFIER', @SeedTenantId = @TenantId;
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private async Task<Guid> ResolveTransactionIdAsync(Guid tenantId, Guid? transactionId, CancellationToken cancellationToken)
    {
        if (transactionId.HasValue && transactionId.Value != Guid.Empty) return transactionId.Value;
        const string sql = "SELECT TOP 1 TransactionId FROM Commission.CommissionTransaction WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private const string SelectSql = @"
SELECT AccrualEntryId,
       TenantId,
       TransactionId,
       GLAccountId,
       AccrualDate,
       AccruedAmount,
       ReversalDate,
       ReversedAmount,
       JournalEntryId,
       StatusCode,
       CreatedDateUtc
FROM Commission.CommissionAccrualEntry";
}
