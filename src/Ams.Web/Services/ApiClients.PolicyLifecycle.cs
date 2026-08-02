using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyLifecycle;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<IReadOnlyList<PolicyLifecycleOptionDto>?> GetPolicyLifecycleOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyLifecycleOptionDto>>($"api/policy-lifecycle/options?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<PolicyLifecycleWorkbenchRowDto>?> GetPolicyLifecycleWorkbenchAsync(Guid tenantId, string? mode = null, CancellationToken cancellationToken = default)
    {
        var modeQuery = string.IsNullOrWhiteSpace(mode) ? string.Empty : $"&mode={Uri.EscapeDataString(mode)}";
        return _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyLifecycleWorkbenchRowDto>>($"api/policy-lifecycle/workbench?tenantId={tenantId}{modeQuery}", cancellationToken);
    }

    public Task<PolicyLifecycleDetailDto?> GetPolicyLifecycleDetailAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyLifecycleDetailDto>($"api/policy-lifecycle/policies/{policyId}?tenantId={tenantId}", cancellationToken);

    public Task<PolicyServicingWorkspaceDto?> GetPolicyServicingWorkspaceAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyServicingWorkspaceDto>($"api/policy-lifecycle/policies/{policyId}/workspace?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreatePolicyLifecycleTransactionAsync(CreatePolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-lifecycle/transactions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task TransitionPolicyLifecycleTransactionAsync(Guid policyTransactionId, TransitionPolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policy-lifecycle/transactions/{policyTransactionId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PolicyServicingActionResultDto> CreatePolicyServicingActivityAsync(CreatePolicyServicingActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-lifecycle/policies/{request.PolicyId}/activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PolicyServicingActionResultDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<PolicyServicingActionResultDto> SendPolicyCommunicationAsync(SendPolicyCommunicationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-lifecycle/policies/{request.PolicyId}/communications", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PolicyServicingActionResultDto>(cancellationToken: cancellationToken))!;
    }
}
