using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Enterprise;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<AmsCapabilityPageDto?> SearchAmsCapabilitiesAsync(Guid tenantId, string? domainCode = null, string? statusCode = null, string? priorityCode = null, string? searchTerm = null, bool activeOnly = true, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<AmsCapabilityPageDto>($"api/enterprise/ams-capabilities?tenantId={tenantId}&domainCode={Uri.EscapeDataString(domainCode ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}&priorityCode={Uri.EscapeDataString(priorityCode ?? string.Empty)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&activeOnly={activeOnly}", ct);

    public Task<AmsCapabilityDto?> GetAmsCapabilityAsync(Guid capabilityId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<AmsCapabilityDto>($"api/enterprise/ams-capabilities/{capabilityId}", ct);

    public async Task UpdateAmsCapabilityAsync(Guid capabilityId, UpdateAmsCapabilityRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/enterprise/ams-capabilities/{capabilityId}", request, ct)).EnsureSuccessStatusCode();
}
