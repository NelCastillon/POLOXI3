using System.Net.Http.Json;
using Ams.Application.Features.SearchMatching;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public async Task<EntityMatchResult?> FindEntityMatchesAsync(EntityMatchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/search-matching/match", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EntityMatchResult>(cancellationToken: cancellationToken);
    }

    public async Task<EntityMatchResult?> FindModuleMatchesAsync(ModuleMatchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/search-matching/module-match", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EntityMatchResult>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<SearchMatchResult>> SearchMatchingAsync(EnterpriseFuzzySearchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/search-matching/search", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SearchMatchResult>>(cancellationToken: cancellationToken) ?? [];
    }

    public Task<MatchPolicy?> GetMatchProfileAsync(string profileCode, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<MatchPolicy>($"api/search-matching/profiles/{Uri.EscapeDataString(profileCode)}", cancellationToken);

    public async Task<MatchReviewDecision?> SaveMatchReviewDecisionAsync(MatchReviewDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/search-matching/review-decisions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MatchReviewDecision>(cancellationToken: cancellationToken);
    }

    public Task<List<MatchReviewDecision>?> GetMatchReviewDecisionsAsync(Guid matchExecutionId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<MatchReviewDecision>>($"api/search-matching/executions/{matchExecutionId}/review-decisions", cancellationToken);

    public Task<SearchMatchingAdministration?> GetSearchMatchingAdministrationAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SearchMatchingAdministration>("api/search-matching/administration", cancellationToken);

    public Task SaveMatchProfileSettingAsync(SaveMatchProfileSettingRequest request, CancellationToken cancellationToken = default)
        => PostSearchSettingAsync("profiles", request, cancellationToken);

    public Task SaveMatchFieldRuleSettingAsync(SaveMatchFieldRuleSettingRequest request, CancellationToken cancellationToken = default)
        => PostSearchSettingAsync("field-rules", request, cancellationToken);

    public Task SaveMatchAlgorithmSettingAsync(SaveMatchAlgorithmSettingRequest request, CancellationToken cancellationToken = default)
        => PostSearchSettingAsync("algorithms", request, cancellationToken);

    public Task SaveNormalizationTermSettingAsync(SaveNormalizationTermSettingRequest request, CancellationToken cancellationToken = default)
        => PostSearchSettingAsync("normalization-terms", request, cancellationToken);

    public Task DeleteMatchProfileSettingAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken = default) => DeleteSearchSettingAsync("profiles", id, rowVersion, cancellationToken);
    public Task DeleteMatchFieldRuleSettingAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken = default) => DeleteSearchSettingAsync("field-rules", id, rowVersion, cancellationToken);
    public Task DeleteMatchAlgorithmSettingAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken = default) => DeleteSearchSettingAsync("algorithms", id, rowVersion, cancellationToken);
    public Task DeleteNormalizationTermSettingAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken = default) => DeleteSearchSettingAsync("normalization-terms", id, rowVersion, cancellationToken);

    private async Task PostSearchSettingAsync<T>(string resource, T request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/search-matching/administration/{resource}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteSearchSettingAsync(string resource, Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var token = Uri.EscapeDataString(Convert.ToBase64String(rowVersion));
        var response = await _httpClient.DeleteAsync($"api/search-matching/administration/{resource}/{id}?rowVersion={token}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
