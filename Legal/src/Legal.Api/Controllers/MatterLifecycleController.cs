using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.MatterLifecycle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Judz Matter Lifecycle API — operational stage context for a matter. Separate from POLOXI Core
// decision mechanics. All stage/transition data is DB-backed; transitions must be valid directed
// edges in the lifecycle graph. Tenant/actor scoped from the authenticated context.
[ApiController]
[Route("api/legal_matter_lifecycle")]
public sealed class MatterLifecycleController(IMatterLifecycleService service) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    // Returns the matter's lifecycle snapshot, creating a default lifecycle on first access.
    [HttpGet("{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetSnapshot(Guid matterId, CancellationToken cancellationToken)
    {
        var snapshot = await service.GetOrCreateSnapshotAsync(TenantId, ActorUserId, matterId, cancellationToken);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    // Performs a directed stage transition. 409 if the transition is not available from the current stage.
    [HttpPost("{matterId:guid}/transitions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> PerformTransition(Guid matterId, [FromBody] PerformMatterLifecycleTransitionRequest request, CancellationToken cancellationToken)
    {
        if (request.DecisionMatterId != matterId)
            return BadRequest("Matter id in the route must match the request body.");

        try
        {
            var ok = await service.PerformTransitionAsync(TenantId, ActorUserId, request, cancellationToken);
            if (!ok)
                return NotFound("The matter has no active lifecycle.");

            var snapshot = await service.GetSnapshotAsync(TenantId, matterId, cancellationToken);
            return Ok(snapshot);
        }
        catch (InvalidMatterLifecycleTransitionException ex)
        {
            return Conflict(ex.Message);
        }
    }

    // ── Configuration admin surface (DB-backed lifecycle authoring) ──────────
    [HttpGet("config/definitions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetDefinitions(CancellationToken cancellationToken)
        => Ok(await service.GetDefinitionsAsync(TenantId, cancellationToken));

    [HttpGet("config/definitions/{definitionId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetDefinitionDetail(Guid definitionId, CancellationToken cancellationToken)
    {
        var detail = await service.GetDefinitionDetailAsync(TenantId, definitionId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("config/definitions")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDefinition([FromBody] SaveMatterLifecycleDefinitionRequest request, CancellationToken cancellationToken)
        => Ok(await service.SaveDefinitionAsync(TenantId, ActorUserId, request, cancellationToken));

    [HttpPost("config/stages")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveStage([FromBody] SaveMatterLifecycleStageRequest request, CancellationToken cancellationToken)
        => Ok(await service.SaveStageAsync(TenantId, ActorUserId, request, cancellationToken));

    [HttpPost("config/transitions")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveTransition([FromBody] SaveMatterLifecycleTransitionRequest request, CancellationToken cancellationToken)
        => Ok(await service.SaveTransitionAsync(TenantId, ActorUserId, request, cancellationToken));
}
