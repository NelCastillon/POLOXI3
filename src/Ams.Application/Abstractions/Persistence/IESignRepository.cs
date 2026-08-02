using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application.Abstractions.Persistence;

public interface IESignRepository
{
    Task<IReadOnlyList<ESignRequestDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ESignRequestDto?> GetByIdAsync(Guid tenantId, Guid eSignRequestId, CancellationToken cancellationToken = default);
    Task<Guid> SendAsync(SendESignRequest request, CancellationToken cancellationToken = default);
    Task VoidAsync(VoidESignRequest request, CancellationToken cancellationToken = default);
    Task RemindAsync(Guid tenantId, Guid eSignRequestId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task ProcessDocuSignCallbackAsync(ProcessDocuSignCallbackRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ESignDispatchWorkItem>> ClaimDispatchesAsync(string workerId, int batchSize, TimeSpan claimLease, CancellationToken cancellationToken = default);
    Task CompleteDispatchAsync(ESignDispatchWorkItem workItem, ESignEnvelopeDispatchResult result, CancellationToken cancellationToken = default);
    Task FailDispatchAsync(ESignDispatchWorkItem workItem, ESignDispatchFailure failure, CancellationToken cancellationToken = default);
}
