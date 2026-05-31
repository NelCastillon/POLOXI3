using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;
using Ams.Application.Features.SubmissionIntake;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<SubmissionIntakeDto>?> SearchSubmissionIntakesAsync(Guid tenantId, string? searchTerm = null, string? status = null, string? source = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SubmissionIntakeDto>>($"api/submission-intake?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}&source={Uri.EscapeDataString(source ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<SubmissionIntakeDto?> GetSubmissionIntakeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SubmissionIntakeDto>($"api/submission-intake/{id}", cancellationToken);

    public async Task<Guid> CaptureSubmissionIntakeAsync(CreateSubmissionIntakeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/submission-intake", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateSubmissionIntakeAsync(Guid id, UpdateSubmissionIntakeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/submission-intake/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<AccountMatchResult?> PreviewSubmissionIntakeMatchAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AccountMatchResult>($"api/submission-intake/{id}/match", cancellationToken);

    public async Task<PromoteSubmissionIntakeResult> PromoteSubmissionIntakeAsync(Guid id, PromoteSubmissionIntakeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/submission-intake/{id}/promote", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PromoteSubmissionIntakeResult>(cancellationToken: cancellationToken))!;
    }

    public async Task UpdateSubmissionIntakeStatusAsync(Guid id, UpdateSubmissionIntakeStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/submission-intake/{id}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSubmissionIntakeAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var url = userId.HasValue ? $"api/submission-intake/{id}?userId={userId.Value}" : $"api/submission-intake/{id}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
