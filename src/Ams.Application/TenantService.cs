using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Tenants;

namespace Ams.Application;

public sealed class TenantService : ITenantService
{
    private readonly ITenantRepository _repository;
    public TenantService(ITenantRepository repository) => _repository = repository;
    public Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<TenantDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
    public Task SetStatusAsync(Guid id, string statusCode, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default) => _repository.SetStatusAsync(id, statusCode, modifiedByUserId, cancellationToken);
}
