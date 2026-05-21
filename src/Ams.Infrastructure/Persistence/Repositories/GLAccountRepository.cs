using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class GLAccountRepository : IGLAccountRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public GLAccountRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Finance') EXEC(N'CREATE SCHEMA Finance');

IF OBJECT_ID(N'Finance.GLAccount', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.GLAccount
    (
        GLAccountId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId         UNIQUEIDENTIFIER NOT NULL,
        AccountCode      NVARCHAR(50)     NOT NULL,
        AccountName      NVARCHAR(200)    NOT NULL,
        AccountTypeCode  NVARCHAR(50)     NOT NULL,
        Description      NVARCHAR(500)    NULL,
        ParentGLAccountId UNIQUEIDENTIFIER NULL,
        IsActive         BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId  UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc  DATETIME2        NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted        BIT              NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Finance.GLAccount', N'TenantId') IS NULL ALTER TABLE Finance.GLAccount ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_GLAccount_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Finance.GLAccount', N'AccountCode') IS NULL ALTER TABLE Finance.GLAccount ADD AccountCode NVARCHAR(50) NOT NULL CONSTRAINT DF_GLAccount_AccountCode DEFAULT N'0000';
    IF COL_LENGTH(N'Finance.GLAccount', N'AccountName') IS NULL ALTER TABLE Finance.GLAccount ADD AccountName NVARCHAR(200) NOT NULL CONSTRAINT DF_GLAccount_AccountName DEFAULT N'GL Account';
    IF COL_LENGTH(N'Finance.GLAccount', N'AccountTypeCode') IS NULL ALTER TABLE Finance.GLAccount ADD AccountTypeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_GLAccount_AccountTypeCode DEFAULT N'Asset';
    IF COL_LENGTH(N'Finance.GLAccount', N'Description') IS NULL ALTER TABLE Finance.GLAccount ADD Description NVARCHAR(500) NULL;
    IF COL_LENGTH(N'Finance.GLAccount', N'ParentGLAccountId') IS NULL ALTER TABLE Finance.GLAccount ADD ParentGLAccountId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.GLAccount', N'IsActive') IS NULL ALTER TABLE Finance.GLAccount ADD IsActive BIT NOT NULL CONSTRAINT DF_GLAccount_IsActive DEFAULT 1;
    IF COL_LENGTH(N'Finance.GLAccount', N'CreatedDateUtc') IS NULL ALTER TABLE Finance.GLAccount ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_GLAccount_CreatedDateUtc DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Finance.GLAccount', N'CreatedByUserId') IS NULL ALTER TABLE Finance.GLAccount ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.GLAccount', N'ModifiedDateUtc') IS NULL ALTER TABLE Finance.GLAccount ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Finance.GLAccount', N'ModifiedByUserId') IS NULL ALTER TABLE Finance.GLAccount ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.GLAccount', N'IsDeleted') IS NULL ALTER TABLE Finance.GLAccount ADD IsDeleted BIT NOT NULL CONSTRAINT DF_GLAccount_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Finance.GLAccount') AND name = N'IX_GLAccount_Tenant_Code')
    CREATE INDEX IX_GLAccount_Tenant_Code ON Finance.GLAccount(TenantId, AccountCode, IsDeleted);

IF @TenantId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Finance.GLAccount WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Finance.GLAccount (GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, ParentGLAccountId, IsActive, CreatedDateUtc, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'1000', N'Cash and Cash Equivalents', N'Asset', N'Operating cash and bank balances synchronized for billing and receipts.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'1100', N'Accounts Receivable', N'Asset', N'Customer invoice receivables from billing workflows.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'1200', N'Prepaid Expenses', N'Asset', N'Prepaid policy, software, and service costs.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'2000', N'Accounts Payable', N'Liability', N'Open vendor and carrier payables.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'2100', N'Unearned Revenue', N'Liability', N'Deferred client billing and advance payments.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'3000', N'Owner Equity', N'Equity', N'Agency owner equity and retained earnings.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'4000', N'Commission Revenue', N'Revenue', N'Insurance commission revenue from bound policies.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'4100', N'Fee Revenue', N'Revenue', N'Broker fee and service fee revenue.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'5000', N'Producer Compensation', N'Expense', N'Producer commission and payroll expense.', NULL, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'5100', N'Operating Expenses', N'Expense', N'General agency operating expense.', NULL, 1, SYSUTCDATETIME(), 0);
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<GLAccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
SELECT GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, ParentGLAccountId, IsActive, CreatedDateUtc
FROM Finance.GLAccount
WHERE GLAccountId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<GLAccountDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<GLAccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var selectColumns = "GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, ParentGLAccountId, IsActive, CreatedDateUtc";
        var searchPredicate = "AccountName LIKE '%' + @SearchTerm + '%' OR AccountCode LIKE '%' + @SearchTerm + '%' OR AccountTypeCode LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.GLAccount", selectColumns, searchPredicate, "AccountCode ASC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<GLAccountDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<GLAccountDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateGLAccountRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.GLAccount (GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, ParentGLAccountId, IsActive, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountCode, @AccountName, @AccountTypeCode, @Description, @ParentGLAccountId, @IsActive, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountCode, request.AccountName, request.AccountTypeCode, request.Description, request.ParentGLAccountId, request.IsActive, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateGLAccountRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
UPDATE Finance.GLAccount
SET AccountCode = @AccountCode,
    AccountName = @AccountName,
    AccountTypeCode = @AccountTypeCode,
    Description = @Description,
    ParentGLAccountId = @ParentGLAccountId,
    IsActive = @IsActive,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE GLAccountId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountCode, request.AccountName, request.AccountTypeCode, request.Description, request.ParentGLAccountId, request.IsActive, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}