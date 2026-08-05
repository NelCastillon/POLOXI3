using Ams.Api.Security;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/platform/rules")]
public sealed class RulesPlatformController(IRulesPlatformService service) : ControllerBase
{
    [HttpPost("evaluate")]
    [Authorize(Policy = IntelligencePolicies.Evaluate)]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateRulesRequest request, CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
        var actorUserId = AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");
        return Ok(await service.EvaluateAsync(request with { TenantId = tenantId, ActorUserId = actorUserId }, cancellationToken));
    }
}
