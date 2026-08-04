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
    public async Task<IActionResult> Search([FromBody]IntelligenceSearchRequest request,CancellationToken cancellationToken)=>Ok(await service.SearchAsync(request with{TenantId=TenantId,UserId=ActorUserId},cancellationToken));

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
