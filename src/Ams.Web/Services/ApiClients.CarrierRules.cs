using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CarrierRules;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<MarketAccessRuleDto>?> SearchMarketAccessRulesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MarketAccessRuleDto>>($"api/carriers/access-rules?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateMarketAccessRuleAsync(CreateMarketAccessRuleRequest request, CancellationToken ct = default) { var r = await _httpClient.PostAsJsonAsync("api/carriers/access-rules", request, ct); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id; }
    public async Task UpdateMarketAccessRuleAsync(Guid id, UpdateMarketAccessRuleRequest request, CancellationToken ct = default) { (await _httpClient.PutAsJsonAsync($"api/carriers/access-rules/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteMarketAccessRuleAsync(Guid id, CancellationToken ct = default) { (await _httpClient.DeleteAsync($"api/carriers/access-rules/{id}", ct)).EnsureSuccessStatusCode(); }

    public Task<PagedResult<CarrierDownloadMappingDto>?> SearchCarrierDownloadMappingsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CarrierDownloadMappingDto>>($"api/carriers/download-mappings?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateCarrierDownloadMappingAsync(CreateCarrierDownloadMappingRequest request, CancellationToken ct = default) { var r = await _httpClient.PostAsJsonAsync("api/carriers/download-mappings", request, ct); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id; }
    public async Task UpdateCarrierDownloadMappingAsync(Guid id, UpdateCarrierDownloadMappingRequest request, CancellationToken ct = default) { (await _httpClient.PutAsJsonAsync($"api/carriers/download-mappings/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteCarrierDownloadMappingAsync(Guid id, CancellationToken ct = default) { (await _httpClient.DeleteAsync($"api/carriers/download-mappings/{id}", ct)).EnsureSuccessStatusCode(); }

    public Task<IReadOnlyList<CarrierRuleCategoryDto>?> GetCarrierRuleCategoriesAsync(CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<CarrierRuleCategoryDto>>("api/carriers/rule-categories", ct);

    public Task<IReadOnlyList<CarrierRuleOptionDto>?> GetCarrierRuleOptionsAsync(Guid tenantId, string optionType, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<CarrierRuleOptionDto>>($"api/carriers/rule-lookups/options?tenantId={tenantId}&optionType={Uri.EscapeDataString(optionType)}", ct);

    public Task<IReadOnlyList<CarrierProductCatalogDto>?> GetCarrierRuleProductsAsync(Guid tenantId, Guid? carrierId = null, Guid? lineOfBusinessId = null, CancellationToken ct = default)
    {
        var url = $"api/carriers/rule-lookups/products?tenantId={tenantId}";
        if (carrierId.HasValue) url += $"&carrierId={carrierId.Value}";
        if (lineOfBusinessId.HasValue) url += $"&lineOfBusinessId={lineOfBusinessId.Value}";
        return _httpClient.GetFromJsonAsync<IReadOnlyList<CarrierProductCatalogDto>>(url, ct);
    }

    public Task<PagedResult<CarrierProductRuleDto>?> SearchCarrierProductRulesAsync(Guid tenantId, string? searchTerm = null, string? categoryCode = null, bool? isActive = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var url = $"api/carriers/product-rules?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&categoryCode={Uri.EscapeDataString(categoryCode ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (isActive.HasValue)
        {
            url += $"&isActive={isActive.Value.ToString().ToLowerInvariant()}";
        }
        return _httpClient.GetFromJsonAsync<PagedResult<CarrierProductRuleDto>>(url, ct);
    }

    public async Task<Guid> CreateCarrierProductRuleAsync(CreateCarrierProductRuleRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/carriers/product-rules", request, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))?.Id
            ?? throw new InvalidOperationException("The carrier rule API did not return the created rule identifier.");
    }

    public async Task UpdateCarrierProductRuleAsync(Guid tenantId, Guid id, UpdateCarrierProductRuleRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/carriers/product-rules/{id}?tenantId={tenantId}", request, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task DeleteCarrierProductRuleAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/carriers/product-rules/{id}?tenantId={tenantId}", ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }
}
