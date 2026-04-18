using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Persistence;

public interface IUserRoleRepository
{
    Task<PagedResult<UserRoleDto>>            SearchAsync(Guid tenantId, Guid? userId, Guid? roleId, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid>                               AssignAsync(AssignUserRoleRequest request, CancellationToken cancellationToken = default);
    Task                                     RevokeAsync(RevokeUserRoleRequest request, CancellationToken cancellationToken = default);
    Task                                     RemoveAsync(RemoveRoleAssignmentRequest request, CancellationToken cancellationToken = default);
    Task                                     ApproveAsync(ApproveRoleAssignmentRequest request, CancellationToken cancellationToken = default);
    Task                                     ExtendAsync(ExtendRoleAssignmentRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<EffectivePermissionDto>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
