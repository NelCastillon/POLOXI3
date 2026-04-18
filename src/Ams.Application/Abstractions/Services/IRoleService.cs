using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Services;

public interface IRoleService
{
    Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RoleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid roleId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
