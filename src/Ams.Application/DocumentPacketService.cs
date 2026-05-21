using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application;

public sealed class DocumentPacketService : IDocumentPacketService
{
    private readonly IDocumentPacketRepository _repository;

    public DocumentPacketService(IDocumentPacketRepository repository) => _repository = repository;

    public Task<IReadOnlyList<DocumentPacketDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantAsync(tenantId, cancellationToken);

    public Task<DocumentPacketDto?> GetByIdAsync(Guid documentPacketId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(documentPacketId, cancellationToken);

    public Task<Guid> CreateAsync(CreateDocumentPacketRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<Guid> AddDocumentAsync(AddDocumentPacketDocumentRequest request, CancellationToken cancellationToken = default)
        => _repository.AddDocumentAsync(request, cancellationToken);

    public Task RemoveDocumentAsync(Guid packetDocumentId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.RemoveDocumentAsync(packetDocumentId, modifiedByUserId, cancellationToken);

    public Task ReorderDocumentsAsync(ReorderDocumentPacketDocumentsRequest request, CancellationToken cancellationToken = default)
        => _repository.ReorderDocumentsAsync(request, cancellationToken);

    public Task SendAsync(SendDocumentPacketRequest request, CancellationToken cancellationToken = default)
        => _repository.SendAsync(request, cancellationToken);

    public Task UpdateStatusAsync(UpdateDocumentPacketStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(request, cancellationToken);

    public Task DeleteAsync(Guid documentPacketId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(documentPacketId, modifiedByUserId, cancellationToken);
}
