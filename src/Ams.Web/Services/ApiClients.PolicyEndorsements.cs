using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyEndorsements;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PolicyEndorsementCenterDto?> GetPolicyEndorsementCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementCenterDto>($"api/policy-endorsements/center?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<PolicyEndorsementOptionDto>?> GetPolicyEndorsementOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyEndorsementOptionDto>>($"api/policy-endorsements/options?tenantId={tenantId}", cancellationToken);

    public Task<PolicyEndorsementDetailDto?> GetPolicyEndorsementDetailAsync(Guid endorsementId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementDetailDto>($"api/policy-endorsements/{endorsementId}", cancellationToken);

    public async Task<Guid> CreatePolicyEndorsementAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-endorsements", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdatePolicyEndorsementAsync(Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policy-endorsements/{endorsementId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePolicyEndorsementStatusAsync(Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/policy-endorsements/{endorsementId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddPolicyEndorsementActivityAsync(AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-endorsements/activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<Guid> UpsertPolicyEndorsementDeltaAsync(UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-endorsements/deltas", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ArchivePolicyEndorsementAsync(Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        var query = modifiedByUserId is null ? string.Empty : $"?modifiedByUserId={modifiedByUserId}";
        var response = await _httpClient.DeleteAsync($"api/policy-endorsements/{endorsementId}{query}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
