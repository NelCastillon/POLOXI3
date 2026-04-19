using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantDeploymentAssignments;

namespace Ams.Application;

public sealed class TenantDeploymentAssignmentService : ITenantDeploymentAssignmentService
{
    private readonly ITenantDeploymentAssignmentRepository _repository;

    public TenantDeploymentAssignmentService(ITenantDeploymentAssignmentRepository repository)
        => _repository = repository;

    public Task<TenantDeploymentAssignmentDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantIdAsync(tenantId, cancellationToken);

    public Task<Guid> UpsertAsync(UpsertTenantDeploymentAssignmentRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertAsync(request, cancellationToken);

    public Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(tenantId, cancellationToken);
}
