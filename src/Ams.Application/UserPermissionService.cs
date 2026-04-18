using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class UserPermissionService : IUserPermissionService
{
    private readonly IUserPermissionRepository _repository;
    public UserPermissionService(IUserPermissionRepository repository) => _repository = repository;

    public Task<PagedResult<UserPermissionDto>> SearchAsync(Guid tenantId, Guid? userId, Guid? permissionId, bool? isGranted, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, userId, permissionId, isGranted, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> GrantAsync(GrantUserPermissionRequest request, CancellationToken cancellationToken = default)
        => _repository.GrantAsync(request, cancellationToken);

    public Task UpdateAsync(UpdateUserPermissionRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(request, cancellationToken);

    public Task RevokeAsync(Guid userPermissionId, Guid? revokedByUserId, CancellationToken cancellationToken = default)
        => _repository.RevokeAsync(userPermissionId, revokedByUserId, cancellationToken);

    public Task<IReadOnlyList<UserPermissionScopeDto>> GetScopesAsync(Guid userPermissionId, CancellationToken cancellationToken = default)
        => _repository.GetScopesAsync(userPermissionId, cancellationToken);

    public Task<Guid> AddScopeAsync(AddPermissionScopeRequest request, CancellationToken cancellationToken = default)
        => _repository.AddScopeAsync(request, cancellationToken);

    public Task RemoveScopeAsync(Guid userPermissionScopeId, CancellationToken cancellationToken = default)
        => _repository.RemoveScopeAsync(userPermissionScopeId, cancellationToken);

    public Task<IReadOnlyList<PermissionConflictDto>> ValidateConflictsAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default)
        => _repository.ValidateConflictsAsync(tenantId, userId, cancellationToken);

    public Task<IReadOnlyList<PermissionScopePreviewDto>> PreviewEffectiveScopeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        => _repository.PreviewEffectiveScopeAsync(tenantId, userId, cancellationToken);
}

