using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CarrierRules;

namespace Ams.Application;

public sealed class MarketAccessRuleService : IMarketAccessRuleService
{
    private readonly IMarketAccessRuleRepository _repo;
    public MarketAccessRuleService(IMarketAccessRuleRepository repo) => _repo = repo;
    public Task<MarketAccessRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<MarketAccessRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateMarketAccessRuleRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateMarketAccessRuleRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class CarrierDownloadMappingService : ICarrierDownloadMappingService
{
    private readonly ICarrierDownloadMappingRepository _repo;
    public CarrierDownloadMappingService(ICarrierDownloadMappingRepository repo) => _repo = repo;
    public Task<CarrierDownloadMappingDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<CarrierDownloadMappingDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateCarrierDownloadMappingRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateCarrierDownloadMappingRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class CarrierRuleCategoryService : ICarrierRuleCategoryService
{
    private readonly ICarrierRuleCategoryRepository _repo;
    public CarrierRuleCategoryService(ICarrierRuleCategoryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<CarrierRuleCategoryDto>> GetActiveAsync(CancellationToken ct = default) => _repo.GetActiveAsync(ct);
}

public sealed class CarrierRuleLookupService : ICarrierRuleLookupService
{
    private readonly ICarrierRuleLookupRepository _repo;
    public CarrierRuleLookupService(ICarrierRuleLookupRepository repo) => _repo = repo;
    public Task<IReadOnlyList<CarrierRuleOptionDto>> GetOptionsAsync(Guid tenantId, string optionType, CancellationToken ct = default) => _repo.GetOptionsAsync(tenantId, optionType, ct);
    public Task<IReadOnlyList<CarrierProductCatalogDto>> GetProductsAsync(Guid tenantId, Guid? carrierId, Guid? lineOfBusinessId, CancellationToken ct = default) => _repo.GetProductsAsync(tenantId, carrierId, lineOfBusinessId, ct);
}

public sealed class CarrierProductRuleService : ICarrierProductRuleService
{
    private readonly ICarrierProductRuleRepository _repo;
    public CarrierProductRuleService(ICarrierProductRuleRepository repo) => _repo = repo;
    public Task<CarrierProductRuleDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(tenantId, id, ct);
    public Task<PagedResult<CarrierProductRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, string? categoryCode, bool? isActive, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, categoryCode, isActive, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateCarrierProductRuleRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid tenantId, Guid id, UpdateCarrierProductRuleRequest request, CancellationToken ct = default) => _repo.UpdateAsync(tenantId, id, request, ct);
    public Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default) => _repo.DeleteAsync(tenantId, id, ct);
}
