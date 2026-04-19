using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantDeploymentAssignments;

namespace Ams.Application.Abstractions.Persistence;

public interface ITenantDeploymentAssignmentRepository
{
    Task<TenantDeploymentAssignmentDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> UpsertAsync(UpsertTenantDeploymentAssignmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
