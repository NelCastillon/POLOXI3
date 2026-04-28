using System.Net.Http.Json;
using Ams.Application.Common.Dtos;

namespace Ams.Web.Services;

public sealed class ProducerWorkbenchApiClient
{
    private readonly HttpClient _httpClient;

    public ProducerWorkbenchApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ProducerWorkbenchDto?> GetWorkbenchAsync(Guid tenantId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/workbench/producer?tenantId={tenantId}";
        if (userId.HasValue) url += $"&userId={userId.Value}";
        return _httpClient.GetFromJsonAsync<ProducerWorkbenchDto>(url, cancellationToken);
    }

    public async Task<string> GetNextLeadNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetFromJsonAsync<string>(
            $"api/workbench/producer/next-lead-number?tenantId={tenantId}", cancellationToken);
        return result ?? string.Empty;
    }

    public async Task LogContactAsync(Guid tenantId, Guid itemId, string itemType, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/workbench/producer/log-contact?tenantId={tenantId}&itemId={itemId}&itemType={Uri.EscapeDataString(itemType)}",
            null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
