using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface IProducerWorkbenchService
{
    Task<ProducerWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default);
    Task<ProducerRenewalCallListDto> GetRenewalCallsAsync(Guid tenantId, Guid? userId, string? statusCode = null, CancellationToken cancellationToken = default);
    Task<ProducerRenewalCallDto?> GetRenewalCallAsync(Guid tenantId, Guid renewalKey, CancellationToken cancellationToken = default);
    Task UpdateRenewalCallAsync(Guid renewalCallId, UpdateProducerRenewalCallRequest request, CancellationToken cancellationToken = default);
    Task<string> GetNextLeadNumberAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task LogContactAsync(ProducerWorkbenchLogContactRequest request, CancellationToken cancellationToken = default);
}
