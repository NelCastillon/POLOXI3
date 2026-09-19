using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — execution gateway.
//
// The single boundary every public intelligence capability (Search, Research,
// Legal Search, Legal Decision, Math) must pass through:
//   authorization → entitlement → quota reservation → idempotency → execution.
// No capability executes directly; the service enforces the full chain.
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/gateway")]
[Authorize]
public sealed class ExecutionGatewayController(
    IIntelligenceExecutionService executionService,
    IEntitlementService entitlementService) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    // Effective, DB-backed entitlements for the current tenant (plan grants merged with overrides).
    [HttpGet("entitlements")]
    public async Task<IActionResult> Entitlements(CancellationToken ct)
        => Ok(await entitlementService.GetEntitlementsAsync(TenantId, ct));

    // Create a governed capability execution (authorize → reserve → idempotency → execute).
    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteCapabilityRequest request, CancellationToken ct)
    {
        var permissions = AuthenticatedRequestContext.GetGrantedPermissions(User);
        try
        {
            var execution = await executionService.CreateAsync(ActorUserId, TenantId, permissions, request, ct);
            return Ok(execution);
        }
        catch (CapabilityAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Reason = ex.Message });
        }
        catch (UsageQuotaExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { ex.MeterCode, ex.Limit, Message = ex.Message });
        }
    }

    [HttpGet("executions/{executionId:guid}")]
    public async Task<IActionResult> Execution(Guid executionId, CancellationToken ct)
    {
        var execution = await executionService.GetAsync(TenantId, executionId, ct);
        return execution is null ? NotFound() : Ok(execution);
    }
}
