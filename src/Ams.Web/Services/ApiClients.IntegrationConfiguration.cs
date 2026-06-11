using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.IntegrationConfig;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<CarrierIntegrationStatusDto>?> SearchCarrierIntegrationStatusesAsync(Guid tenantId, int pageNumber = 1, int pageSize = 100, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CarrierIntegrationStatusDto>?>($"api/integrations/carriers?tenantId={tenantId}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public Task<CarrierIntegrationStatusDto?> GetCarrierIntegrationStatusByIdAsync(Guid id, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<CarrierIntegrationStatusDto?>($"api/integrations/carriers/{id}", ct);

    public Task<PagedResult<IntegrationConfigItemDto>?> SearchIntegrationConfigItemsAsync(Guid tenantId, string kind, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<IntegrationConfigItemDto>>($"api/integration-config?tenantId={tenantId}&kind={Uri.EscapeDataString(kind)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public async Task<Guid> CreateIntegrationConfigItemAsync(CreateIntegrationConfigItemRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/integration-config", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }

    public async Task UpdateIntegrationConfigItemAsync(Guid id, UpdateIntegrationConfigItemRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/integration-config/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteIntegrationConfigItemAsync(Guid id, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/integration-config/{id}", ct)).EnsureSuccessStatusCode();
}
