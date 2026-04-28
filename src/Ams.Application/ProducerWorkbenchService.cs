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

    public Task<string> GetNextLeadNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetNextLeadNumberAsync(tenantId, cancellationToken);

    public Task LogContactAsync(Guid tenantId, Guid itemId, string itemType, CancellationToken cancellationToken = default)
        => _repository.LogContactAsync(tenantId, itemId, itemType, cancellationToken);
}
