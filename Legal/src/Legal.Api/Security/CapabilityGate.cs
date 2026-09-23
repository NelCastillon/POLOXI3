using System.Security.Claims;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Security;

// ─────────────────────────────────────────────────────────────────────────────
// Enforces the Judz.ai execution boundary for the feature controllers.
//
// Every public intelligence capability (Search, Legal Decision, Math, Formalize)
// must pass authorization → entitlement → quota reservation before its pipeline
// runs. This helper invokes IIntelligenceExecutionService.CreateAsync (the same
// governed boundary used by the execution gateway), returning the created
// execution when allowed, or an IActionResult (403/429) describing the denial.
// ─────────────────────────────────────────────────────────────────────────────
public static class CapabilityGate
{
    // Runs the boundary for the given capability. On success, 'result' is null
    // and 'execution' carries the governed execution record. On failure, 'result'
    // is a 403/429 action result and 'execution' is null.
    public static async Task<(IActionResult? Result, IntelligenceExecutionDto? Execution)> EnforceAsync(
        IIntelligenceExecutionService executionService,
        ClaimsPrincipal user,
        string capabilityCode,
        Guid? matterId,
        string? idempotencyKey,
        CancellationToken ct)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(user)
            ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
        var userId = AuthenticatedRequestContext.GetUserId(user)
            ?? throw new UnauthorizedAccessException("An authenticated user context is required.");
        var permissions = AuthenticatedRequestContext.GetGrantedPermissions(user);

        var request = new ExecuteCapabilityRequest(capabilityCode, matterId, idempotencyKey);
        try
        {
            var execution = await executionService.CreateAsync(userId, tenantId, permissions, request, ct);
            return (null, execution);
        }
        catch (CapabilityAuthorizationException ex)
        {
            return (new ObjectResult(new { Reason = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden }, null);
        }
        catch (UsageQuotaExceededException ex)
        {
            return (new ObjectResult(new { ex.MeterCode, ex.Limit, Message = ex.Message }) { StatusCode = StatusCodes.Status429TooManyRequests }, null);
        }
    }
}
