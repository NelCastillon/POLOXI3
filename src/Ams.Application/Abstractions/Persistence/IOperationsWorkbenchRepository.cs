using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface IOperationsWorkbenchRepository
{
    Task<OperationsWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool myItemsOnly, string? assigneeFilter, CancellationToken cancellationToken = default);
    Task RetryItemAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default);
    Task SkipAutomationStepAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default);
}
