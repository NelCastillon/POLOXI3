using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.MarketingConfig;

namespace Ams.Application.Abstractions.Services;

public interface IMarketingConfigService
{
    Task<MarketingConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<MarketingConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateMarketingConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateMarketingConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
