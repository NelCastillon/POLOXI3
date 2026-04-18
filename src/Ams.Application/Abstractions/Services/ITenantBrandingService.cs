using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface ITenantBrandingService
{
    Task<TenantBrandingDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantBrandingDto?> GetByIdAsync(Guid brandingId, CancellationToken cancellationToken = default);
    Task<PagedResult<TenantBrandingDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
