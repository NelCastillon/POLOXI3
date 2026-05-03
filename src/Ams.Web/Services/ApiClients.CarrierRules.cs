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
}
