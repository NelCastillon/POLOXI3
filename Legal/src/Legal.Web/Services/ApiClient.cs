using System.Net.Http.Json;
using Legal.Application.Abstractions.Persistence;
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

    // POLOXI Legal Decision Intelligence (/legal/decision) — self-contained module.
    public async Task<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse?> LegalDecideAsync(Legal.Application.Features.Intelligence.Decision.DecisionSearchRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/legal_decision/decide",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse>(cancellationToken:token);
    }

    public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionModelOptionDto>> GetLegalDecisionModelsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionModelOptionDto>>("api/legal_decision/models",token)??[];

    public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionContextDto>> GetLegalDecisionContextsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionContextDto>>("api/legal_decision/contexts",token)??[];

        // POLOXI Legal Decision cockpit — matter dashboard + timeline.
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionMatterDto>> GetLegalDecisionMattersAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionMatterDto>>("api/legal_decision/matters",token)??[];
        public async Task<Legal.Application.Features.Intelligence.Decision.DecisionMatterFacetsDto> GetLegalDecisionMatterFacetsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionMatterFacetsDto>("api/legal_decision/matters/facets",token)??new([],[],[]);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionMatterDto?> GetLegalDecisionMatterAsync(Guid matterId,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionMatterDto>($"api/legal_decision/matters/{matterId}",token);
        public async Task<Guid> CreateLegalDecisionMatterAsync(Legal.Application.Features.Intelligence.Decision.DecisionMatterCreateRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync("api/legal_decision/matters",request,token);
            await EnsureSuccessWithDetailAsync(response,token);
            var created=await response.Content.ReadFromJsonAsync<CreatedMatterResult>(cancellationToken:token);
            return created?.DecisionMatterId??Guid.Empty;
        }
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionTimelineEventDto>> GetLegalDecisionTimelineAsync(Guid sessionId,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionTimelineEventDto>>($"api/legal_decision/sessions/{sessionId}/timeline",token)??[];
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse?> GetLegalDecisionSessionAsync(Guid sessionId,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse>($"api/legal_decision/sessions/{sessionId}",token);
        public async Task<Legal.Application.Features.Intelligence.Decision.DecisionClosedLoopResultDto?> ApplyLegalDecisionVerificationAsync(Guid sessionId,Legal.Application.Features.Intelligence.Decision.DecisionVerificationChangeRequest request,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/sessions/{sessionId}/verify",request,token);response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionClosedLoopResultDto>(token);}
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionSessionSummaryDto>> GetLegalDecisionMatterSessionsAsync(Guid matterId,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionSessionSummaryDto>>($"api/legal_decision/matters/{matterId}/sessions",token)??[];
        public async Task UpdateLegalDecisionMatterAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionMatterUpdateRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PutAsJsonAsync($"api/legal_decision/matters/{matterId}",request,token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        public async Task UpdateLegalDecisionMatterStatusAsync(Guid matterId,string statusCode,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/status",new Legal.Application.Features.Intelligence.Decision.DecisionMatterStatusUpdateRequest(statusCode),token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        public async Task DeleteLegalDecisionMatterAsync(Guid matterId,CancellationToken token=default)
        {
            using var response=await _httpClient.DeleteAsync($"api/legal_decision/matters/{matterId}",token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        private sealed record CreatedMatterResult(Guid DecisionMatterId);

    public async Task<IReadOnlyList<MathExecutionSummary>> GetMathRunsAsync(int take=50,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyList<MathExecutionSummary>>($"api/intelligence_math/runs?take={take}",token)??[];
    public Task<MathExecutionDetail?> GetMathRunAsync(Guid mathExecutionId,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<MathExecutionDetail>($"api/intelligence_math/runs/{mathExecutionId}",token);

    // ── POLOXI Math solve (deterministic verification pipeline) ──────────────
    public async Task<Legal.Application.Features.Intelligence.Science.MathSolveResponse?> SolveMathAsync(Legal.Application.Features.Intelligence.Science.MathSolveRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/intelligence_math/solve",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Science.MathSolveResponse>(cancellationToken:token);
    }

    // ── POLOXI Formalization Gate (Research → Formalize → Math handoff) ──────────
    public async Task<Legal.Application.Features.Intelligence.Science.FormalizationResponse?> FormalizeAsync(Legal.Application.Features.Intelligence.Science.FormalizationRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/intelligence_formalization/formalize",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Science.FormalizationResponse>(cancellationToken:token);
    }

    // ── Configuration center
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

    // Epistemic Authority (POLOXI EA) settings stored in Core.ConfigurationSetting (tenant override + platform default).
    public async Task<IReadOnlyCollection<EpistemicSettingDto>> GetEpistemicSettingsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<EpistemicSettingDto>>("api/intelligence/epistemic-settings",token)??[];
    public async Task SaveEpistemicSettingAsync(string settingKey,SaveEpistemicSettingRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/epistemic-settings/{Uri.EscapeDataString(settingKey)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}

    // Search result display toggle for the End-to-end POLOXI pipeline section.
    public async Task<bool> GetShowPipelineAsync(CancellationToken token=default)=>await TryGetShowPipelineAsync("api/intelligence/show-pipeline",token);
    public async Task<bool> GetSearchShowPipelineAsync(CancellationToken token=default)=>await TryGetShowPipelineAsync("api/intelligence_wide/show-pipeline",token);
    private async Task<bool> TryGetShowPipelineAsync(string url,CancellationToken token)
    {
        using var response=await _httpClient.GetAsync(url,token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return false;
        await EnsureSuccessWithDetailAsync(response,token);
        return (await response.Content.ReadFromJsonAsync<ShowPipelineResponse>(token))?.ShowPipeline??false;
    }
    public async Task SaveShowPipelineAsync(bool showPipeline,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync("api/intelligence/show-pipeline",new{showPipeline},token);await EnsureSuccessWithDetailAsync(response,token);}

    private sealed record ShowPipelineResponse(bool ShowPipeline);

    private static async Task EnsureSuccessWithDetailAsync(HttpResponseMessage response,CancellationToken token){if(response.IsSuccessStatusCode)return;var detail=await response.Content.ReadAsStringAsync(token);throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)?$"Request failed with status {(int)response.StatusCode}.":detail);}
}
