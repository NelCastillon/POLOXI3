using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application;

public sealed class DocumentExceptionService : IDocumentExceptionService
{
    private readonly IDocumentExceptionRepository _repository;

    public DocumentExceptionService(IDocumentExceptionRepository repository) => _repository = repository;

    public Task<IReadOnlyList<DocumentExceptionDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantAsync(tenantId, cancellationToken);

    public Task<DocumentExceptionDto?> GetByIdAsync(Guid documentExceptionId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(documentExceptionId, cancellationToken);

    public Task<Guid> CreateAsync(CreateDocumentExceptionRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task ClassifyAsync(ClassifyDocumentExceptionRequest request, CancellationToken cancellationToken = default)
        => _repository.ClassifyAsync(request, cancellationToken);

    public Task UpdateStatusAsync(UpdateDocumentExceptionStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(request, cancellationToken);
}
