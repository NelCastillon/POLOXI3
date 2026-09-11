using System.Net.Http.Json;
using Legal.Application.Features.Intelligence;

namespace Legal.Web.Services;

// Typed HTTP client for the Legal.Api host. Only the endpoints used by the Legal
// search and configuration pages are exposed.
public sealed class ApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient=httpClient;

    // ── POLOXI Wide search (start+poll transport) ─────────────────────────────
    public async Task<WideSearchResponse?> IntelligentSearchWideDynamicAsync(WideSearchRequest request,CancellationToken token=default)
    {
        using var startResponse=await _httpClient.PostAsJsonAsync("api/intelligence_wide/search/dynamic/start",request,token);
        startResponse.EnsureSuccessStatusCode();
        var start=await startResponse.Content.ReadFromJsonAsync<WideSearchOperationStartResponse>(cancellationToken:token)??throw new InvalidOperationException("The wide search operation could not be started.");
        try
        {
            while(true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2),token);
                var status=await _httpClient.GetFromJsonAsync<WideSearchOperationStatusResponse>($"api/intelligence_wide/search/dynamic/status/{start.OperationId}",token)??throw new InvalidOperationException("The wide search operation is no longer available.");
                if(status.StatusCode=="COMPLETED")return status.Response??throw new InvalidOperationException("The wide search completed without a result.");
                if(status.StatusCode=="CANCELLED")throw new OperationCanceledException("The wide search was cancelled.");
                if(status.StatusCode=="FAILED")throw new InvalidOperationException(status.ErrorMessage??"The wide search failed.");
                if(status.StatusCode!="RUNNING")throw new InvalidOperationException($"The wide search returned an unexpected status: {status.StatusCode}.");
            }
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested)
        {
            using var stopTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                using var stop=await _httpClient.PostAsync($"api/intelligence_wide/search/dynamic/cancel/{start.OperationId}",null,stopTimeout.Token);
                await EnsureSuccessWithDetailAsync(stop,stopTimeout.Token);
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException($"Stopped waiting for results, but server cancellation could not be confirmed: {ex.Message}",ex);
            }
            throw;
        }
    }

    public async Task<IReadOnlyCollection<WideModelOptionDto>> GetIntelligenceWideModelsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<WideModelOptionDto>>("api/intelligence_wide/models",token)??[];

    public async Task<IReadOnlyCollection<WideSearchContextDto>> GetIntelligenceSearchContextsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<WideSearchContextDto>>("api/intelligence_wide/contexts",token)??[];

    // ── Configuration center ──────────────────────────────────────────────────
    public Task<IntelligencePlatformSummaryDto?> GetIntelligencePlatformAsync(CancellationToken token=default)=>_httpClient.GetFromJsonAsync<IntelligencePlatformSummaryDto>("api/intelligence/platform",token);
    public async Task<IReadOnlyCollection<AiProviderDto>> GetIntelligenceProvidersAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<AiProviderDto>>("api/intelligence/providers",token)??[];
    public async Task SaveIntelligenceProviderAsync(string providerCode,SaveAiProviderRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/providers/{Uri.EscapeDataString(providerCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteIntelligenceProviderAsync(string providerCode,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/providers/{Uri.EscapeDataString(providerCode)}",token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task<IReadOnlyCollection<AiModelDeploymentDto>> GetIntelligenceModelsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<AiModelDeploymentDto>>("api/intelligence/models",token)??[];
    public async Task SaveIntelligenceModelDeploymentAsync(string modelCode,SaveAiModelDeploymentRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/models/{Uri.EscapeDataString(modelCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteIntelligenceModelDeploymentAsync(string modelCode,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/models/{Uri.EscapeDataString(modelCode)}",token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task<IReadOnlyCollection<AiFeaturePolicyDto>> GetIntelligenceFeaturePoliciesAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<AiFeaturePolicyDto>>("api/intelligence/feature-policies",token)??[];
    public async Task SaveIntelligenceFeaturePolicyAsync(string featureCode,SaveAiFeaturePolicyRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/feature-policies/{Uri.EscapeDataString(featureCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteIntelligenceFeaturePolicyAsync(string featureCode,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/feature-policies/{Uri.EscapeDataString(featureCode)}",token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task<IReadOnlyCollection<IntelligencePromptDefinitionDto>> GetIntelligencePromptDefinitionsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<IntelligencePromptDefinitionDto>>("api/intelligence/prompts",token)??[];
    public async Task SaveIntelligencePromptDefinitionAsync(string promptCode,string versionLabel,SaveIntelligencePromptDefinitionRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/prompts/{Uri.EscapeDataString(promptCode)}/{Uri.EscapeDataString(versionLabel)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteIntelligencePromptDefinitionAsync(string promptCode,string versionLabel,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/prompts/{Uri.EscapeDataString(promptCode)}/{Uri.EscapeDataString(versionLabel)}",token);await EnsureSuccessWithDetailAsync(response,token);}

    // Legal Grounding settings (CourtListener/GovInfo/eCFR) stored in Core.ConfigurationSetting.
    public async Task<IReadOnlyCollection<LegalGroundingSettingDto>> GetLegalGroundingSettingsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<LegalGroundingSettingDto>>("api/intelligence/legal-grounding-settings",token)??[];
    public async Task SaveLegalGroundingSettingAsync(string settingKey,SaveLegalGroundingSettingRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/legal-grounding-settings/{Uri.EscapeDataString(settingKey)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteLegalGroundingSettingAsync(string settingKey,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/legal-grounding-settings/{Uri.EscapeDataString(settingKey)}",token);await EnsureSuccessWithDetailAsync(response,token);}

    private static async Task EnsureSuccessWithDetailAsync(HttpResponseMessage response,CancellationToken token){if(response.IsSuccessStatusCode)return;var detail=await response.Content.ReadAsStringAsync(token);throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)?$"Request failed with status {(int)response.StatusCode}.":detail);}
}
