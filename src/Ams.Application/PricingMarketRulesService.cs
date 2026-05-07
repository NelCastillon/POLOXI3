using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class PricingMarketRulesService : IPricingMarketRulesService
{
    private readonly IPricingMarketRulesRepository _repository;

    public PricingMarketRulesService(IPricingMarketRulesRepository repository) => _repository = repository;

    public Task<PagedResult<PriceClassDto>> SearchPriceClassesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _repository.SearchPriceClassesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreatePriceClassAsync(UpsertPriceClassRequest request, CancellationToken cancellationToken = default)
        => _repository.CreatePriceClassAsync(request, cancellationToken);
    public Task UpdatePriceClassAsync(Guid id, UpsertPriceClassRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdatePriceClassAsync(id, request, cancellationToken);
    public Task DeletePriceClassAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
        => _repository.DeletePriceClassAsync(id, userId, cancellationToken);

    public Task<PagedResult<MarketAppetiteDto>> SearchMarketAppetiteAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _repository.SearchMarketAppetiteAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateMarketAppetiteAsync(UpsertMarketAppetiteRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateMarketAppetiteAsync(request, cancellationToken);
    public Task UpdateMarketAppetiteAsync(Guid id, UpsertMarketAppetiteRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateMarketAppetiteAsync(id, request, cancellationToken);
    public Task DeleteMarketAppetiteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteMarketAppetiteAsync(id, userId, cancellationToken);

    public Task<PagedResult<CarrierMappingDto>> SearchCarrierMappingsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _repository.SearchCarrierMappingsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateCarrierMappingAsync(UpsertCarrierMappingRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateCarrierMappingAsync(request, cancellationToken);
    public Task UpdateCarrierMappingAsync(Guid id, UpsertCarrierMappingRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateCarrierMappingAsync(id, request, cancellationToken);
    public Task DeleteCarrierMappingAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteCarrierMappingAsync(id, userId, cancellationToken);
    public Task TestCarrierMappingAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
        => _repository.TestCarrierMappingAsync(id, userId, cancellationToken);
}
