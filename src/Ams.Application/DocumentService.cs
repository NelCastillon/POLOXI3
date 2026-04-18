using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Documents;

namespace Ams.Application;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    public DocumentService(IDocumentRepository repository) => _repository = repository;

    public Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<DocumentDto>> SearchAsync(Guid tenantId, string? categoryCode, string? entityName, Guid? entityId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, categoryCode, entityName, entityId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateMetadataAsync(UpdateDocumentMetadataRequest request, CancellationToken cancellationToken = default) => _repository.UpdateMetadataAsync(request, cancellationToken);
    public Task ArchiveAsync(Guid documentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.ArchiveAsync(documentId, modifiedByUserId, cancellationToken);

    // Version control
    public Task<IReadOnlyList<DocumentVersionDto>> GetVersionsAsync(Guid documentId, CancellationToken cancellationToken = default) => _repository.GetVersionsAsync(documentId, cancellationToken);
    public Task<Guid> CreateVersionAsync(CreateDocumentVersionRequest request, CancellationToken cancellationToken = default) => _repository.CreateVersionAsync(request, cancellationToken);

    // Secure sharing
    public Task<IReadOnlyList<DocumentShareLinkDto>> GetShareLinksAsync(Guid documentId, CancellationToken cancellationToken = default) => _repository.GetShareLinksAsync(documentId, cancellationToken);
    public Task<Guid> CreateShareLinkAsync(CreateDocumentShareLinkRequest request, CancellationToken cancellationToken = default) => _repository.CreateShareLinkAsync(request, cancellationToken);
    public Task RevokeShareLinkAsync(Guid shareLinkId, CancellationToken cancellationToken = default) => _repository.RevokeShareLinkAsync(shareLinkId, cancellationToken);

    // Audit / access log
    public Task<IReadOnlyList<DocumentAccessLogDto>> GetAccessLogAsync(Guid documentId, int top = 50, CancellationToken cancellationToken = default) => _repository.GetAccessLogAsync(documentId, top, cancellationToken);
    public Task LogAccessAsync(Guid tenantId, Guid documentId, Guid? userId, Guid? shareLinkId, string actionCode, string? ipAddress, CancellationToken cancellationToken = default) => _repository.LogAccessAsync(tenantId, documentId, userId, shareLinkId, actionCode, ipAddress, cancellationToken);

    // Core CRUD (additional)
    public Task<IReadOnlyList<DocumentDto>> GetByEntityAsync(Guid tenantId, string entityName, Guid entityId, CancellationToken cancellationToken = default) => _repository.GetByEntityAsync(tenantId, entityName, entityId, cancellationToken);
    public Task RenameAsync(RenameDocumentRequest request, CancellationToken cancellationToken = default) => _repository.RenameAsync(request, cancellationToken);
    public Task DeleteAsync(DeleteDocumentRequest request, CancellationToken cancellationToken = default) => _repository.DeleteAsync(request, cancellationToken);
}
