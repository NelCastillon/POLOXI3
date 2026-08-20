using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application.Abstractions.Services;

public interface IESignService
{
    Task<IReadOnlyList<ESignRequestDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ESignRequestDto?> GetByIdAsync(Guid tenantId, Guid eSignRequestId, CancellationToken cancellationToken = default);
    Task<Guid> SendAsync(SendESignRequest request, CancellationToken cancellationToken = default);
    Task VoidAsync(VoidESignRequest request, CancellationToken cancellationToken = default);
    Task RemindAsync(Guid tenantId, Guid eSignRequestId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task ProcessDocuSignCallbackAsync(ProcessDocuSignCallbackRequest request, CancellationToken cancellationToken = default);
}
