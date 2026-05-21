using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application.Abstractions.Persistence;

public interface IDocumentExceptionRepository
{
    Task<IReadOnlyList<DocumentExceptionDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DocumentExceptionDto?> GetByIdAsync(Guid documentExceptionId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDocumentExceptionRequest request, CancellationToken cancellationToken = default);
    Task ClassifyAsync(ClassifyDocumentExceptionRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(UpdateDocumentExceptionStatusRequest request, CancellationToken cancellationToken = default);
}
