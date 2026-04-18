using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class UserRoleService : IUserRoleService
{
    private readonly IUserRoleRepository _repository;
    public UserRoleService(IUserRoleRepository repository) => _repository = repository;
    public Task<PagedResult<UserRoleDto>>            SearchAsync(Guid tenantId, Guid? userId, Guid? roleId, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, userId, roleId, isActive, pageNumber, pageSize, cancellationToken);
    public Task<Guid>                               AssignAsync(AssignUserRoleRequest request, CancellationToken cancellationToken = default)  => _repository.AssignAsync(request, cancellationToken);
    public Task                                     RevokeAsync(RevokeUserRoleRequest request, CancellationToken cancellationToken = default)  => _repository.RevokeAsync(request, cancellationToken);
    public Task                                     RemoveAsync(RemoveRoleAssignmentRequest request, CancellationToken cancellationToken = default)  => _repository.RemoveAsync(request, cancellationToken);
    public Task                                     ApproveAsync(ApproveRoleAssignmentRequest request, CancellationToken cancellationToken = default) => _repository.ApproveAsync(request, cancellationToken);
    public Task                                     ExtendAsync(ExtendRoleAssignmentRequest request, CancellationToken cancellationToken = default)  => _repository.ExtendAsync(request, cancellationToken);
    public Task<IEnumerable<EffectivePermissionDto>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => _repository.GetEffectivePermissionsAsync(userId, cancellationToken);
}
