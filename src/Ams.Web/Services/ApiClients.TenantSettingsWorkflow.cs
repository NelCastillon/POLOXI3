using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantSettings;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<IReadOnlyList<TenantSettingsWorkflowItemDto>?> GetTenantSettingsWorkflowAsync(Guid tenantId, string pageCode, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<TenantSettingsWorkflowItemDto>>($"api/tenant-settings/workflow?tenantId={tenantId}&pageCode={Uri.EscapeDataString(pageCode)}", cancellationToken);

    public async Task<Guid> CreateTenantSettingsWorkflowAsync(CreateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tenant-settings/workflow", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateTenantSettingsWorkflowAsync(Guid workflowItemId, UpdateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tenant-settings/workflow/{workflowItemId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AdvanceTenantSettingsWorkflowAsync(Guid workflowItemId, AdvanceTenantSettingsWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/tenant-settings/workflow/{workflowItemId}/advance", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTenantSettingsWorkflowAsync(Guid workflowItemId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tenant-settings/workflow/{workflowItemId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
