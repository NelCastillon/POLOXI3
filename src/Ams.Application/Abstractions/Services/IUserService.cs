using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Ams.Application.Features.Security;

namespace Ams.Application.Abstractions.Services;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<UserDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid userId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task LockAsync(Guid userId, DateTime? lockoutEnd, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task UnlockAsync(Guid userId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task ChangeStatusAsync(ChangeUserStatusRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserPermissionDto>> GetDirectPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserPermissionDto>> GetDirectUsersByPermissionAsync(Guid permissionId, CancellationToken cancellationToken = default);
    Task<Guid> GrantPermissionAsync(GrantUserPermissionRequest request, CancellationToken cancellationToken = default);
    Task RevokePermissionAsync(Guid userPermissionId, Guid? revokedByUserId, CancellationToken cancellationToken = default);
}
