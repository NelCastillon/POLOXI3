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
