using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IPricingMarketRulesService
{
    Task<PagedResult<PriceClassDto>> SearchPriceClassesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default);
    Task<Guid> CreatePriceClassAsync(UpsertPriceClassRequest request, CancellationToken cancellationToken = default);
    Task UpdatePriceClassAsync(Guid id, UpsertPriceClassRequest request, CancellationToken cancellationToken = default);
    Task DeletePriceClassAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    Task<PagedResult<MarketAppetiteDto>> SearchMarketAppetiteAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default);
    Task<Guid> CreateMarketAppetiteAsync(UpsertMarketAppetiteRequest request, CancellationToken cancellationToken = default);
    Task UpdateMarketAppetiteAsync(Guid id, UpsertMarketAppetiteRequest request, CancellationToken cancellationToken = default);
    Task DeleteMarketAppetiteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    Task<PagedResult<CarrierMappingDto>> SearchCarrierMappingsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default);
    Task<Guid> CreateCarrierMappingAsync(UpsertCarrierMappingRequest request, CancellationToken cancellationToken = default);
    Task UpdateCarrierMappingAsync(Guid id, UpsertCarrierMappingRequest request, CancellationToken cancellationToken = default);
    Task DeleteCarrierMappingAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);
    Task TestCarrierMappingAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);
}
