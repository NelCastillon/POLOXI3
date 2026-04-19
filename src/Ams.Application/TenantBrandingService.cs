using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class TenantBrandingService : ITenantBrandingService
{
    private readonly ITenantBrandingRepository _repository;

    public TenantBrandingService(ITenantBrandingRepository repository)
        => _repository = repository;

    public Task<TenantBrandingDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantIdAsync(tenantId, cancellationToken);

    public Task<TenantBrandingDto?> GetByIdAsync(Guid brandingId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(brandingId, cancellationToken);

    public Task<PagedResult<TenantBrandingDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken);

    public Task UpdateAsync(Guid tenantId, Features.Tenants.UpdateTenantBrandingRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(tenantId, request, cancellationToken);

    public Task ResetToDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.ResetToDefaultsAsync(tenantId, cancellationToken);
}
