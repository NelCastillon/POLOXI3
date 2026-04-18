using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Persistence;

public interface IPermissionRepository
{
    Task<PermissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PermissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? resourceCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid permissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);

    // Role-permission assignments
    Task<IEnumerable<RolePermissionDto>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RolePermissionDto>> GetByPermissionAsync(Guid permissionId, CancellationToken cancellationToken = default);
    Task<Guid> AssignToRoleAsync(AssignRolePermissionRequest request, CancellationToken cancellationToken = default);
    Task RevokeFromRoleAsync(RevokeRolePermissionRequest request, CancellationToken cancellationToken = default);
    Task<RolePermissionMatrixDto> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
