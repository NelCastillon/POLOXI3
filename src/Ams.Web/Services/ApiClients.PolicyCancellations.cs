using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyCancellations;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PolicyCancellationCenterDto?> GetPolicyCancellationCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyCancellationCenterDto>($"api/policy-cancellations/center?tenantId={tenantId}", cancellationToken);

    public Task<PolicyCancellationDetailDto?> GetPolicyCancellationDetailAsync(Guid cancellationId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyCancellationDetailDto>($"api/policy-cancellations/{cancellationId}", cancellationToken);

    public async Task<Guid> CreatePolicyCancellationAsync(CreatePolicyCancellationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-cancellations", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdatePolicyCancellationAsync(Guid cancellationId, UpdatePolicyCancellationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policy-cancellations/{cancellationId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePolicyCancellationStatusAsync(Guid cancellationId, UpdatePolicyCancellationStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/policy-cancellations/{cancellationId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddPolicyCancellationActivityAsync(AddPolicyCancellationActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-cancellations/activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ArchivePolicyCancellationAsync(Guid cancellationId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        var query = modifiedByUserId is null ? string.Empty : $"?modifiedByUserId={modifiedByUserId}";
        var response = await _httpClient.DeleteAsync($"api/policy-cancellations/{cancellationId}{query}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
