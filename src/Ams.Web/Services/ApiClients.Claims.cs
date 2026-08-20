using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Claims;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<ClaimDto>?> SearchClaimsAsync(Guid tenantId, string? searchTerm = null, string? status = null, string? lob = null, string? catCode = null, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ClaimDto>>($"api/claims?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}&lob={Uri.EscapeDataString(lob ?? string.Empty)}&catCode={Uri.EscapeDataString(catCode ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<ClaimDetailDto?> GetClaimDetailAsync(Guid tenantId, Guid claimId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ClaimDetailDto>($"api/claims/{claimId}?tenantId={tenantId}", cancellationToken);

    public Task<List<ClaimOptionDto>?> GetClaimOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<ClaimOptionDto>>($"api/claims/options?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreateClaimAsync(CreateClaimRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/claims", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public Task<Guid> AssignClaimAdjusterAsync(AssignClaimAdjusterRequest request, CancellationToken cancellationToken = default)
        => SendForIdAsync(HttpMethod.Post, $"api/claims/{request.ClaimId}/adjusters", request, cancellationToken);

    public Task<Guid> UpsertClaimPartyAsync(UpsertClaimPartyRequest request, CancellationToken cancellationToken = default)
        => SendForIdAsync(HttpMethod.Put, $"api/claims/{request.ClaimId}/parties", request, cancellationToken);

    public Task<Guid> CreateClaimFinancialTransactionAsync(CreateClaimFinancialTransactionRequest request, CancellationToken cancellationToken = default)
        => SendForIdAsync(HttpMethod.Post, $"api/claims/{request.ClaimId}/financial-transactions", request, cancellationToken);

    public Task<Guid> ReverseClaimFinancialTransactionAsync(ReverseClaimFinancialTransactionRequest request, CancellationToken cancellationToken = default)
        => SendForIdAsync(HttpMethod.Post, $"api/claims/financial-transactions/{request.ClaimFinancialTransactionId}/reverse", request, cancellationToken);

    public Task<Guid> CreateClaimNoteAsync(CreateClaimNoteRequest request, CancellationToken cancellationToken = default)
        => SendForIdAsync(HttpMethod.Post, $"api/claims/{request.ClaimId}/notes", request, cancellationToken);

    public Task<Guid> CreateClaimTaskAsync(CreateClaimTaskRequest request, CancellationToken cancellationToken = default)
        => SendForIdAsync(HttpMethod.Post, $"api/claims/{request.ClaimId}/tasks", request, cancellationToken);

    public async Task CompleteClaimTaskAsync(CompleteClaimTaskRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/claims/tasks/{request.ClaimTaskId}/complete", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<Guid> LinkClaimDocumentAsync(LinkClaimDocumentRequest request, CancellationToken cancellationToken = default)
        => SendForIdAsync(HttpMethod.Post, $"api/claims/{request.ClaimId}/documents", request, cancellationToken);

    public async Task<LossRunImportResultDto?> ImportLossRunAsync(ImportLossRunRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/claims/loss-runs/import", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LossRunImportResultDto>(cancellationToken: cancellationToken);
    }

    public Task<List<LossRunDto>?> GetLossRunsAsync(Guid tenantId, Guid? accountId = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<LossRunDto>>($"api/claims/loss-runs?tenantId={tenantId}&accountId={accountId}", cancellationToken);

    public async Task UpdateClaimStatusAsync(Guid claimId, UpdateClaimStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/claims/{claimId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateClaimFollowUpAsync(Guid claimId, UpdateClaimFollowUpRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/claims/{claimId}/follow-up", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddClaimActivityAsync(CreateClaimActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/claims/activity", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public Task<PagedResult<CatEventDto>?> SearchCatEventsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CatEventDto>>($"api/claims/cat/events?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreateCatEventAsync(CreateCatEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/claims/cat/events", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public Task<CatastrophePageDto?> GetCatastrophePageAsync(Guid tenantId, Guid? catEventId = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<CatastrophePageDto>($"api/claims/cat/page?tenantId={tenantId}&catEventId={catEventId}", cancellationToken);

    public async Task MarkAffectedInsuredContactedAsync(Guid affectedInsuredId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/claims/cat/affected/{affectedInsuredId}/contacted", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> ApplyGeoTagAsync(Guid catEventId, string? states, string? counties, string? zips, string? lob, decimal? minTiv, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/claims/cat/events/{catEventId}/geo-tag", new { States = states, Counties = counties, Zips = zips, Lob = lob, MinTiv = minTiv }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GeoTagResult>(cancellationToken: cancellationToken);
        return result?.Count ?? 0;
    }

    public async Task<int> SendCatBlastAsync(CatBlastRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/claims/cat/events/{request.CatEventId}/blast", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GeoTagResult>(cancellationToken: cancellationToken);
        return result?.Count ?? 0;
    }

    public async Task<Guid> CreateFastCatFnolAsync(FastCatFnolRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/claims/cat/events/{request.CatEventId}/fast-fnol", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    private sealed class GeoTagResult
    {
        public int Count { get; set; }
    }

    private async Task<Guid> SendForIdAsync<T>(HttpMethod method, string uri, T request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(request) };
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }
}
