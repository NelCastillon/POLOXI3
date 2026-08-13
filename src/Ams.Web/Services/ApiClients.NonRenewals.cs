using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.NonRenewals;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<NonRenewalCenterDto?> GetNonRenewalCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<NonRenewalCenterDto>($"api/non-renewals/center?tenantId={tenantId}", cancellationToken);

    public Task<NonRenewalDetailDto?> GetNonRenewalDetailAsync(Guid nonRenewalId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<NonRenewalDetailDto>($"api/non-renewals/{nonRenewalId}", cancellationToken);

    public async Task<Guid> CreateNonRenewalAsync(CreateNonRenewalRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/non-renewals", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateNonRenewalAsync(Guid nonRenewalId, UpdateNonRenewalRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/non-renewals/{nonRenewalId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateNonRenewalStatusAsync(Guid nonRenewalId, UpdateNonRenewalStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/non-renewals/{nonRenewalId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RecordNonRenewalInsuredNotificationAsync(Guid nonRenewalId, RecordInsuredNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/non-renewals/{nonRenewalId}/insured-notification", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddNonRenewalActivityAsync(AddNonRenewalActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/non-renewals/activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ArchiveNonRenewalAsync(Guid nonRenewalId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        var query = modifiedByUserId is null ? string.Empty : $"?modifiedByUserId={modifiedByUserId}";
        var response = await _httpClient.DeleteAsync($"api/non-renewals/{nonRenewalId}{query}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
