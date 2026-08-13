using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyChecks;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PolicyCheckCenterDto?> GetPolicyCheckCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyCheckCenterDto>($"api/policy-checks/center?tenantId={tenantId}", cancellationToken);

    public Task<PolicyCheckDetailDto?> GetPolicyCheckDetailAsync(Guid policyCheckId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyCheckDetailDto>($"api/policy-checks/{policyCheckId}", cancellationToken);

    public async Task<Guid> CreatePolicyCheckAsync(CreatePolicyCheckRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-checks", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdatePolicyCheckAsync(Guid policyCheckId, UpdatePolicyCheckRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policy-checks/{policyCheckId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePolicyCheckStatusAsync(Guid policyCheckId, UpdatePolicyCheckStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/policy-checks/{policyCheckId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePolicyCheckItemAsync(Guid policyCheckItemId, UpdatePolicyCheckItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/policy-checks/items/{policyCheckItemId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddPolicyCheckDiscrepancyAsync(AddPolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-checks/discrepancies", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ResolvePolicyCheckDiscrepancyAsync(Guid discrepancyId, ResolvePolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/policy-checks/discrepancies/{discrepancyId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddPolicyCheckActivityAsync(AddPolicyCheckActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-checks/activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ArchivePolicyCheckAsync(Guid policyCheckId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        var query = modifiedByUserId is null ? string.Empty : $"?modifiedByUserId={modifiedByUserId}";
        var response = await _httpClient.DeleteAsync($"api/policy-checks/{policyCheckId}{query}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
