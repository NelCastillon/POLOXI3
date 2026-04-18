using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Documents;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository : IDocumentRepository
{
    private const string SelectColumns = "DocumentId, TenantId, DocumentTypeCode, CategoryCode, EntityName, EntityId, FileName, StoragePath, ContentType, FileSizeBytes, VersionNumber, StatusCode, RetentionDate, Description, Tags, UploadedByName, CreatedDateUtc, ModifiedDateUtc";

    private readonly ISqlConnectionFactory _connectionFactory;
    public DocumentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM DMS.Document WHERE DocumentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DocumentDto>> SearchAsync(Guid tenantId, string? categoryCode, string? entityName, Guid? entityId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = $@"
;WITH Cte AS (
    SELECT {SelectColumns}
    FROM DMS.Document
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@CategoryCode IS NULL OR @CategoryCode = '' OR CategoryCode = @CategoryCode)
      AND (@EntityName IS NULL OR @EntityName = '' OR EntityName = @EntityName)
      AND (@EntityId IS NULL OR EntityId = @EntityId)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR FileName LIKE '%' + @SearchTerm + '%' OR DocumentTypeCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR Tags LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1) FROM DMS.Document
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@CategoryCode IS NULL OR @CategoryCode = '' OR CategoryCode = @CategoryCode)
  AND (@EntityName IS NULL OR @EntityName = '' OR EntityName = @EntityName)
  AND (@EntityId IS NULL OR EntityId = @EntityId)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR FileName LIKE '%' + @SearchTerm + '%' OR DocumentTypeCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR Tags LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, CategoryCode = categoryCode, EntityName = entityName, EntityId = entityId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DocumentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DocumentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.Document (DocumentId, TenantId, DocumentTypeCode, CategoryCode, FileName, StoragePath, ContentType, FileSizeBytes, EntityName, EntityId, Description, Tags, RetentionDate, VersionNumber, StatusCode, UploadedByName, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@DocumentId, @TenantId, @DocumentTypeCode, @CategoryCode, @FileName, @StoragePath, @ContentType, @FileSizeBytes, @EntityName, @EntityId, @Description, @Tags, @RetentionDate, 1, 'Active', @UploadedByName, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { DocumentId = id, request.TenantId, request.DocumentTypeCode, request.CategoryCode, request.FileName, request.StoragePath, request.ContentType, request.FileSizeBytes, request.EntityName, request.EntityId, request.Description, request.Tags, request.RetentionDate, request.UploadedByName, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateMetadataAsync(UpdateDocumentMetadataRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.Document SET Description = @Description, Tags = @Tags, RetentionDate = @RetentionDate, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE DocumentId = @DocumentId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.DocumentId, request.Description, request.Tags, request.RetentionDate, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task ArchiveAsync(Guid documentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.Document SET StatusCode = 'Archived', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE DocumentId = @DocumentId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { DocumentId = documentId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    // ── Version control ──────────────────────────────────────

    public async Task<IReadOnlyList<DocumentVersionDto>> GetVersionsAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DocumentVersionId, TenantId, DocumentId, VersionNumber, FileName, StoragePath, ContentType, FileSizeBytes, ChangeNotes, CreatedByUserId, CreatedDateUtc FROM DMS.DocumentVersion WHERE DocumentId = @DocumentId AND IsDeleted = 0 ORDER BY VersionNumber DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentVersionDto>(new CommandDefinition(sql, new { DocumentId = documentId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateVersionAsync(CreateDocumentVersionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NextVersion INT;
SELECT @NextVersion = ISNULL(MAX(VersionNumber), 0) + 1 FROM DMS.DocumentVersion WHERE DocumentId = @DocumentId AND IsDeleted = 0;

INSERT INTO DMS.DocumentVersion (DocumentVersionId, TenantId, DocumentId, VersionNumber, FileName, StoragePath, ContentType, FileSizeBytes, ChangeNotes, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@DocumentVersionId, @TenantId, @DocumentId, @NextVersion, @FileName, @StoragePath, @ContentType, @FileSizeBytes, @ChangeNotes, @CreatedByUserId, SYSUTCDATETIME(), 0);

UPDATE DMS.Document SET VersionNumber = @NextVersion, FileName = @FileName, StoragePath = @StoragePath, ContentType = @ContentType, FileSizeBytes = @FileSizeBytes, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @CreatedByUserId WHERE DocumentId = @DocumentId AND IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { DocumentVersionId = id, request.TenantId, request.DocumentId, request.FileName, request.StoragePath, request.ContentType, request.FileSizeBytes, request.ChangeNotes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    // ── Secure sharing ───────────────────────────────────────

    public async Task<IReadOnlyList<DocumentShareLinkDto>> GetShareLinksAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ShareLinkId, TenantId, DocumentId, Token, CreatedByUserId, ExpiresDateUtc, MaxAccessCount, AccessCount, RequiresPin, IsRevoked, CreatedDateUtc FROM DMS.DocumentShareLink WHERE DocumentId = @DocumentId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentShareLinkDto>(new CommandDefinition(sql, new { DocumentId = documentId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateShareLinkAsync(CreateDocumentShareLinkRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentShareLink (ShareLinkId, TenantId, DocumentId, Token, CreatedByUserId, ExpiresDateUtc, MaxAccessCount, AccessCount, RequiresPin, PinHash, IsRevoked, CreatedDateUtc, IsDeleted)
VALUES (@ShareLinkId, @TenantId, @DocumentId, @Token, @CreatedByUserId, @ExpiresDateUtc, @MaxAccessCount, 0, @RequiresPin, @PinHash, 0, SYSUTCDATETIME(), 0);";
        var id = Guid.NewGuid();
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("+", "-").Replace("/", "_");
        var pinHash = request.RequiresPin && !string.IsNullOrWhiteSpace(request.Pin) ? Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Pin))) : null;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ShareLinkId = id, request.TenantId, request.DocumentId, Token = token, request.CreatedByUserId, request.ExpiresDateUtc, request.MaxAccessCount, request.RequiresPin, PinHash = pinHash }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task RevokeShareLinkAsync(Guid shareLinkId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentShareLink SET IsRevoked = 1, RevokedDateUtc = SYSUTCDATETIME() WHERE ShareLinkId = @ShareLinkId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ShareLinkId = shareLinkId }, cancellationToken: cancellationToken));
    }

    // ── Audit / access log ───────────────────────────────────

    public async Task<IReadOnlyList<DocumentAccessLogDto>> GetAccessLogAsync(Guid documentId, int top = 50, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TOP(@Top) AccessLogId, TenantId, DocumentId, UserId, ShareLinkId, ActionCode, IpAddress, AccessDateUtc FROM DMS.DocumentAccessLog WHERE DocumentId = @DocumentId ORDER BY AccessDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentAccessLogDto>(new CommandDefinition(sql, new { DocumentId = documentId, Top = top }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task LogAccessAsync(Guid tenantId, Guid documentId, Guid? userId, Guid? shareLinkId, string actionCode, string? ipAddress, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentAccessLog (AccessLogId, TenantId, DocumentId, UserId, ShareLinkId, ActionCode, IpAddress, AccessDateUtc)
VALUES (@AccessLogId, @TenantId, @DocumentId, @UserId, @ShareLinkId, @ActionCode, @IpAddress, SYSUTCDATETIME());";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { AccessLogId = Guid.NewGuid(), TenantId = tenantId, DocumentId = documentId, UserId = userId, ShareLinkId = shareLinkId, ActionCode = actionCode, IpAddress = ipAddress }, cancellationToken: cancellationToken));
    }

    // ── Core CRUD (additional) ───────────────────────────────

    public async Task<IReadOnlyList<DocumentDto>> GetByEntityAsync(Guid tenantId, string entityName, Guid entityId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM DMS.Document WHERE TenantId = @TenantId AND EntityName = @EntityName AND EntityId = @EntityId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentDto>(new CommandDefinition(sql, new { TenantId = tenantId, EntityName = entityName, EntityId = entityId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task RenameAsync(RenameDocumentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.Document SET FileName = @NewFileName, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE DocumentId = @DocumentId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.DocumentId, request.NewFileName, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(DeleteDocumentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.Document SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @DeletedByUserId WHERE DocumentId = @DocumentId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.DocumentId, request.DeletedByUserId }, cancellationToken: cancellationToken));
    }
}
