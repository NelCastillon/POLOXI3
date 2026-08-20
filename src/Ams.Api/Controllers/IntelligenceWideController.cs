using Ams.Api.Security;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Intelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

// Isolated controller for the "Intelligent Search Wide" variant. Mirrors the existing
// api/intelligence/search/poloxi endpoint so the wide path can evolve independently.
[ApiController]
[Route("api/intelligence_wide")]
public sealed class IntelligenceWideController(IIntelligenceWideService service):ControllerBase
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
}
