using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DataConfig;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<DataConfigItemDto>?> SearchDataConfigItemsAsync(Guid tenantId, string kind, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DataConfigItemDto>>($"api/data-config?tenantId={tenantId}&kind={Uri.EscapeDataString(kind)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public async Task<Guid> CreateDataConfigItemAsync(CreateDataConfigItemRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/data-config", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }

    public async Task UpdateDataConfigItemAsync(Guid id, UpdateDataConfigItemRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/data-config/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteDataConfigItemAsync(Guid id, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/data-config/{id}", ct)).EnsureSuccessStatusCode();
}
