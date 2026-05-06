using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PortalConfig;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<PortalConfigItemDto>?> SearchPortalConfigItemsAsync(Guid tenantId, string kind, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalConfigItemDto>>($"api/portal-config?tenantId={tenantId}&kind={Uri.EscapeDataString(kind)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public async Task<Guid> CreatePortalConfigItemAsync(CreatePortalConfigItemRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/portal-config", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }

    public async Task UpdatePortalConfigItemAsync(Guid id, UpdatePortalConfigItemRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/portal-config/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeletePortalConfigItemAsync(Guid id, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/portal-config/{id}", ct)).EnsureSuccessStatusCode();
}
