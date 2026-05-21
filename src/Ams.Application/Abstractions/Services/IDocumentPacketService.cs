using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application.Abstractions.Services;

public interface IDocumentPacketService
{
    Task<IReadOnlyList<DocumentPacketDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DocumentPacketDto?> GetByIdAsync(Guid documentPacketId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDocumentPacketRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddDocumentAsync(AddDocumentPacketDocumentRequest request, CancellationToken cancellationToken = default);
    Task RemoveDocumentAsync(Guid packetDocumentId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
    Task ReorderDocumentsAsync(ReorderDocumentPacketDocumentsRequest request, CancellationToken cancellationToken = default);
    Task SendAsync(SendDocumentPacketRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(UpdateDocumentPacketStatusRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid documentPacketId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
