using Legal.Api.Security;
using Legal.Api.Services;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Isolated controller for the "Intelligent Search Wide" variant. Mirrors the existing
// api/intelligence/search/poloxi endpoint so the wide path can evolve independently.
[ApiController]
[Route("api/intelligence_wide")]
public sealed class IntelligenceWideController(IIntelligenceWideService service,WideSearchOperationStore operationStore,IIntelligenceExecutionService executionService):ControllerBase
{
    private const string CapabilityCode=JudzCapabilities.GeneralSearch;
    private Guid TenantId=>AuthenticatedRequestContext.GetTenantId(User)??throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId=>AuthenticatedRequestContext.GetUserId(User)??throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("search/poloxi_wide")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> SearchWithPoloxiWide([FromBody]PoloxiSearchRequest request,CancellationToken cancellationToken)
    {
        var (denied,_)=await CapabilityGate.EnforceAsync(executionService,User,CapabilityCode,null,null,cancellationToken);
        if(denied is not null)return denied;
        return Ok(await service.SearchWithPoloxiWideAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));
    }

    // Dynamic progressive disambiguation pipeline: LLM builds a problem-specific hierarchy, each level is
    // grounded against enterprise data, weak candidates are eliminated, and a verified answer is composed.
    [HttpPost("search/dynamic")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> SearchDynamic([FromBody]WideSearchRequest request,CancellationToken cancellationToken)
    {
        var (denied,_)=await CapabilityGate.EnforceAsync(executionService,User,CapabilityCode,null,null,cancellationToken);
        if(denied is not null)return denied;
        return Ok(await service.SearchDynamicAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));
    }

    // Async start+poll transport: starts the SAME pipeline on a background task and returns an operation
    // ID immediately, so no HTTP request has to outlive the pipeline. Transport only; POLOXI unchanged.
    [HttpPost("search/dynamic/start")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> StartSearchDynamic([FromBody]WideSearchRequest request,CancellationToken cancellationToken)
    {
        var (denied,_)=await CapabilityGate.EnforceAsync(executionService,User,CapabilityCode,null,null,cancellationToken);
        if(denied is not null)return denied;
        return Ok(new WideSearchOperationStartResponse(operationStore.Start(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)})));
    }

    [HttpGet("search/dynamic/status/{operationId:guid}")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public IActionResult SearchDynamicStatus(Guid operationId)=>operationStore.GetStatus(TenantId,operationId)is{}status?Ok(status):NotFound();

    // User-initiated cancellation of a running wide search operation (tenant-scoped, idempotent).
    [HttpPost("search/dynamic/cancel/{operationId:guid}")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public IActionResult CancelSearchDynamic(Guid operationId)=>operationStore.Cancel(TenantId,operationId)?Ok():NotFound();

    // Database-backed model options for the wide-search Model dropdown (active CHAT deployments).
    [HttpGet("models")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> Models(CancellationToken cancellationToken)=>Ok(await service.GetWideModelsAsync(TenantId,cancellationToken));

    // Database-backed context options for the wide-search Context dropdown (POLOXI.Legal_SearchContext).
    [HttpGet("contexts")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> Contexts(CancellationToken cancellationToken)=>Ok(await service.GetSearchContextsAsync(TenantId,cancellationToken));

    // Platform UI toggle: when true the search result page renders the End-to-end POLOXI pipeline section.
    [HttpGet("show-pipeline")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> ShowPipeline(CancellationToken cancellationToken)=>Ok(new{showPipeline=await service.GetShowPipelineAsync(cancellationToken)});
}
