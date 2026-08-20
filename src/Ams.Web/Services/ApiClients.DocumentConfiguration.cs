using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentConfig;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<DocumentConfigItemDto>?> SearchDocumentConfigItemsAsync(Guid tenantId, string? kind = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentConfigItemDto>>($"api/document-config?tenantId={tenantId}&kind={Uri.EscapeDataString(kind ?? string.Empty)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public Task<PagedResult<DocumentConfigItemDto>?> SearchDocumentGroupsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentConfigItemDto>>($"api/document-config/groups?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public async Task<Guid> CreateDocumentConfigItemAsync(CreateDocumentConfigItemRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/document-config", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }

    public async Task<Guid> CreateDocumentGroupAsync(CreateDocumentGroupRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/document-config/groups", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }

    public async Task UpdateDocumentConfigItemAsync(Guid id, UpdateDocumentConfigItemRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/document-config/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task UpdateDocumentGroupAsync(Guid id, UpdateDocumentGroupRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/document-config/groups/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteDocumentConfigItemAsync(Guid id, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/document-config/{id}", ct)).EnsureSuccessStatusCode();

    public async Task DeleteDocumentGroupAsync(Guid id, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/document-config/groups/{id}", ct)).EnsureSuccessStatusCode();
}
