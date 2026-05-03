using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.BillingConfig;

namespace Ams.Application.Abstractions.Services;

public interface IBillingConfigService
{
    Task<BillingConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<BillingConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateBillingConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateBillingConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
