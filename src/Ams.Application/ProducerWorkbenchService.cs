using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class ProducerWorkbenchService : IProducerWorkbenchService
{
    private readonly IProducerWorkbenchRepository _repository;

    public ProducerWorkbenchService(IProducerWorkbenchRepository repository)
    {
        _repository = repository;
    }

    public Task<ProducerWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default)
        => _repository.GetWorkbenchAsync(tenantId, userId, cancellationToken);

    public Task<ProducerRenewalCallListDto> GetRenewalCallsAsync(Guid tenantId, Guid? userId, string? statusCode = null, CancellationToken cancellationToken = default)
        => _repository.GetRenewalCallsAsync(tenantId, userId, statusCode, cancellationToken);

    public Task<ProducerRenewalCallDto?> GetRenewalCallAsync(Guid tenantId, Guid renewalKey, CancellationToken cancellationToken = default)
        => _repository.GetRenewalCallAsync(tenantId, renewalKey, cancellationToken);

    public Task UpdateRenewalCallAsync(Guid renewalCallId, UpdateProducerRenewalCallRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateRenewalCallAsync(renewalCallId, request, cancellationToken);

    public Task<string> GetNextLeadNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetNextLeadNumberAsync(tenantId, cancellationToken);

    public Task LogContactAsync(ProducerWorkbenchLogContactRequest request, CancellationToken cancellationToken = default)
        => _repository.LogContactAsync(request, cancellationToken);
}
