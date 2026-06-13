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

    public Task<ProducerRenewalCallListDto?> GetRenewalCallsAsync(Guid tenantId, Guid? userId = null, string? statusCode = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/workbench/producer/renewal-calls?tenantId={tenantId}";
        if (userId.HasValue) url += $"&userId={userId.Value}";
        if (!string.IsNullOrWhiteSpace(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        return _httpClient.GetFromJsonAsync<ProducerRenewalCallListDto>(url, cancellationToken);
    }

    public Task<ProducerRenewalCallDto?> GetRenewalCallAsync(Guid tenantId, Guid renewalKey, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ProducerRenewalCallDto>($"api/workbench/producer/renewal-calls/{renewalKey}?tenantId={tenantId}", cancellationToken);

    public async Task UpdateRenewalCallAsync(Guid renewalCallId, UpdateProducerRenewalCallRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/workbench/producer/renewal-calls/{renewalCallId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> GetNextLeadNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetFromJsonAsync<string>(
            $"api/workbench/producer/next-lead-number?tenantId={tenantId}", cancellationToken);
        return result ?? string.Empty;
    }

    public async Task LogContactAsync(ProducerWorkbenchLogContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/workbench/producer/log-contact", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
