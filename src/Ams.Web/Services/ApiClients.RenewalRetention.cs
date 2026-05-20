using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.RenewalRetention;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<RenewalRetentionCenterDto?> GetRenewalRetentionCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<RenewalRetentionCenterDto>($"api/renewal-retention/center?tenantId={tenantId}", cancellationToken);

    public Task<RenewalRetentionDetailDto?> GetRenewalRetentionDetailAsync(Guid retentionCaseId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<RenewalRetentionDetailDto>($"api/renewal-retention/cases/{retentionCaseId}", cancellationToken);

    public async Task<Guid> CreateRenewalRetentionCaseAsync(CreateRenewalRetentionCaseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/renewal-retention/cases", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateRenewalRetentionStageAsync(Guid retentionCaseId, UpdateRenewalRetentionStageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/renewal-retention/cases/{retentionCaseId}/stage", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddRenewalRetentionActivityAsync(CreateRenewalRetentionActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/renewal-retention/activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<Guid> AddRenewalRetentionOfferAsync(CreateRenewalRetentionOfferRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/renewal-retention/offers", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateRenewalRetentionOfferStatusAsync(Guid retentionOfferId, UpdateRenewalRetentionOfferStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/renewal-retention/offers/{retentionOfferId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
