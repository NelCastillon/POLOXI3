using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AiConfig;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<AiConfigItemDto>?> SearchAiConfigItemsAsync(Guid tenantId, string kind, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AiConfigItemDto>>($"api/ai-config?tenantId={tenantId}&kind={Uri.EscapeDataString(kind)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public async Task<Guid> CreateAiConfigItemAsync(CreateAiConfigItemRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/ai-config", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }

    public async Task UpdateAiConfigItemAsync(Guid id, UpdateAiConfigItemRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/ai-config/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteAiConfigItemAsync(Guid id, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/ai-config/{id}", ct)).EnsureSuccessStatusCode();
}
