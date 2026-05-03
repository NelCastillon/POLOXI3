using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantSettings;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<IReadOnlyList<SubscriptionSettingsWorkflowItemDto>?> GetSubscriptionSettingsWorkflowAsync(Guid tenantId, string pageCode, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<SubscriptionSettingsWorkflowItemDto>>($"api/subscription-settings/workflow?tenantId={tenantId}&pageCode={Uri.EscapeDataString(pageCode)}", cancellationToken);

    public async Task<Guid> CreateSubscriptionSettingsWorkflowAsync(CreateSubscriptionSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/subscription-settings/workflow", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateSubscriptionSettingsWorkflowAsync(Guid workflowItemId, UpdateSubscriptionSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/subscription-settings/workflow/{workflowItemId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AdvanceSubscriptionSettingsWorkflowAsync(Guid workflowItemId, AdvanceSubscriptionSettingsWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/subscription-settings/workflow/{workflowItemId}/advance", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSubscriptionSettingsWorkflowAsync(Guid workflowItemId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/subscription-settings/workflow/{workflowItemId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
