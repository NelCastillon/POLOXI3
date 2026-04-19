using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Tenants;

namespace Ams.Application;

public sealed class TenantDomainService : ITenantDomainService
{
    private readonly ITenantDomainRepository _repository;
    public TenantDomainService(ITenantDomainRepository repository) => _repository = repository;

    public Task<TenantDomainDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<TenantDomainDto>> SearchByTenantAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchByTenantAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<TenantDomainDto>> SearchAllAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAllAsync(searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateTenantDomainRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateRedirectAsync(Guid id, string? redirectTarget, string? notes = null, CancellationToken cancellationToken = default)
        => _repository.UpdateRedirectAsync(id, redirectTarget, notes, cancellationToken);

    public Task SetPrimaryAsync(Guid tenantId, Guid domainId, CancellationToken cancellationToken = default)
        => _repository.SetPrimaryAsync(tenantId, domainId, cancellationToken);

    public Task VerifyAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.VerifyAsync(id, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);
}
