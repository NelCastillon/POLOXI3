using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Tenants;

namespace Ams.Application.Abstractions.Persistence;

public interface ITenantBrandingRepository
{
    Task<TenantBrandingDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantBrandingDto?> GetByIdAsync(Guid brandingId, CancellationToken cancellationToken = default);
    Task<PagedResult<TenantBrandingDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid tenantId, UpdateTenantBrandingRequest request, CancellationToken cancellationToken = default);
    Task ResetToDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
