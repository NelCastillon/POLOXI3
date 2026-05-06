using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.SubscriptionConfig;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<SubscriptionConfigItemDto>?> SearchSubscriptionConfigItemsAsync(Guid tenantId, string kind, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SubscriptionConfigItemDto>>($"api/subscription-config?tenantId={tenantId}&kind={Uri.EscapeDataString(kind)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public async Task<Guid> CreateSubscriptionConfigItemAsync(CreateSubscriptionConfigItemRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/subscription-config", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }

    public async Task UpdateSubscriptionConfigItemAsync(Guid id, UpdateSubscriptionConfigItemRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/subscription-config/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteSubscriptionConfigItemAsync(Guid id, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/subscription-config/{id}", ct)).EnsureSuccessStatusCode();
}
