using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Ams.Application.Features.Security;

namespace Ams.Application;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _repository;
    public UserService(IUserRepository repository) => _repository = repository;
    public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<UserDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<IReadOnlyList<JobTitleDto>> GetJobTitlesAsync(Guid tenantId, Guid? departmentId = null, CancellationToken cancellationToken = default) => _repository.GetJobTitlesAsync(tenantId, departmentId, cancellationToken);
    public Task<Guid> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(request, cancellationToken);
    public Task SetActiveAsync(Guid userId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.SetActiveAsync(userId, isActive, modifiedByUserId, cancellationToken);
    public Task LockAsync(Guid userId, DateTime? lockoutEnd, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.LockAsync(userId, lockoutEnd, modifiedByUserId, cancellationToken);
    public Task UnlockAsync(Guid userId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.UnlockAsync(userId, modifiedByUserId, cancellationToken);
    public Task SetMfaAsync(Guid userId, bool enabled, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.SetMfaAsync(userId, enabled, modifiedByUserId, cancellationToken);
    public Task AssignBranchAsync(Guid userId, Guid? branchId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.AssignBranchAsync(userId, branchId, modifiedByUserId, cancellationToken);
    public Task ChangeStatusAsync(ChangeUserStatusRequest request, CancellationToken cancellationToken = default) => _repository.ChangeStatusAsync(request, cancellationToken);
    public Task<IEnumerable<UserPermissionDto>> GetDirectPermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => _repository.GetDirectPermissionsAsync(userId, cancellationToken);
    public Task<IEnumerable<UserPermissionDto>> GetDirectUsersByPermissionAsync(Guid permissionId, CancellationToken cancellationToken = default) => _repository.GetDirectUsersByPermissionAsync(permissionId, cancellationToken);
    public Task<Guid> GrantPermissionAsync(GrantUserPermissionRequest request, CancellationToken cancellationToken = default) => _repository.GrantPermissionAsync(request, cancellationToken);
    public Task RevokePermissionAsync(Guid userPermissionId, Guid? revokedByUserId, CancellationToken cancellationToken = default) => _repository.RevokePermissionAsync(userPermissionId, revokedByUserId, cancellationToken);
}
