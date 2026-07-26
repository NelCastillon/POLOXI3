using System.ComponentModel.DataAnnotations;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.Documents;

namespace Ams.Application;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IQuoteRepository _quoteRepository;
    private readonly ILeadRepository _leadRepository;

    public DocumentService(
        IDocumentRepository repository,
        IAccountRepository accountRepository,
        IOpportunityRepository opportunityRepository,
        ISubmissionRepository submissionRepository,
        IQuoteRepository quoteRepository,
        ILeadRepository leadRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _opportunityRepository = opportunityRepository;
        _submissionRepository = submissionRepository;
        _quoteRepository = quoteRepository;
        _leadRepository = leadRepository;
    }

    public Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<DocumentDto>> SearchAsync(Guid tenantId, string? categoryCode, string? entityName, Guid? entityId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, categoryCode, entityName, entityId, searchTerm, pageNumber, pageSize, cancellationToken);

    public async Task<Guid> CreateAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: when a document is attached to a business record, that parent
        // entity must exist and belong to the same tenant. This prevents orphaned or
        // cross-tenant document attachments through the polymorphic EntityName/EntityId link.
        await ValidateEntityOwnershipAsync(request.TenantId, request.EntityName, request.EntityId, cancellationToken);
        return await _repository.CreateAsync(request, cancellationToken);
    }

    private async Task ValidateEntityOwnershipAsync(Guid tenantId, string? entityName, Guid? entityId, CancellationToken cancellationToken)
    {
        // Unattached (library) documents have no parent context to validate.
        if (string.IsNullOrWhiteSpace(entityName) || !entityId.HasValue || entityId.Value == Guid.Empty)
        {
            return;
        }

        // Resolve the supplied entity to its typed parent. Unknown entity types are treated as
        // free-form attachments and are not blocked, preserving the polymorphic document contract.
        var normalized = entityName.Trim();
        switch (normalized.ToLowerInvariant())
        {
            case "account":
                await TenantGuard.EnsureOptionalParentAsync(entityId, tenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Account", "document", cancellationToken);
                break;
            case "opportunity":
                await TenantGuard.EnsureOptionalParentAsync(entityId, tenantId, _opportunityRepository.GetByIdAsync, o => o.TenantId, "Opportunity", "document", cancellationToken);
                break;
            case "submission":
                await TenantGuard.EnsureOptionalParentAsync(entityId, tenantId, _submissionRepository.GetByIdAsync, s => s.TenantId, "Submission", "document", cancellationToken);
                break;
            case "quote":
                await TenantGuard.EnsureOptionalParentAsync(entityId, tenantId, _quoteRepository.GetByIdAsync, q => q.TenantId, "Quote", "document", cancellationToken);
                break;
            case "lead":
                await TenantGuard.EnsureOptionalParentAsync(entityId, tenantId, _leadRepository.GetByIdAsync, l => l.TenantId, "Lead", "document", cancellationToken);
                break;
            default:
                // Unknown/unsupported entity type: no typed parent to validate against.
                break;
        }
    }
    public Task UpdateMetadataAsync(UpdateDocumentMetadataRequest request, CancellationToken cancellationToken = default) => _repository.UpdateMetadataAsync(request, cancellationToken);
    public Task ArchiveAsync(Guid documentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.ArchiveAsync(documentId, modifiedByUserId, cancellationToken);

    // Version control
    public Task<IReadOnlyList<DocumentVersionDto>> GetVersionsAsync(Guid documentId, CancellationToken cancellationToken = default) => _repository.GetVersionsAsync(documentId, cancellationToken);
    public Task<DocumentVersionDto?> GetVersionAsync(Guid documentId, Guid documentVersionId, CancellationToken cancellationToken = default) => _repository.GetVersionAsync(documentId, documentVersionId, cancellationToken);
    public async Task<Guid> CreateVersionAsync(CreateDocumentVersionRequest request, CancellationToken cancellationToken = default)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var document = await _repository.GetByIdAsync(request.DocumentId, cancellationToken)
            ?? throw new KeyNotFoundException("The document was not found or is no longer available.");

        if (request.TenantId == Guid.Empty || document.TenantId != request.TenantId)
            throw new ValidationException("The document does not belong to the specified tenant.");

        if (string.Equals(document.StatusCode, "Archived", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Archived documents cannot receive new versions. Restore the document before uploading a version.");

        request.FileName = request.FileName.Trim();
        request.StoragePath = request.StoragePath.Trim();
        request.ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? null : request.ContentType.Trim();
        request.ChangeNotes = request.ChangeNotes!.Trim();

        return await _repository.CreateVersionAsync(request, cancellationToken);
    }

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
