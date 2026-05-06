using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class ServiceManagerWorkbenchService : IServiceManagerWorkbenchService
{
    private readonly IServiceManagerWorkbenchRepository _repository;

    public ServiceManagerWorkbenchService(IServiceManagerWorkbenchRepository repository)
    {
        _repository = repository;
    }

    public Task<ServiceManagerWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default)
        => _repository.GetWorkbenchAsync(tenantId, userId, teamScope, branchId, teamId, cancellationToken);

    public Task AssignAsync(Guid tenantId, Guid itemId, Guid assignedToUserId, CancellationToken cancellationToken = default)
        => _repository.AssignAsync(tenantId, itemId, assignedToUserId, cancellationToken);
}
