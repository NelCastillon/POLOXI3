using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _repository;
    public RoleService(IRoleRepository repository) => _repository = repository;
    public Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<RoleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(request, cancellationToken);
    public Task SetActiveAsync(Guid roleId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.SetActiveAsync(roleId, isActive, modifiedByUserId, cancellationToken);
}
