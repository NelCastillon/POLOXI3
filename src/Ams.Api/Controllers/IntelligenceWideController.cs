using Ams.Api.Security;
using Ams.Api.Services;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Intelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

// Isolated controller for the "Intelligent Search Wide" variant. Mirrors the existing
// api/intelligence/search/poloxi endpoint so the wide path can evolve independently.
[ApiController]
[Route("api/intelligence_wide")]
public sealed class IntelligenceWideController(IIntelligenceWideService service,WideSearchOperationStore operationStore):ControllerBase
{
    private Guid TenantId=>AuthenticatedRequestContext.GetTenantId(User)??throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId=>AuthenticatedRequestContext.GetUserId(User)??throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("search/poloxi_wide")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> SearchWithPoloxiWide([FromBody]PoloxiSearchRequest request,CancellationToken cancellationToken)=>Ok(await service.SearchWithPoloxiWideAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));

    // Dynamic progressive disambiguation pipeline: LLM builds a problem-specific hierarchy, each level is
    // grounded against enterprise data, weak candidates are eliminated, and a verified answer is composed.
    [HttpPost("search/dynamic")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public async Task<IActionResult> SearchDynamic([FromBody]WideSearchRequest request,CancellationToken cancellationToken)=>Ok(await service.SearchDynamicAsync(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)},cancellationToken));

    // Async start+poll transport: starts the SAME pipeline on a background task and returns an operation
    // ID immediately, so no HTTP request has to outlive the pipeline. Transport only; POLOXI unchanged.
    [HttpPost("search/dynamic/start")]
    [Authorize(Policy=IntelligencePolicies.Search)]
    public IActionResult StartSearchDynamic([FromBody]WideSearchRequest request)=>Ok(new WideSearchOperationStartResponse(operationStore.Start(request with{TenantId=TenantId,UserId=ActorUserId,GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)})));

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
}
