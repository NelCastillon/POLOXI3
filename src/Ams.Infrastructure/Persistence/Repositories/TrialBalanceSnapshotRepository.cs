using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TrialBalanceSnapshotRepository : ITrialBalanceSnapshotRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TrialBalanceSnapshotRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Finance') EXEC(N'CREATE SCHEMA Finance');

IF OBJECT_ID(N'Finance.GLAccount', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.GLAccount
    (
        GLAccountId uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        AccountCode nvarchar(50) NOT NULL,
        AccountName nvarchar(200) NOT NULL,
        AccountTypeCode nvarchar(50) NOT NULL,
        Description nvarchar(500) NULL,
        ParentGLAccountId uniqueidentifier NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedDateUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2 NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL DEFAULT 0
    );
END;

IF COL_LENGTH(N'Finance.GLAccount', N'TenantId') IS NULL ALTER TABLE Finance.GLAccount ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.GLAccount', N'AccountCode') IS NULL ALTER TABLE Finance.GLAccount ADD AccountCode nvarchar(50) NULL;
IF COL_LENGTH(N'Finance.GLAccount', N'AccountName') IS NULL ALTER TABLE Finance.GLAccount ADD AccountName nvarchar(200) NULL;
IF COL_LENGTH(N'Finance.GLAccount', N'AccountTypeCode') IS NULL ALTER TABLE Finance.GLAccount ADD AccountTypeCode nvarchar(50) NULL;
IF COL_LENGTH(N'Finance.GLAccount', N'IsActive') IS NULL ALTER TABLE Finance.GLAccount ADD IsActive bit NULL;
IF COL_LENGTH(N'Finance.GLAccount', N'CreatedDateUtc') IS NULL ALTER TABLE Finance.GLAccount ADD CreatedDateUtc datetime2 NULL;
IF COL_LENGTH(N'Finance.GLAccount', N'IsDeleted') IS NULL ALTER TABLE Finance.GLAccount ADD IsDeleted bit NULL;

UPDATE Finance.GLAccount SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.GLAccount SET AccountCode = COALESCE(NULLIF(AccountCode, ''), '0000') WHERE AccountCode IS NULL OR AccountCode = '';
UPDATE Finance.GLAccount SET AccountName = COALESCE(NULLIF(AccountName, ''), 'GL Account') WHERE AccountName IS NULL OR AccountName = '';
UPDATE Finance.GLAccount SET AccountTypeCode = COALESCE(NULLIF(AccountTypeCode, ''), 'Asset') WHERE AccountTypeCode IS NULL OR AccountTypeCode = '';
UPDATE Finance.GLAccount SET IsActive = COALESCE(IsActive, 1) WHERE IsActive IS NULL;
UPDATE Finance.GLAccount SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.GLAccount SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF @TenantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Finance.GLAccount WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
BEGIN
    INSERT INTO Finance.GLAccount (GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, ParentGLAccountId, IsActive, CreatedDateUtc, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'1000', N'Cash and Cash Equivalents', N'Asset', N'Operating cash and bank balances.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'1100', N'Accounts Receivable', N'Asset', N'Customer invoice receivables.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'2000', N'Accounts Payable', N'Liability', N'Open vendor and carrier payables.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'2100', N'Unearned Revenue', N'Liability', N'Deferred billing and advance payments.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'3000', N'Owner Equity', N'Equity', N'Agency owner equity.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'4000', N'Commission Revenue', N'Revenue', N'Insurance commission revenue.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'5000', N'Operating Expenses', N'Expense', N'General agency operating expense.', NULL, 1, SYSUTCDATETIME(), 0);
END;

IF OBJECT_ID(N'Finance.TrialBalanceSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.TrialBalanceSnapshot
    (
        TrialBalanceSnapshotId uniqueidentifier NOT NULL CONSTRAINT PK_TrialBalanceSnapshot PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        SnapshotDate date NOT NULL,
        AccountingPeriodId uniqueidentifier NULL,
        GLAccountId uniqueidentifier NOT NULL,
        AccountCode nvarchar(50) NOT NULL,
        AccountName nvarchar(200) NOT NULL,
        DebitBalance decimal(18,2) NOT NULL CONSTRAINT DF_TrialBalanceSnapshot_Debit DEFAULT (0),
        CreditBalance decimal(18,2) NOT NULL CONSTRAINT DF_TrialBalanceSnapshot_Credit DEFAULT (0),
        NetBalance decimal(18,2) NOT NULL CONSTRAINT DF_TrialBalanceSnapshot_Net DEFAULT (0),
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_TrialBalanceSnapshot_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_TrialBalanceSnapshot_IsDeleted DEFAULT (0)
    );
END;

IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'TenantId') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'SnapshotDate') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD SnapshotDate date NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'AccountingPeriodId') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD AccountingPeriodId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'GLAccountId') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD GLAccountId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'AccountCode') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD AccountCode nvarchar(50) NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'AccountName') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD AccountName nvarchar(200) NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'DebitBalance') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD DebitBalance decimal(18,2) NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'CreditBalance') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD CreditBalance decimal(18,2) NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'NetBalance') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD NetBalance decimal(18,2) NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'CreatedDateUtc') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'CreatedByUserId') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'ModifiedDateUtc') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'ModifiedByUserId') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD ModifiedByUserId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.TrialBalanceSnapshot', N'IsDeleted') IS NULL ALTER TABLE Finance.TrialBalanceSnapshot ADD IsDeleted bit NULL;

UPDATE Finance.TrialBalanceSnapshot SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.TrialBalanceSnapshot SET SnapshotDate = COALESCE(SnapshotDate, CAST(SYSUTCDATETIME() AS date)) WHERE SnapshotDate IS NULL;
UPDATE Finance.TrialBalanceSnapshot SET DebitBalance = COALESCE(DebitBalance, 0) WHERE DebitBalance IS NULL;
UPDATE Finance.TrialBalanceSnapshot SET CreditBalance = COALESCE(CreditBalance, 0) WHERE CreditBalance IS NULL;
UPDATE Finance.TrialBalanceSnapshot SET NetBalance = COALESCE(NetBalance, DebitBalance - CreditBalance, 0) WHERE NetBalance IS NULL;
UPDATE Finance.TrialBalanceSnapshot SET AccountCode = COALESCE(NULLIF(AccountCode, ''), '0000') WHERE AccountCode IS NULL OR AccountCode = '';
UPDATE Finance.TrialBalanceSnapshot SET AccountName = COALESCE(NULLIF(AccountName, ''), 'Trial Balance Account') WHERE AccountName IS NULL OR AccountName = '';
UPDATE Finance.TrialBalanceSnapshot SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.TrialBalanceSnapshot SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TrialBalanceSnapshot_Tenant_Date' AND object_id = OBJECT_ID(N'Finance.TrialBalanceSnapshot'))
    CREATE INDEX IX_TrialBalanceSnapshot_Tenant_Date ON Finance.TrialBalanceSnapshot (TenantId, SnapshotDate DESC, AccountCode) INCLUDE (AccountName, DebitBalance, CreditBalance, NetBalance);
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return;
        }

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Finance.TrialBalanceSnapshot WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
BEGIN
    INSERT INTO Finance.TrialBalanceSnapshot (TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, CAST(SYSUTCDATETIME() AS date), NULL, GLAccountId, AccountCode, AccountName,
           CASE AccountTypeCode WHEN 'Asset' THEN CASE AccountCode WHEN '1000' THEN 82500 WHEN '1100' THEN 41250 ELSE 18500 END WHEN 'Expense' THEN 27500 ELSE 0 END,
           CASE AccountTypeCode WHEN 'Liability' THEN CASE AccountCode WHEN '2000' THEN 22000 ELSE 36500 END WHEN 'Equity' THEN 48000 WHEN 'Revenue' THEN 63250 ELSE 0 END,
           CASE AccountTypeCode WHEN 'Asset' THEN CASE AccountCode WHEN '1000' THEN 82500 WHEN '1100' THEN 41250 ELSE 18500 END WHEN 'Expense' THEN 27500 ELSE 0 END -
           CASE AccountTypeCode WHEN 'Liability' THEN CASE AccountCode WHEN '2000' THEN 22000 ELSE 36500 END WHEN 'Equity' THEN 48000 WHEN 'Revenue' THEN 63250 ELSE 0 END,
           SYSUTCDATETIME(), NULL, NULL, NULL, 0
    FROM Finance.GLAccount
    WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0;
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<TrialBalanceSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = "SELECT TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc FROM Finance.TrialBalanceSnapshot WHERE TrialBalanceSnapshotId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TrialBalanceSnapshotDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TrialBalanceSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc";
        var searchPredicate = "AccountCode LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%'";
        var searchSql = RepositorySql.BuildPagedSearchSql("Finance.TrialBalanceSnapshot", selectColumns, searchPredicate, "AccountCode ASC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TrialBalanceSnapshotDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TrialBalanceSnapshotDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateTrialBalanceSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        var netBalance = request.DebitBalance - request.CreditBalance;
        const string sql = @"
INSERT INTO Finance.TrialBalanceSnapshot (TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @SnapshotDate, @AccountingPeriodId, @GLAccountId, @AccountCode, @AccountName, @DebitBalance, @CreditBalance, @NetBalance, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.SnapshotDate, request.AccountingPeriodId, request.GLAccountId, request.AccountCode, request.AccountName, request.DebitBalance, request.CreditBalance, NetBalance = netBalance, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateTrialBalanceSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var netBalance = request.DebitBalance - request.CreditBalance;
        const string sql = @"
UPDATE Finance.TrialBalanceSnapshot
SET SnapshotDate = @SnapshotDate,
    AccountingPeriodId = @AccountingPeriodId,
    GLAccountId = @GLAccountId,
    AccountCode = @AccountCode,
    AccountName = @AccountName,
    DebitBalance = @DebitBalance,
    CreditBalance = @CreditBalance,
    NetBalance = @NetBalance,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TrialBalanceSnapshotId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.SnapshotDate, request.AccountingPeriodId, request.GLAccountId, request.AccountCode, request.AccountName, request.DebitBalance, request.CreditBalance, NetBalance = netBalance, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
