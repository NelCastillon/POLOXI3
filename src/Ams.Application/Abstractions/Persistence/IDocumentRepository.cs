using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Documents;

namespace Ams.Application.Abstractions.Persistence;

public interface IDocumentRepository
{
    // ── Core CRUD ────────────────────────────────────────────
    Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentDto>> SearchAsync(Guid tenantId, string? categoryCode, string? entityName, Guid? entityId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentDto>> GetByEntityAsync(Guid tenantId, string entityName, Guid entityId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default);
    Task UpdateMetadataAsync(UpdateDocumentMetadataRequest request, CancellationToken cancellationToken = default);
    Task RenameAsync(RenameDocumentRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid documentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(DeleteDocumentRequest request, CancellationToken cancellationToken = default);

    // ── Version control ──────────────────────────────────────
    Task<IReadOnlyList<DocumentVersionDto>> GetVersionsAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<DocumentVersionDto?> GetVersionAsync(Guid documentId, Guid documentVersionId, CancellationToken cancellationToken = default);
    Task<Guid> CreateVersionAsync(CreateDocumentVersionRequest request, CancellationToken cancellationToken = default);

    // ── Secure sharing ───────────────────────────────────────
    Task<IReadOnlyList<DocumentShareLinkDto>> GetShareLinksAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<Guid> CreateShareLinkAsync(CreateDocumentShareLinkRequest request, CancellationToken cancellationToken = default);
    Task RevokeShareLinkAsync(Guid shareLinkId, CancellationToken cancellationToken = default);

    // ── Audit / access log ───────────────────────────────────
    Task<IReadOnlyList<DocumentAccessLogDto>> GetAccessLogAsync(Guid documentId, int top = 50, CancellationToken cancellationToken = default);
    Task LogAccessAsync(Guid tenantId, Guid documentId, Guid? userId, Guid? shareLinkId, string actionCode, string? ipAddress, CancellationToken cancellationToken = default);
}
