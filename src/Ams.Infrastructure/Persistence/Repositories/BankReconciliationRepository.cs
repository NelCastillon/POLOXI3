using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BankReconciliationRepository : IBankReconciliationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BankReconciliationRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Finance') EXEC(N'CREATE SCHEMA Finance');

IF OBJECT_ID(N'Finance.BankReconciliation', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.BankReconciliation
    (
        BankReconciliationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER NOT NULL,
        BankAccountNumber    NVARCHAR(50)     NOT NULL,
        BankName             NVARCHAR(150)    NOT NULL,
        BankStatementDate    DATE             NOT NULL,
        BankBalance          DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        BookBalance          DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        OutstandingDeposits  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        OutstandingChecks    DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        Discrepancy          DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        StatusCode           NVARCHAR(50)     NOT NULL DEFAULT N'Pending',
        CreatedDateUtc       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId      UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc      DATETIME2        NULL,
        ModifiedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted            BIT              NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Finance.BankReconciliation', N'TenantId') IS NULL ALTER TABLE Finance.BankReconciliation ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_BankRecon_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Finance.BankReconciliation', N'BankAccountNumber') IS NULL ALTER TABLE Finance.BankReconciliation ADD BankAccountNumber NVARCHAR(50) NOT NULL CONSTRAINT DF_BankRecon_BankAccountNumber DEFAULT N'0000';
    IF COL_LENGTH(N'Finance.BankReconciliation', N'BankName') IS NULL ALTER TABLE Finance.BankReconciliation ADD BankName NVARCHAR(150) NOT NULL CONSTRAINT DF_BankRecon_BankName DEFAULT N'Operating Bank';
    IF COL_LENGTH(N'Finance.BankReconciliation', N'BankStatementDate') IS NULL ALTER TABLE Finance.BankReconciliation ADD BankStatementDate DATE NOT NULL CONSTRAINT DF_BankRecon_BankStatementDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Finance.BankReconciliation', N'BankBalance') IS NULL ALTER TABLE Finance.BankReconciliation ADD BankBalance DECIMAL(18, 2) NOT NULL CONSTRAINT DF_BankRecon_BankBalance DEFAULT 0;
    IF COL_LENGTH(N'Finance.BankReconciliation', N'BookBalance') IS NULL ALTER TABLE Finance.BankReconciliation ADD BookBalance DECIMAL(18, 2) NOT NULL CONSTRAINT DF_BankRecon_BookBalance DEFAULT 0;
    IF COL_LENGTH(N'Finance.BankReconciliation', N'OutstandingDeposits') IS NULL ALTER TABLE Finance.BankReconciliation ADD OutstandingDeposits DECIMAL(18, 2) NOT NULL CONSTRAINT DF_BankRecon_OutstandingDeposits DEFAULT 0;
    IF COL_LENGTH(N'Finance.BankReconciliation', N'OutstandingChecks') IS NULL ALTER TABLE Finance.BankReconciliation ADD OutstandingChecks DECIMAL(18, 2) NOT NULL CONSTRAINT DF_BankRecon_OutstandingChecks DEFAULT 0;
    IF COL_LENGTH(N'Finance.BankReconciliation', N'Discrepancy') IS NULL ALTER TABLE Finance.BankReconciliation ADD Discrepancy DECIMAL(18, 2) NOT NULL CONSTRAINT DF_BankRecon_Discrepancy DEFAULT 0;
    IF COL_LENGTH(N'Finance.BankReconciliation', N'StatusCode') IS NULL ALTER TABLE Finance.BankReconciliation ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BankRecon_StatusCode DEFAULT N'Pending';
    IF COL_LENGTH(N'Finance.BankReconciliation', N'CreatedDateUtc') IS NULL ALTER TABLE Finance.BankReconciliation ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BankRecon_CreatedDateUtc DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Finance.BankReconciliation', N'CreatedByUserId') IS NULL ALTER TABLE Finance.BankReconciliation ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.BankReconciliation', N'ModifiedDateUtc') IS NULL ALTER TABLE Finance.BankReconciliation ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Finance.BankReconciliation', N'ModifiedByUserId') IS NULL ALTER TABLE Finance.BankReconciliation ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.BankReconciliation', N'IsDeleted') IS NULL ALTER TABLE Finance.BankReconciliation ADD IsDeleted BIT NOT NULL CONSTRAINT DF_BankRecon_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Finance.BankReconciliation') AND name = N'IX_BankRecon_Tenant_Date')
    CREATE INDEX IX_BankRecon_Tenant_Date ON Finance.BankReconciliation(TenantId, BankStatementDate DESC, IsDeleted);

IF @TenantId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Finance.BankReconciliation WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Finance.BankReconciliation (BankReconciliationId, TenantId, BankAccountNumber, BankName, BankStatementDate, BankBalance, BookBalance, OutstandingDeposits, OutstandingChecks, Discrepancy, StatusCode, CreatedDateUtc, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'OPER-1001', N'Operating Checking', DATEADD(day, -30, CONVERT(date, SYSUTCDATETIME())), 125240.50, 125240.50, 0.00, 0.00, 0.00, N'Reconciled', SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'TRUST-2001', N'Premium Trust Account', DATEADD(day, -15, CONVERT(date, SYSUTCDATETIME())), 84210.25, 83960.25, 250.00, 0.00, 250.00, N'Pending', SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'OPER-1001', N'Operating Checking', DATEADD(day, -5, CONVERT(date, SYSUTCDATETIME())), 132880.75, 132450.75, 0.00, 430.00, 430.00, N'Exception', SYSUTCDATETIME(), 0);
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<BankReconciliationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
SELECT BankReconciliationId, TenantId, BankAccountNumber, BankName, BankStatementDate, BankBalance, BookBalance, OutstandingDeposits, OutstandingChecks, Discrepancy, StatusCode, CreatedDateUtc
FROM Finance.BankReconciliation
WHERE BankReconciliationId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BankReconciliationDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BankReconciliationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var selectColumns = "BankReconciliationId, TenantId, BankAccountNumber, BankName, BankStatementDate, BankBalance, BookBalance, OutstandingDeposits, OutstandingChecks, Discrepancy, StatusCode, CreatedDateUtc";
        var searchPredicate = "BankAccountNumber LIKE '%' + @SearchTerm + '%' OR BankName LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.BankReconciliation", selectColumns, searchPredicate, "BankStatementDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<BankReconciliationDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BankReconciliationDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateBankReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        var discrepancy = request.BankBalance - request.BookBalance;
        const string sql = @"
INSERT INTO Finance.BankReconciliation (BankReconciliationId, TenantId, BankAccountNumber, BankName, BankStatementDate, BankBalance, BookBalance, OutstandingDeposits, OutstandingChecks, Discrepancy, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @BankAccountNumber, @BankName, @BankStatementDate, @BankBalance, @BookBalance, 0, 0, @Discrepancy, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.BankAccountNumber, request.BankName, request.BankStatementDate, request.BankBalance, request.BookBalance, Discrepancy = discrepancy, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateBankReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        var discrepancy = request.BankBalance - request.BookBalance;
        const string sql = @"
UPDATE Finance.BankReconciliation
SET BankAccountNumber = @BankAccountNumber,
    BankName = @BankName,
    BankStatementDate = @BankStatementDate,
    BankBalance = @BankBalance,
    BookBalance = @BookBalance,
    Discrepancy = @Discrepancy,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE BankReconciliationId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.BankAccountNumber, request.BankName, request.BankStatementDate, request.BankBalance, request.BookBalance, Discrepancy = discrepancy, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}