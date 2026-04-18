using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _repository;
    public PermissionService(IPermissionRepository repository) => _repository = repository;
    public Task<PermissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<PermissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? resourceCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, resourceCode, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task DeactivateAsync(Guid permissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.DeactivateAsync(permissionId, modifiedByUserId, cancellationToken);
    public Task<IEnumerable<RolePermissionDto>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default) => _repository.GetByRoleAsync(roleId, cancellationToken);
    public Task<IEnumerable<RolePermissionDto>> GetByPermissionAsync(Guid permissionId, CancellationToken cancellationToken = default) => _repository.GetByPermissionAsync(permissionId, cancellationToken);
    public Task<Guid> AssignToRoleAsync(AssignRolePermissionRequest request, CancellationToken cancellationToken = default) => _repository.AssignToRoleAsync(request, cancellationToken);
    public Task RevokeFromRoleAsync(RevokeRolePermissionRequest request, CancellationToken cancellationToken = default) => _repository.RevokeFromRoleAsync(request, cancellationToken);
    public Task<RolePermissionMatrixDto> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default) => _repository.GetMatrixAsync(tenantId, cancellationToken);
}
