using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CarrierRules;

namespace Ams.Application.Abstractions.Persistence;

public interface IMarketAccessRuleRepository
{
    Task<MarketAccessRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<MarketAccessRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateMarketAccessRuleRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateMarketAccessRuleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICarrierDownloadMappingRepository
{
    Task<CarrierDownloadMappingDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CarrierDownloadMappingDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCarrierDownloadMappingRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCarrierDownloadMappingRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICarrierRuleCategoryRepository
{
    Task<IReadOnlyList<CarrierRuleCategoryDto>> GetActiveAsync(CancellationToken ct = default);
}

public interface ICarrierRuleLookupRepository
{
    Task<IReadOnlyList<CarrierRuleOptionDto>> GetOptionsAsync(Guid tenantId, string optionType, CancellationToken ct = default);
    Task<IReadOnlyList<CarrierProductCatalogDto>> GetProductsAsync(Guid tenantId, Guid? carrierId, Guid? lineOfBusinessId, CancellationToken ct = default);
}

public interface ICarrierProductRuleRepository
{
    Task<CarrierProductRuleDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<PagedResult<CarrierProductRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, string? categoryCode, bool? isActive, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCarrierProductRuleRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid tenantId, Guid id, UpdateCarrierProductRuleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
