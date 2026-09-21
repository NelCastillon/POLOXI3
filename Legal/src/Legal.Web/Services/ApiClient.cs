using System.Net.Http.Json;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

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

    // ── Owner-only Account Settings (organization/tenant profile)
    public Task<Legal.Application.Features.Saas.TenantProfileDto?> GetTenantProfileAsync(CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.Saas.TenantProfileDto>("api/account_settings/profile",token);
    public async Task<Legal.Application.Features.Saas.TenantProfileDto?> UpdateTenantProfileAsync(Legal.Application.Features.Saas.UpdateTenantProfileRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync("api/account_settings/profile",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Saas.TenantProfileDto>(cancellationToken:token);}

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
    public async Task<IReadOnlyCollection<DecisionPromptConfigurationDto>> GetDecisionPromptConfigurationsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DecisionPromptConfigurationDto>>("api/intelligence/decision-prompts",token)??[];
    public async Task<IReadOnlyCollection<DecisionModelRouteDto>> GetDecisionModelRoutesAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DecisionModelRouteDto>>("api/intelligence/decision-model-routes",token)??[];
    public async Task SaveDecisionPromptConfigurationAsync(string promptCode,SaveDecisionPromptConfigurationRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/decision-prompts/{Uri.EscapeDataString(promptCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
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

    // ── Judz.ai Early Access SaaS — public authentication surface ──────────────
    // Calls the anonymous api/auth/* endpoints. The API returns loosely-typed JSON
    // envelopes ({ message, errors, outcome, ... }); AuthResult normalises them.
    public Task<AuthResult> SignupAsync(Legal.Application.Features.Saas.SignupRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/signup",request,token);    public Task<AuthResult> VerifyEmailAsync(Legal.Application.Features.Saas.VerifyEmailRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/verify-email",request,token);

    public Task<AuthResult> ResendVerificationAsync(Legal.Application.Features.Saas.ResendVerificationRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/resend-verification",request,token);

    public Task<AuthResult> LoginAsync(Legal.Application.Features.Saas.LoginRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/login",request,token);

    public Task<AuthResult> ForgotPasswordAsync(Legal.Application.Features.Saas.ForgotPasswordRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/forgot-password",request,token);

    public Task<AuthResult> ResetPasswordAsync(Legal.Application.Features.Saas.ResetPasswordRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/reset-password",request,token);

    private async Task<AuthResult> PostAuthAsync(string url,object request,CancellationToken token)
    {
        using var response=await _httpClient.PostAsJsonAsync(url,request,token);
        AuthEnvelope? envelope=null;
        try{envelope=await response.Content.ReadFromJsonAsync<AuthEnvelope>(cancellationToken:token);}catch{/* non-JSON body */}
        if(response.IsSuccessStatusCode)
            return new AuthResult(true,envelope?.Message,null,envelope?.Outcome,envelope?.TenantId,envelope?.RequiresVerification??false,envelope?.UserId,envelope?.Email,envelope?.DisplayName,envelope?.Permissions??[],envelope?.RoleCode);

        var errors=envelope?.Errors is{Count:>0}?string.Join(" ",envelope.Errors):null;
        var message=envelope?.Message??errors??$"Request failed with status {(int)response.StatusCode}.";
        return new AuthResult(false,message,errors,envelope?.Outcome,envelope?.TenantId,envelope?.RequiresVerification??false,envelope?.UserId,envelope?.Email,envelope?.DisplayName,envelope?.Permissions??[],envelope?.RoleCode);
    }

    private sealed record AuthEnvelope(string? Message,List<string>? Errors,string? Outcome,Guid? TenantId,bool? RequiresVerification,Guid? UserId,string? Email,string? DisplayName,List<string>? Permissions,string? RoleCode);

    // Anonymous legal agreements for the signup clickwrap surface.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.LegalAgreementDto>> GetActiveAgreementsAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.LegalAgreementDto>>("api/auth/agreements",token)??[];

    // Tenant Configuration control plane (/admin/configuration).
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.TenantConfigurationCategoryDto>> GetTenantConfigurationAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.TenantConfigurationCategoryDto>>("api/tenant_configuration",token)??[];

    public async Task SetTenantConfigurationValueAsync(string key,string? valueJson,CancellationToken token=default)
    {
        var request=new Legal.Application.Features.Saas.SetTenantConfigurationRequest(key,valueJson);
        using var response=await _httpClient.PutAsJsonAsync("api/tenant_configuration",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Platform Configuration control plane (/platform/configuration) — Super Admin only.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.PlatformConfigurationCategoryDto>> GetPlatformConfigurationAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.PlatformConfigurationCategoryDto>>("api/platform_configuration",token)??[];

    public async Task SetPlatformConfigurationValueAsync(string key,string? valueJson,CancellationToken token=default)
    {
        var request=new Legal.Application.Features.Saas.SetPlatformConfigurationRequest(key,valueJson);
        using var response=await _httpClient.PutAsJsonAsync("api/platform_configuration",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Tenant User Management (/admin/users) — Tenant Admin, own tenant only.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.ManagedMemberDto>> GetTenantMembersAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.ManagedMemberDto>>("api/tenant_users",token)??[];

    public async Task<Legal.Application.Features.Saas.MemberPageDto> GetTenantMembersPageAsync(string? search=null,string? status=null,int page=1,int pageSize=25,CancellationToken token=default)
    {
        var query=$"api/tenant_users/page?page={page}&pageSize={pageSize}";
        if(!string.IsNullOrWhiteSpace(search))query+=$"&search={Uri.EscapeDataString(search.Trim())}";
        if(!string.IsNullOrWhiteSpace(status))query+=$"&status={Uri.EscapeDataString(status.Trim())}";
        return await _httpClient.GetFromJsonAsync<Legal.Application.Features.Saas.MemberPageDto>(query,token)
            ??new Legal.Application.Features.Saas.MemberPageDto([],0,0,0,0,page,pageSize);
    }

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AssignableRoleDto>> GetTenantAssignableRolesAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AssignableRoleDto>>("api/tenant_users/roles",token)??[];

    // Legal clickwrap consent evidence for a member.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.ConsentRecordDto>> GetTenantMemberConsentAsync(Guid userId,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.ConsentRecordDto>>($"api/tenant_users/{userId}/consent",token)??[];

    public async Task<Legal.Application.Features.Saas.ProvisionMemberResult> InviteTenantMemberAsync(Legal.Application.Features.Saas.InviteMemberRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.InviteMemberRequest,Legal.Application.Features.Saas.ProvisionMemberResult>("api/tenant_users/invite",request,token);

    public async Task<Legal.Application.Features.Saas.ProvisionMemberResult> CreateTenantMemberAsync(Legal.Application.Features.Saas.CreateMemberRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.CreateMemberRequest,Legal.Application.Features.Saas.ProvisionMemberResult>("api/tenant_users/create",request,token);

    public async Task ChangeTenantMemberRoleAsync(Legal.Application.Features.Saas.ChangeMemberRoleRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/tenant_users/role",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task ChangeTenantMemberStatusAsync(Legal.Application.Features.Saas.ChangeMemberStatusRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/tenant_users/status",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task RemoveTenantMemberAsync(Guid membershipId,CancellationToken token=default)
    {
        using var response=await _httpClient.DeleteAsync($"api/tenant_users/{membershipId}",token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task ResetTenantMemberLockoutAsync(Guid membershipId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/tenant_users/{membershipId}/reset-lockout",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task SetTenantMemberPasswordAsync(Legal.Application.Features.Saas.SetMemberPasswordRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/tenant_users/set-password",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Tenant Invitations (Phase B) — Tenant Admin manages invitations for own tenant.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.TenantInvitationDto>> GetTenantInvitationsAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.TenantInvitationDto>>("api/tenant_users/invitations",token)??[];

    public async Task<Legal.Application.Features.Saas.TenantInvitationDto> CreateTenantInvitationAsync(Legal.Application.Features.Saas.CreateInvitationRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.CreateInvitationRequest,Legal.Application.Features.Saas.TenantInvitationDto>("api/tenant_users/invitations",request,token);

    public async Task ResendTenantInvitationAsync(Guid invitationId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/tenant_users/invitations/{invitationId}/resend",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task RevokeTenantInvitationAsync(Guid invitationId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/tenant_users/invitations/{invitationId}/revoke",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Tenant Groups (Phase C) — Tenant Admin manages groups for own tenant.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.TenantGroupDto>> GetTenantGroupsAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.TenantGroupDto>>("api/tenant_groups",token)??[];

    public async Task<Legal.Application.Features.Saas.TenantGroupDetailDto?> GetTenantGroupAsync(Guid groupId,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<Legal.Application.Features.Saas.TenantGroupDetailDto>($"api/tenant_groups/{groupId}",token);

    public async Task<Legal.Application.Features.Saas.TenantGroupDto> CreateTenantGroupAsync(Legal.Application.Features.Saas.CreateGroupRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.CreateGroupRequest,Legal.Application.Features.Saas.TenantGroupDto>("api/tenant_groups",request,token);

    public async Task UpdateTenantGroupAsync(Legal.Application.Features.Saas.UpdateGroupRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/tenant_groups",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task DeleteTenantGroupAsync(Guid groupId,CancellationToken token=default)
    {
        using var response=await _httpClient.DeleteAsync($"api/tenant_groups/{groupId}",token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.GroupMemberDto>> GetTenantGroupMembersAsync(Guid groupId,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.GroupMemberDto>>($"api/tenant_groups/{groupId}/members",token)??[];

    public async Task AddTenantGroupMemberAsync(Guid groupId,Guid userId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/tenant_groups/{groupId}/members/{userId}",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task RemoveTenantGroupMemberAsync(Guid groupId,Guid userId,CancellationToken token=default)
    {
        using var response=await _httpClient.DeleteAsync($"api/tenant_groups/{groupId}/members/{userId}",token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Tenant Activity (Phase C) — read-only audit/usage/login history for own tenant.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>> GetActivityAuditEventsAsync(int days=30,int take=100,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>>($"api/tenant_activity/audit?days={days}&take={take}",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>> GetActivityUsageAsync(int days=30,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>>($"api/tenant_activity/usage?days={days}",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.LoginHistoryDto>> GetActivityLoginHistoryAsync(int days=30,int take=100,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.LoginHistoryDto>>($"api/tenant_activity/logins?days={days}&take={take}",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>> GetMyAuditEventsAsync(int days=30,int take=100,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>>($"api/tenant_activity/me/audit?days={days}&take={take}",token)??[];

    public Task<Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>> GetMyAuditEventsPageAsync(int days=30,string? search=null,int page=1,int pageSize=25,CancellationToken token=default)
        =>GetAuditPageAsync($"api/tenant_activity/me/audit/page?days={days}",search,page,pageSize,token);

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>> GetMyUsageAsync(int days=30,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>>($"api/tenant_activity/me/usage?days={days}",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>> GetUserAuditEventsAsync(Guid userId,Guid? tenantId=null,int days=30,int take=100,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>>($"api/tenant_activity/users/{userId}/audit?tenantId={tenantId}&days={days}&take={take}",token)??[];

    public Task<Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>> GetUserAuditEventsPageAsync(Guid userId,Guid? tenantId=null,int days=30,string? search=null,int page=1,int pageSize=25,CancellationToken token=default)
        =>GetAuditPageAsync($"api/tenant_activity/users/{userId}/audit/page?tenantId={tenantId}&days={days}",search,page,pageSize,token);

    private async Task<Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>> GetAuditPageAsync(string endpoint,string? search,int page,int pageSize,CancellationToken token)
    {
        var query=$"{endpoint}&page={page}&pageSize={pageSize}";
        if(!string.IsNullOrWhiteSpace(search))query+=$"&search={Uri.EscapeDataString(search.Trim())}";
        return await _httpClient.GetFromJsonAsync<Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>>(query,token)
            ??new Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>([],0,page,pageSize);
    }

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>> GetUserUsageAsync(Guid userId,Guid? tenantId=null,int days=30,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>>($"api/tenant_activity/users/{userId}/usage?tenantId={tenantId}&days={days}",token)??[];

    // Public invitation acceptance (/invitations/{token}) — anonymous, token-only.
    public async Task<Legal.Application.Features.Saas.InvitationLookupDto?> LookupInvitationAsync(string invitationToken,CancellationToken token=default)
    {
        using var response=await _httpClient.GetAsync($"api/invitations/{Uri.EscapeDataString(invitationToken)}",token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return null;
        await EnsureSuccessWithDetailAsync(response,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Saas.InvitationLookupDto>(cancellationToken:token);
    }

    public async Task<Legal.Application.Features.Saas.AcceptInvitationResult?> AcceptInvitationAsync(Legal.Application.Features.Saas.AcceptInvitationRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/invitations/accept",request,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Saas.AcceptInvitationResult>(cancellationToken:token);
    }

    // Platform User Management (/platform/users) — Super Admin, all tenants.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.ManagedMemberDto>> GetPlatformMembersAsync(Guid? tenantId=null,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.ManagedMemberDto>>(tenantId.HasValue?$"api/platform_users?tenantId={tenantId.Value}":"api/platform_users",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AssignableRoleDto>> GetPlatformAssignableRolesAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AssignableRoleDto>>("api/platform_users/roles",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.TenantOptionDto>> GetPlatformTenantsAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.TenantOptionDto>>("api/platform_users/tenants",token)??[];

    public async Task<Legal.Application.Features.Saas.ProvisionMemberResult> InvitePlatformMemberAsync(Legal.Application.Features.Saas.InviteMemberRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.InviteMemberRequest,Legal.Application.Features.Saas.ProvisionMemberResult>("api/platform_users/invite",request,token);

    public async Task<Legal.Application.Features.Saas.ProvisionMemberResult> CreatePlatformMemberAsync(Legal.Application.Features.Saas.CreateMemberRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.CreateMemberRequest,Legal.Application.Features.Saas.ProvisionMemberResult>("api/platform_users/create",request,token);

    public async Task ChangePlatformMemberRoleAsync(Legal.Application.Features.Saas.ChangeMemberRoleRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/platform_users/role",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task ChangePlatformMemberStatusAsync(Legal.Application.Features.Saas.ChangeMemberStatusRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/platform_users/status",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task RemovePlatformMemberAsync(Guid membershipId,CancellationToken token=default)
    {
        using var response=await _httpClient.DeleteAsync($"api/platform_users/{membershipId}",token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task ResetPlatformMemberLockoutAsync(Guid membershipId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/platform_users/{membershipId}/reset-lockout",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task SetPlatformMemberPasswordAsync(Legal.Application.Features.Saas.SetMemberPasswordRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/platform_users/set-password",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    private async Task<TResult> PostForResultAsync<TRequest,TResult>(string uri,TRequest request,CancellationToken token)
    {
        using var response=await _httpClient.PostAsJsonAsync(uri,request,token);
        await EnsureSuccessWithDetailAsync(response,token);
        return (await response.Content.ReadFromJsonAsync<TResult>(token))!;
    }

    private static async Task EnsureSuccessWithDetailAsync(HttpResponseMessage response,CancellationToken token){if(response.IsSuccessStatusCode)return;var detail=await response.Content.ReadAsStringAsync(token);throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)?$"Request failed with status {(int)response.StatusCode}.":detail);}
}

/// <summary>Normalised result of a Judz.ai auth endpoint call for the Blazor UI.</summary>
public sealed record AuthResult(
    bool Succeeded,
    string? Message,
    string? Errors,
    string? Outcome,
    Guid? TenantId,
    bool RequiresVerification,
    Guid? UserId = null,
    string? Email = null,
    string? DisplayName = null,
    IReadOnlyList<string>? Permissions = null,
    string? RoleCode = null);
