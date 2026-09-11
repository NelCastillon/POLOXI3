using Legal.Api.Security;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Science;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// POLOXI Scientific Reasoning — Mathematics V1 endpoint. Runs the linear 8-stage discovery pipeline and
// returns the honest outcome plus the verification/proof transparency trail. Answer acceptance is owned
// by deterministic C# verification, not the LLM.
[ApiController]
[Route("api/intelligence_math")]
public sealed class IntelligenceMathController(IMathReasoningService service, IMathReasoningRepository repository) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("solve")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Solve([FromBody] MathSolveRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SolveAsync(
            request with
            {
                TenantId = TenantId,
                UserId = ActorUserId,
                GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User),
            },
            cancellationToken));

    // Lists recent persisted Math solve runs for the tenant (most recent first) for the audit history view.
    [HttpGet("runs")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ListRuns([FromQuery] int take = 50, CancellationToken cancellationToken = default) =>
        Ok(await repository.ListMathExecutionsAsync(TenantId, take, cancellationToken));

    // Loads a single persisted Math run with its deterministic obligation verdicts and ranked candidates.
    [HttpGet("runs/{mathExecutionId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetRun(Guid mathExecutionId, CancellationToken cancellationToken)
    {
        var run = await repository.GetMathExecutionAsync(TenantId, mathExecutionId, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }
}
