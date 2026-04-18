using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Services;

public interface IRoleBundleService
{
    Task<RoleBundleDto?>              GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RoleBundleDto>>  SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid>                        CreateAsync(CreateRoleBundleRequest request, CancellationToken cancellationToken = default);
    Task                              UpdateAsync(UpdateRoleBundleRequest request, CancellationToken cancellationToken = default);
    Task                              SetActiveAsync(Guid bundleId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BundleRoleDto>>  GetRolesAsync(Guid bundleId, CancellationToken cancellationToken = default);
    Task                              SetRolesAsync(SetBundleRolesRequest request, CancellationToken cancellationToken = default);
    Task                              AssignToUsersAsync(AssignBundleToUsersRequest request, CancellationToken cancellationToken = default);
}
