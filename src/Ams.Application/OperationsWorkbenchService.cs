using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class OperationsWorkbenchService : IOperationsWorkbenchService
{
    private readonly IOperationsWorkbenchRepository _repository;

    public OperationsWorkbenchService(IOperationsWorkbenchRepository repository)
    {
        _repository = repository;
    }

    public Task<OperationsWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool myItemsOnly, string? assigneeFilter, CancellationToken cancellationToken = default)
        => _repository.GetWorkbenchAsync(tenantId, userId, myItemsOnly, assigneeFilter, cancellationToken);

    public Task RetryItemAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
        => _repository.RetryItemAsync(tenantId, itemId, cancellationToken);

    public Task SkipAutomationStepAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
        => _repository.SkipAutomationStepAsync(tenantId, itemId, cancellationToken);
}
