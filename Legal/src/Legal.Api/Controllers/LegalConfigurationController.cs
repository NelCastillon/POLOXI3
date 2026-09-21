using Legal.Api.Security;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Configuration surface for the Legal module (providers, models, feature policies, prompts).
// Mirrors the subset of api/intelligence endpoints used by the Legal configuration page.
[ApiController]
[Route("api/intelligence")]
public sealed class LegalConfigurationController(IIntelligenceRepository repository,IIntelligenceWideService wideService):ControllerBase
{
    private Guid TenantId=>AuthenticatedRequestContext.GetTenantId(User)??throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId=>AuthenticatedRequestContext.GetUserId(User)??throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpGet("platform")]
    [Authorize(Policy=IntelligencePolicies.Read)]
    public async Task<IActionResult> Platform(CancellationToken cancellationToken)=>Ok(await repository.GetPlatformSummaryAsync(TenantId,cancellationToken));

    [HttpGet("providers")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Providers(CancellationToken cancellationToken)=>Ok(await repository.GetProvidersAsync(TenantId,cancellationToken));

    [HttpPut("providers/{providerCode}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveProvider(string providerCode,[FromBody]SaveAiProviderRequest request,CancellationToken cancellationToken){await repository.SaveProviderAsync(request with{TenantId=TenantId,ProviderCode=providerCode,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpDelete("providers/{providerCode}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteProvider(string providerCode,CancellationToken cancellationToken){await repository.DeleteProviderAsync(TenantId,providerCode,ActorUserId,cancellationToken);return NoContent();}

    [HttpGet("models")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Models(CancellationToken cancellationToken)=>Ok(await repository.GetModelsAsync(TenantId,cancellationToken));

    [HttpPut("models/{modelCode}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveModel(string modelCode,[FromBody]SaveAiModelDeploymentRequest request,CancellationToken cancellationToken){await repository.SaveModelDeploymentAsync(request with{TenantId=TenantId,ModelCode=modelCode,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpDelete("models/{modelCode}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteModel(string modelCode,CancellationToken cancellationToken){await repository.DeleteModelDeploymentAsync(TenantId,modelCode,ActorUserId,cancellationToken);return NoContent();}

    [HttpGet("feature-policies")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> FeaturePolicies(CancellationToken cancellationToken)=>Ok(await repository.GetFeaturePoliciesAsync(TenantId,cancellationToken));

    [HttpPut("feature-policies/{featureCode}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveFeaturePolicy(string featureCode,[FromBody]SaveAiFeaturePolicyRequest request,CancellationToken cancellationToken){await repository.SaveFeaturePolicyAsync(request with{TenantId=TenantId,FeatureCode=featureCode,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpDelete("feature-policies/{featureCode}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteFeaturePolicy(string featureCode,CancellationToken cancellationToken){await repository.DeleteFeaturePolicyAsync(TenantId,featureCode,ActorUserId,cancellationToken);return NoContent();}

    [HttpGet("prompts")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Prompts(CancellationToken cancellationToken)=>Ok(await repository.GetPromptDefinitionsAsync(TenantId,cancellationToken));

    [HttpPut("prompts/{promptCode}/{versionLabel}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SavePrompt(string promptCode,string versionLabel,[FromBody]SaveIntelligencePromptDefinitionRequest request,CancellationToken cancellationToken){await repository.SavePromptDefinitionAsync(request with{TenantId=TenantId,PromptCode=promptCode,VersionLabel=versionLabel,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpDelete("prompts/{promptCode}/{versionLabel}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeletePrompt(string promptCode,string versionLabel,CancellationToken cancellationToken){await repository.DeletePromptDefinitionAsync(TenantId,promptCode,versionLabel,ActorUserId,cancellationToken);return NoContent();}

    [HttpGet("decision-prompts")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> DecisionPrompts([FromServices] ILegalDecisionRepository decisionRepository,CancellationToken cancellationToken)
        =>Ok(await decisionRepository.GetPromptConfigurationsAsync(cancellationToken));

    [HttpGet("decision-model-routes")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> DecisionModelRoutes([FromServices] ILegalDecisionRepository decisionRepository,CancellationToken cancellationToken)
        =>Ok(await decisionRepository.GetModelRoutesAsync(cancellationToken));

    [HttpPut("decision-prompts/{promptCode}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDecisionPrompt(
        string promptCode,
        [FromBody] SaveDecisionPromptConfigurationRequest request,
        [FromServices] ILegalDecisionRepository decisionRepository,
        CancellationToken cancellationToken)
    {
        await decisionRepository.SavePromptConfigurationAsync(ActorUserId,request with{PromptCode=promptCode},cancellationToken);
        return NoContent();
    }

    // Legal Grounding settings (CourtListener/GovInfo/eCFR) stored in Core.ConfigurationSetting.
    [HttpGet("legal-grounding-settings")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> LegalGroundingSettings(CancellationToken cancellationToken)=>Ok(await wideService.GetLegalGroundingSettingsAsync(cancellationToken));

    [HttpPut("legal-grounding-settings/{settingKey}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveLegalGroundingSetting(string settingKey,[FromBody]SaveLegalGroundingSettingRequest request,CancellationToken cancellationToken){await wideService.SaveLegalGroundingSettingAsync(request with{SettingKey=settingKey},ActorUserId,cancellationToken);return NoContent();}

    [HttpDelete("legal-grounding-settings/{settingKey}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteLegalGroundingSetting(string settingKey,CancellationToken cancellationToken){await wideService.DeleteLegalGroundingSettingAsync(settingKey,cancellationToken);return NoContent();}

    // Epistemic Authority (POLOXI EA) settings stored in Core.ConfigurationSetting (tenant override + platform default).
    [HttpGet("epistemic-settings")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> EpistemicSettings(CancellationToken cancellationToken)=>Ok(await wideService.GetEpistemicSettingsAsync(TenantId,cancellationToken));

    [HttpPut("epistemic-settings/{settingKey}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveEpistemicSetting(string settingKey,[FromBody]SaveEpistemicSettingRequest request,CancellationToken cancellationToken){await wideService.SaveEpistemicSettingAsync(request with{SettingKey=settingKey},TenantId,ActorUserId,cancellationToken);return NoContent();}

    // Search result display toggle for the End-to-end POLOXI pipeline section (Core.ConfigurationSetting).
    [HttpGet("show-pipeline")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> GetShowPipeline(CancellationToken cancellationToken)=>Ok(new{showPipeline=await wideService.GetShowPipelineAsync(cancellationToken)});

    [HttpPut("show-pipeline")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveShowPipeline([FromBody]SaveShowPipelineRequest request,CancellationToken cancellationToken){await wideService.SaveShowPipelineAsync(request.ShowPipeline,cancellationToken);return NoContent();}
}

public sealed record SaveShowPipelineRequest(bool ShowPipeline);
