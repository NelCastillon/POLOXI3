using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.TenantConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface ITenantConfigRepository
{
    Task<TenantConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<TenantConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateTenantConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateTenantConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
