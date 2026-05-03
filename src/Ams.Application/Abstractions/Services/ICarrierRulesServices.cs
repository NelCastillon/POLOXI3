using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CarrierRules;

namespace Ams.Application.Abstractions.Services;

public interface IMarketAccessRuleService
{
    Task<MarketAccessRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<MarketAccessRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateMarketAccessRuleRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateMarketAccessRuleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICarrierDownloadMappingService
{
    Task<CarrierDownloadMappingDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CarrierDownloadMappingDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCarrierDownloadMappingRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCarrierDownloadMappingRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
