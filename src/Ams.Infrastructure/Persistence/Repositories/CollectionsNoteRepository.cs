using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CollectionsNoteRepository : ICollectionsNoteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CollectionsNoteRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF OBJECT_ID(N'Billing.CollectionsNote', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.CollectionsNote
    (
        CollectionsNoteId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId          UNIQUEIDENTIFIER NOT NULL,
        AccountId         UNIQUEIDENTIFIER NOT NULL,
        InvoiceId         UNIQUEIDENTIFIER NULL,
        NoteDate          DATE             NOT NULL,
        NoteText          NVARCHAR(2000)   NOT NULL,
        ActionCode        NVARCHAR(80)     NOT NULL,
        NextFollowUpDate  DATE             NULL,
        CreatedByUserId   UNIQUEIDENTIFIER NULL,
        CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc   DATETIME2        NULL,
        ModifiedByUserId  UNIQUEIDENTIFIER NULL,
        IsDeleted         BIT              NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Billing.CollectionsNote', N'TenantId') IS NULL ALTER TABLE Billing.CollectionsNote ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CollectionsNote_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Billing.CollectionsNote', N'AccountId') IS NULL ALTER TABLE Billing.CollectionsNote ADD AccountId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CollectionsNote_AccountId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Billing.CollectionsNote', N'InvoiceId') IS NULL ALTER TABLE Billing.CollectionsNote ADD InvoiceId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.CollectionsNote', N'NoteDate') IS NULL ALTER TABLE Billing.CollectionsNote ADD NoteDate DATE NOT NULL CONSTRAINT DF_CollectionsNote_NoteDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Billing.CollectionsNote', N'NoteText') IS NULL ALTER TABLE Billing.CollectionsNote ADD NoteText NVARCHAR(2000) NOT NULL CONSTRAINT DF_CollectionsNote_NoteText DEFAULT N'';
    IF COL_LENGTH(N'Billing.CollectionsNote', N'ActionCode') IS NULL ALTER TABLE Billing.CollectionsNote ADD ActionCode NVARCHAR(80) NOT NULL CONSTRAINT DF_CollectionsNote_Action DEFAULT N'Called';
    IF COL_LENGTH(N'Billing.CollectionsNote', N'NextFollowUpDate') IS NULL ALTER TABLE Billing.CollectionsNote ADD NextFollowUpDate DATE NULL;
    IF COL_LENGTH(N'Billing.CollectionsNote', N'CreatedByUserId') IS NULL ALTER TABLE Billing.CollectionsNote ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.CollectionsNote', N'CreatedDateUtc') IS NULL ALTER TABLE Billing.CollectionsNote ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CollectionsNote_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Billing.CollectionsNote', N'ModifiedDateUtc') IS NULL ALTER TABLE Billing.CollectionsNote ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Billing.CollectionsNote', N'ModifiedByUserId') IS NULL ALTER TABLE Billing.CollectionsNote ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.CollectionsNote', N'IsDeleted') IS NULL ALTER TABLE Billing.CollectionsNote ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CollectionsNote_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.CollectionsNote') AND name = N'IX_CollectionsNote_Tenant_Date')
    CREATE INDEX IX_CollectionsNote_Tenant_Date ON Billing.CollectionsNote(TenantId, NoteDate DESC, IsDeleted);

IF @TenantId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Billing.CollectionsNote WHERE TenantId = @TenantId)
   AND OBJECT_ID(N'Billing.Invoice', N'U') IS NOT NULL
BEGIN
    INSERT INTO Billing.CollectionsNote (CollectionsNoteId, TenantId, AccountId, InvoiceId, NoteDate, NoteText, ActionCode, NextFollowUpDate, CreatedDateUtc, IsDeleted)
    SELECT TOP (5)
        NEWID(), i.TenantId, i.AccountId, i.InvoiceId,
        DATEADD(day, -ROW_NUMBER() OVER (ORDER BY i.CreatedDateUtc DESC), CONVERT(date, SYSUTCDATETIME())),
        N'Tenant Admin collection follow-up generated from overdue billing invoice data.',
        CASE ROW_NUMBER() OVER (ORDER BY i.CreatedDateUtc DESC) % 4 WHEN 0 THEN N'Escalated' WHEN 1 THEN N'Called' WHEN 2 THEN N'Email Sent' ELSE N'Letter Sent' END,
        DATEADD(day, 3, CONVERT(date, SYSUTCDATETIME())),
        SYSUTCDATETIME(), 0
    FROM Billing.Invoice i
    WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.BalanceAmount > 0
    ORDER BY i.CreatedDateUtc DESC;
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<CollectionsNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "SELECT CollectionsNoteId, TenantId, AccountId, InvoiceId, NoteDate, NoteText, ActionCode, NextFollowUpDate, CreatedByUserId, CreatedDateUtc FROM Billing.CollectionsNote WHERE CollectionsNoteId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CollectionsNoteDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CollectionsNoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Billing.CollectionsNote", "CollectionsNoteId, TenantId, AccountId, InvoiceId, NoteDate, NoteText, ActionCode, NextFollowUpDate, CreatedByUserId, CreatedDateUtc", "NoteText LIKE '%' + @SearchTerm + '%' OR ActionCode LIKE '%' + @SearchTerm + '%'", "NoteDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CollectionsNoteDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CollectionsNoteDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCollectionsNoteRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Billing.CollectionsNote (CollectionsNoteId, TenantId, AccountId, InvoiceId, NoteDate, NoteText, ActionCode, NextFollowUpDate, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @AccountId, @InvoiceId, @NoteDate, @NoteText, @ActionCode, @NextFollowUpDate, @CreatedByUserId, SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.InvoiceId, request.NoteDate, request.NoteText, request.ActionCode, request.NextFollowUpDate, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCollectionsNoteRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
UPDATE Billing.CollectionsNote
SET AccountId = @AccountId,
    InvoiceId = @InvoiceId,
    NoteDate = @NoteDate,
    NoteText = @NoteText,
    ActionCode = @ActionCode,
    NextFollowUpDate = @NextFollowUpDate,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CollectionsNoteId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountId, request.InvoiceId, request.NoteDate, request.NoteText, request.ActionCode, request.NextFollowUpDate, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "UPDATE Billing.CollectionsNote SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE CollectionsNoteId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
