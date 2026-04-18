using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class RoleBundleService : IRoleBundleService
{
    private readonly IRoleBundleRepository _repository;
    public RoleBundleService(IRoleBundleRepository repository) => _repository = repository;

    public Task<RoleBundleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<RoleBundleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateRoleBundleRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(UpdateRoleBundleRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(request, cancellationToken);

    public Task SetActiveAsync(Guid bundleId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.SetActiveAsync(bundleId, isActive, modifiedByUserId, cancellationToken);

    public Task<IEnumerable<BundleRoleDto>> GetRolesAsync(Guid bundleId, CancellationToken cancellationToken = default)
        => _repository.GetRolesAsync(bundleId, cancellationToken);

    public Task SetRolesAsync(SetBundleRolesRequest request, CancellationToken cancellationToken = default)
        => _repository.SetRolesAsync(request, cancellationToken);

    public Task AssignToUsersAsync(AssignBundleToUsersRequest request, CancellationToken cancellationToken = default)
        => _repository.AssignToUsersAsync(request, cancellationToken);
}
