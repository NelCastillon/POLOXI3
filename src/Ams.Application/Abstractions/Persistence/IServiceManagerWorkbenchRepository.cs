using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface IServiceManagerWorkbenchRepository
{
    Task<ServiceManagerWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default);
    Task AssignAsync(Guid tenantId, Guid itemId, Guid assignedToUserId, CancellationToken cancellationToken = default);
}
