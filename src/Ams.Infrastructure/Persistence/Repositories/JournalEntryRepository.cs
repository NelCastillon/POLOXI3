using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class JournalEntryRepository : IJournalEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public JournalEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Finance') EXEC(N'CREATE SCHEMA Finance');

IF OBJECT_ID(N'Finance.JournalEntry', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.JournalEntry
    (
        JournalEntryId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        EntryNumber     NVARCHAR(50)     NOT NULL,
        EntryDate       DATE             NOT NULL,
        Description     NVARCHAR(1000)   NOT NULL,
        TotalDebit      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        TotalCredit     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        StatusCode      NVARCHAR(50)     NOT NULL DEFAULT N'Draft',
        CreatedDateUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2        NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted       BIT              NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Finance.JournalEntry', N'TenantId') IS NULL ALTER TABLE Finance.JournalEntry ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_JournalEntry_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Finance.JournalEntry', N'EntryNumber') IS NULL ALTER TABLE Finance.JournalEntry ADD EntryNumber NVARCHAR(50) NOT NULL CONSTRAINT DF_JournalEntry_EntryNumber DEFAULT N'JE-0000';
    IF COL_LENGTH(N'Finance.JournalEntry', N'EntryDate') IS NULL ALTER TABLE Finance.JournalEntry ADD EntryDate DATE NOT NULL CONSTRAINT DF_JournalEntry_EntryDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Finance.JournalEntry', N'Description') IS NULL ALTER TABLE Finance.JournalEntry ADD Description NVARCHAR(1000) NOT NULL CONSTRAINT DF_JournalEntry_Description DEFAULT N'Journal entry';
    IF COL_LENGTH(N'Finance.JournalEntry', N'TotalDebit') IS NULL ALTER TABLE Finance.JournalEntry ADD TotalDebit DECIMAL(18, 2) NOT NULL CONSTRAINT DF_JournalEntry_TotalDebit DEFAULT 0;
    IF COL_LENGTH(N'Finance.JournalEntry', N'TotalCredit') IS NULL ALTER TABLE Finance.JournalEntry ADD TotalCredit DECIMAL(18, 2) NOT NULL CONSTRAINT DF_JournalEntry_TotalCredit DEFAULT 0;
    IF COL_LENGTH(N'Finance.JournalEntry', N'StatusCode') IS NULL ALTER TABLE Finance.JournalEntry ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_JournalEntry_StatusCode DEFAULT N'Draft';
    IF COL_LENGTH(N'Finance.JournalEntry', N'CreatedDateUtc') IS NULL ALTER TABLE Finance.JournalEntry ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_JournalEntry_CreatedDateUtc DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Finance.JournalEntry', N'CreatedByUserId') IS NULL ALTER TABLE Finance.JournalEntry ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.JournalEntry', N'ModifiedDateUtc') IS NULL ALTER TABLE Finance.JournalEntry ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Finance.JournalEntry', N'ModifiedByUserId') IS NULL ALTER TABLE Finance.JournalEntry ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.JournalEntry', N'IsDeleted') IS NULL ALTER TABLE Finance.JournalEntry ADD IsDeleted BIT NOT NULL CONSTRAINT DF_JournalEntry_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Finance.JournalEntry') AND name = N'IX_JournalEntry_Tenant_Date')
    CREATE INDEX IX_JournalEntry_Tenant_Date ON Finance.JournalEntry(TenantId, EntryDate DESC, IsDeleted);

IF @TenantId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Finance.JournalEntry WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Finance.JournalEntry (JournalEntryId, TenantId, EntryNumber, EntryDate, Description, TotalDebit, TotalCredit, StatusCode, CreatedDateUtc, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'JE-SEED-001', DATEADD(day, -10, CONVERT(date, SYSUTCDATETIME())), N'Seeded opening balance entry synchronized with chart of accounts.', 12500.00, 12500.00, N'Posted', SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'JE-SEED-002', DATEADD(day, -5, CONVERT(date, SYSUTCDATETIME())), N'Seeded billing revenue accrual for finance workflow validation.', 4250.00, 4250.00, N'Posted', SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, N'JE-SEED-003', DATEADD(day, -1, CONVERT(date, SYSUTCDATETIME())), N'Seeded draft adjustment pending review.', 875.00, 875.00, N'Draft', SYSUTCDATETIME(), 0);
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<JournalEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
SELECT JournalEntryId, TenantId, EntryNumber, EntryDate, Description, TotalDebit, TotalCredit, StatusCode, CreatedDateUtc
FROM Finance.JournalEntry
WHERE JournalEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<JournalEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<JournalEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var selectColumns = "JournalEntryId, TenantId, EntryNumber, EntryDate, Description, TotalDebit, TotalCredit, StatusCode, CreatedDateUtc";
        var searchPredicate = "EntryNumber LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.JournalEntry", selectColumns, searchPredicate, "EntryDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<JournalEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<JournalEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.JournalEntry (JournalEntryId, TenantId, EntryNumber, EntryDate, Description, TotalDebit, TotalCredit, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @EntryNumber, @EntryDate, @Description, @TotalDebit, @TotalCredit, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.EntryNumber, request.EntryDate, request.Description, request.TotalDebit, request.TotalCredit, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
UPDATE Finance.JournalEntry
SET EntryNumber = @EntryNumber,
    EntryDate = @EntryDate,
    Description = @Description,
    TotalDebit = @TotalDebit,
    TotalCredit = @TotalCredit,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE JournalEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.EntryNumber, request.EntryDate, request.Description, request.TotalDebit, request.TotalCredit, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}