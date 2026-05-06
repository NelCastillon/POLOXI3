using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface IServiceManagerWorkbenchService
{
    Task<ServiceManagerWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default);
    Task AssignAsync(Guid tenantId, Guid itemId, Guid assignedToUserId, CancellationToken cancellationToken = default);
}
