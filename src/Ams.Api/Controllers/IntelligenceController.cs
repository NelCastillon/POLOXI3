using Ams.Api.Security;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Intelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/intelligence")]
public sealed class IntelligenceController(IIntelligenceService service):ControllerBase
{
    private Guid TenantId=>AuthenticatedRequestContext.GetTenantId(User)??throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId=>AuthenticatedRequestContext.GetUserId(User)??throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpGet("dashboard")]
    [Authorize(Policy=IntelligencePolicies.Read)]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)=>Ok(await service.GetDashboardAsync(TenantId,cancellationToken));

    [HttpGet("platform")]
    [Authorize(Policy=IntelligencePolicies.Read)]
    public async Task<IActionResult> Platform(CancellationToken cancellationToken)=>Ok(await service.GetPlatformSummaryAsync(TenantId,cancellationToken));

    [HttpGet("platform/architecture")]
    [Authorize(Policy=IntelligencePolicies.Read)]
    public async Task<IActionResult> PlatformArchitecture(CancellationToken cancellationToken)=>Ok(await service.GetPlatformArchitectureAsync(TenantId,cancellationToken));

    [HttpGet("engine-policies")]
    [Authorize(Policy=IntelligencePolicies.GovernanceManage)]
    public async Task<IActionResult> EnginePolicies(CancellationToken cancellationToken)=>Ok(await service.GetEnginePoliciesAsync(TenantId,cancellationToken));

    [HttpPut("engine-policies/{policyCode}/{versionNumber:int}")]
    [Authorize(Policy=IntelligencePolicies.GovernanceManage)]
    public async Task<IActionResult> SaveEnginePolicy(string policyCode,int versionNumber,[FromBody]SaveIntelligenceEnginePolicyRequest request,CancellationToken cancellationToken){await service.SaveEnginePolicyAsync(request with{TenantId=TenantId,PolicyCode=policyCode,VersionNumber=versionNumber,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpGet("safety-controls")]
    [Authorize(Policy=IntelligencePolicies.GovernanceManage)]
    public async Task<IActionResult> SafetyControls(CancellationToken cancellationToken)=>Ok(await service.GetSafetyControlsAsync(TenantId,cancellationToken));

    [HttpPut("safety-controls/{controlCode}")]
    [Authorize(Policy=IntelligencePolicies.GovernanceManage)]
    public async Task<IActionResult> SaveSafetyControl(string controlCode,[FromBody]SaveIntelligenceSafetyControlRequest request,CancellationToken cancellationToken){await service.SaveSafetyControlAsync(request with{TenantId=TenantId,ControlCode=controlCode,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpGet("compliance-requirements")]
    [Authorize(Policy=IntelligencePolicies.GovernanceManage)]
    public async Task<IActionResult> ComplianceRequirements(CancellationToken cancellationToken)=>Ok(await service.GetComplianceRequirementsAsync(TenantId,cancellationToken));

    [HttpGet("safety-events")]
    [Authorize(Policy=IntelligencePolicies.AuditRead)]
    public async Task<IActionResult> SafetyEvents([FromQuery]int pageSize=100,CancellationToken cancellationToken=default)=>Ok(await service.GetSafetyEventsAsync(TenantId,pageSize,cancellationToken));

    [HttpGet("prompts")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Prompts(CancellationToken cancellationToken)=>Ok(await service.GetPromptDefinitionsAsync(TenantId,cancellationToken));

    [HttpPut("prompts/{promptCode}/{versionLabel}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SavePrompt(string promptCode,string versionLabel,[FromBody]SaveIntelligencePromptDefinitionRequest request,CancellationToken cancellationToken){await service.SavePromptDefinitionAsync(request with{TenantId=TenantId,PromptCode=promptCode,VersionLabel=versionLabel,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpPost("evaluations/labels")]
    [Authorize(Policy=IntelligencePolicies.Evaluate)]
    public async Task<IActionResult> SubmitEvaluationLabel([FromBody]SubmitEvaluationSampleLabelRequest request,CancellationToken cancellationToken){await service.SubmitEvaluationSampleLabelAsync(request with{TenantId=TenantId,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpGet("findings")]
    [Authorize(Policy=IntelligencePolicies.FindingsRead)]
    public async Task<IActionResult> Findings([FromQuery]string? searchTerm,[FromQuery]string? capabilityCode,[FromQuery]string? entityTypeCode,[FromQuery]Guid? entityId,[FromQuery]string? severityCode,[FromQuery]string? statusCode,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=50,CancellationToken cancellationToken=default)=>Ok(await service.SearchFindingsAsync(new(TenantId,searchTerm,capabilityCode,entityTypeCode,entityId,severityCode,statusCode,pageNumber,pageSize),cancellationToken));

    [HttpGet("findings/{id:guid}")]
    [Authorize(Policy=IntelligencePolicies.FindingsRead)]
    public async Task<IActionResult> Finding(Guid id,CancellationToken cancellationToken)=>await service.GetFindingAsync(TenantId,id,cancellationToken) is{} finding?Ok(finding):NotFound();

    [HttpPost("findings/{id:guid}/decision")]
    [Authorize(Policy=IntelligencePolicies.FindingsReview)]
    public async Task<IActionResult> DecideFinding(Guid id,[FromBody]DecideIntelligenceFindingRequest request,CancellationToken cancellationToken){await service.DecideFindingAsync(request with{TenantId=TenantId,IntelligenceFindingId=id,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpGet("relationships/{entityTypeCode}/{entityId:guid}")]
    [Authorize(Policy=IntelligencePolicies.RelationshipsRead)]
    public async Task<IActionResult> Relationships(string entityTypeCode,Guid entityId,[FromQuery]int maximumDepth=3,CancellationToken cancellationToken=default)=>Ok(await service.GetRelationshipGraphAsync(new(TenantId,ActorUserId,entityTypeCode,entityId,maximumDepth),cancellationToken));

    [HttpGet("similarity/{entityTypeCode}/{entityId:guid}")]
    [Authorize(Policy=IntelligencePolicies.RelationshipsRead)]
    public async Task<IActionResult> Similarity(string entityTypeCode,Guid entityId,[FromQuery]decimal minimumScore=0.7m,[FromQuery]int maximumResults=25,CancellationToken cancellationToken=default)=>Ok(await service.GetSimilarEntitiesAsync(new(TenantId,ActorUserId,entityTypeCode,entityId,minimumScore,maximumResults),cancellationToken));

    [HttpGet("business-signals")]
    [Authorize(Policy=IntelligencePolicies.FindingsRead)]
    public async Task<IActionResult> BusinessSignals([FromQuery]string? searchTerm,[FromQuery]string? capabilityCode,[FromQuery]string? entityTypeCode,[FromQuery]Guid? entityId,[FromQuery]string? severityCode,[FromQuery]string? statusCode,[FromQuery]Guid? assignedToUserId,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=50,CancellationToken cancellationToken=default)=>Ok(await service.SearchBusinessSignalsAsync(new(TenantId,searchTerm,capabilityCode,entityTypeCode,entityId,severityCode,statusCode,assignedToUserId,pageNumber,pageSize),cancellationToken));

    [HttpPost("business-signals/{id:guid}/decision")]
    [Authorize(Policy=IntelligencePolicies.FindingsReview)]
    public async Task<IActionResult> DecideBusinessSignal(Guid id,[FromBody]DecideBusinessIntelligenceSignalRequest request,CancellationToken cancellationToken){await service.DecideBusinessSignalAsync(request with{TenantId=TenantId,BusinessSignalId=id,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpPost("reasoning")]
    [Authorize(Policy=IntelligencePolicies.Reason)]
    public async Task<IActionResult> Reason([FromBody]InsuranceReasoningRequest request,CancellationToken cancellationToken)=>Ok(await service.ExecuteReasoningAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));

    [HttpGet("reasoning/{id:guid}")]
    [Authorize(Policy=IntelligencePolicies.Reason)]
    public async Task<IActionResult> ReasoningSession(Guid id,CancellationToken cancellationToken)=>await service.GetReasoningSessionAsync(TenantId,ActorUserId,id,cancellationToken) is{} session?Ok(session):NotFound();

    [HttpGet("providers")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Providers(CancellationToken cancellationToken)=>Ok(await service.GetProvidersAsync(TenantId,cancellationToken));

    [HttpGet("models")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Models(CancellationToken cancellationToken)=>Ok(await service.GetModelsAsync(TenantId,cancellationToken));

    [HttpGet("feature-policies")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> FeaturePolicies(CancellationToken cancellationToken)=>Ok(await service.GetFeaturePoliciesAsync(TenantId,cancellationToken));

    [HttpPut("feature-policies/{featureCode}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveFeaturePolicy(string featureCode,[FromBody]SaveAiFeaturePolicyRequest request,CancellationToken cancellationToken){await service.SaveFeaturePolicyAsync(request with{TenantId=TenantId,FeatureCode=featureCode,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpGet("executions")]
    [Authorize(Policy=IntelligencePolicies.AuditRead)]
    public async Task<IActionResult> Executions([FromQuery]string? searchTerm,[FromQuery]string? featureCode,[FromQuery]string? statusCode,[FromQuery]DateTime? fromUtc,[FromQuery]DateTime? toUtc,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=50,CancellationToken cancellationToken=default)=>Ok(await service.SearchExecutionsAsync(new(TenantId,searchTerm,featureCode,statusCode,fromUtc,toUtc,pageNumber,pageSize),cancellationToken));

    [HttpGet("executions/{id:guid}")]
    [Authorize(Policy=IntelligencePolicies.AuditRead)]
    public async Task<IActionResult> Execution(Guid id,CancellationToken cancellationToken)=>await service.GetExecutionAsync(TenantId,id,cancellationToken) is{} execution?Ok(execution):NotFound();

    [HttpPost("executions/{id:guid}/feedback")]
    [Authorize(Policy=IntelligencePolicies.Review)]
    public async Task<IActionResult> SubmitFeedback(Guid id,[FromBody]SubmitAiExecutionFeedbackRequest request,CancellationToken cancellationToken){await service.SubmitExecutionFeedbackAsync(request with{TenantId=TenantId,ExecutionId=id,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpGet("recommendation-types")]
    [Authorize(Policy=IntelligencePolicies.Read)]
    public async Task<IActionResult> RecommendationTypes(CancellationToken cancellationToken)=>Ok(await service.GetRecommendationTypesAsync(TenantId,cancellationToken));

    [HttpGet("recommendations")]
    [Authorize(Policy=IntelligencePolicies.Read)]
    public async Task<IActionResult> Recommendations([FromQuery]string? searchTerm,[FromQuery]string? typeCode,[FromQuery]string? statusCode,[FromQuery]string? entityTypeCode,[FromQuery]Guid? entityId,[FromQuery]Guid? assignedToUserId,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=50,CancellationToken cancellationToken=default)=>Ok(await service.SearchRecommendationsAsync(new(TenantId,searchTerm,typeCode,statusCode,entityTypeCode,entityId,assignedToUserId,pageNumber,pageSize),cancellationToken));

    [HttpPost("recommendations/generate")]
    [Authorize(Policy=IntelligencePolicies.Recommend)]
    public async Task<IActionResult> GenerateRecommendations([FromBody]GenerateRecommendationsRequest request,CancellationToken cancellationToken){await service.QueueRecommendationsAsync(request with{TenantId=TenantId,ActorUserId=ActorUserId},cancellationToken);return Accepted();}

    [HttpPost("recommendations/{id:guid}/decision")]
    [Authorize(Policy=IntelligencePolicies.Recommend)]
    public async Task<IActionResult> DecideRecommendation(Guid id,[FromBody]DecideRecommendationRequest request,CancellationToken cancellationToken){await service.DecideRecommendationAsync(request with{TenantId=TenantId,RecommendationId=id,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpPost("search")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> Search([FromBody]IntelligenceSearchRequest request,CancellationToken cancellationToken)=>Ok(await service.SearchAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));

    [HttpPost("search/poloxi")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> SearchWithPoloxi([FromBody]PoloxiSearchRequest request,CancellationToken cancellationToken)=>Ok(await service.SearchWithPoloxiAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));

    [HttpPost("quick-search/fast")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> QuickSearchFastPath([FromBody]QuickSearchRequest request,CancellationToken cancellationToken)=>Ok(await service.QuickSearchFastPathAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));

    [HttpPost("quick-search/intelligent-fallback")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> QuickSearchIntelligentFallback([FromBody]QuickSearchRequest request,CancellationToken cancellationToken)=>Ok(await service.QuickSearchIntelligentFallbackAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));

    [HttpGet("review-queue")]
    [Authorize(Policy=IntelligencePolicies.Review)]
    public async Task<IActionResult> ReviewQueue([FromQuery]string? searchTerm,[FromQuery]string? reviewTypeCode,[FromQuery]string? statusCode,[FromQuery]string? priorityCode,[FromQuery]Guid? assignedToUserId,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=50,CancellationToken cancellationToken=default)=>Ok(await service.SearchReviewQueueAsync(new(TenantId,searchTerm,reviewTypeCode,statusCode,priorityCode,assignedToUserId,pageNumber,pageSize),cancellationToken));

    [HttpPost("review-queue/{id:guid}/decision")]
    [Authorize(Policy=IntelligencePolicies.Review)]
    public async Task<IActionResult> DecideReview(Guid id,[FromBody]DecideAiReviewRequest request,CancellationToken cancellationToken){await service.DecideReviewAsync(request with{TenantId=TenantId,ReviewQueueItemId=id,ActorUserId=ActorUserId},cancellationToken);return NoContent();}

    [HttpGet("evaluations/definitions")]
    [Authorize(Policy=IntelligencePolicies.Evaluate)]
    public async Task<IActionResult> EvaluationDefinitions(CancellationToken cancellationToken)=>Ok(await service.GetEvaluationDefinitionsAsync(TenantId,cancellationToken));

    [HttpGet("evaluations/runs")]
    [Authorize(Policy=IntelligencePolicies.Evaluate)]
    public async Task<IActionResult> EvaluationRuns([FromQuery]int pageSize=100,CancellationToken cancellationToken=default)=>Ok(await service.GetEvaluationRunsAsync(TenantId,pageSize,cancellationToken));

    [HttpPost("evaluations/runs")]
    [Authorize(Policy=IntelligencePolicies.Evaluate)]
    public async Task<IActionResult> QueueEvaluation([FromBody]QueueAiEvaluationRequest request,CancellationToken cancellationToken){var id=await service.QueueEvaluationAsync(request with{TenantId=TenantId,ActorUserId=ActorUserId},cancellationToken);return Accepted(new{id});}
}
