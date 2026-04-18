using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Services;

public interface IUserPermissionService
{
    Task<PagedResult<UserPermissionDto>>           SearchAsync(Guid tenantId, Guid? userId, Guid? permissionId, bool? isGranted, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid>                                     GrantAsync(GrantUserPermissionRequest request, CancellationToken cancellationToken = default);
    Task                                           UpdateAsync(UpdateUserPermissionRequest request, CancellationToken cancellationToken = default);
    Task                                           RevokeAsync(Guid userPermissionId, Guid? revokedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserPermissionScopeDto>>    GetScopesAsync(Guid userPermissionId, CancellationToken cancellationToken = default);
    Task<Guid>                                     AddScopeAsync(AddPermissionScopeRequest request, CancellationToken cancellationToken = default);
    Task                                           RemoveScopeAsync(Guid userPermissionScopeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionConflictDto>>     ValidateConflictsAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionScopePreviewDto>> PreviewEffectiveScopeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}
