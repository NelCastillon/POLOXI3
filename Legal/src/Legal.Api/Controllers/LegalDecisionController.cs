using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Isolated controller for the self-contained POLOXI Legal Decision Intelligence module
// (/legal/decision). Evolves independently from the Intelligence Wide (/legal/search) controller.
[ApiController]
[Route("api/legal_decision")]
public sealed class LegalDecisionController(ILegalDecisionService service,IIntelligenceExecutionService executionService) : ControllerBase
{
    private const string CapabilityCode = JudzCapabilities.LegalDecision;
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("decide")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Decide([FromBody] DecisionSearchRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, request.MatterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await service.DecideAsync(
            request with
            {
                TenantId = TenantId,
                UserId = ActorUserId,
                GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User)
            },
            cancellationToken));
    }

    // Database-backed model options for the decision Model dropdown.
    [HttpGet("models")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Models(CancellationToken cancellationToken)
        => Ok(await service.GetModelsAsync(TenantId, cancellationToken));

    // Database-backed context options for the decision Context dropdown (POLOXI.Legal_DecisionContext).
    [HttpGet("contexts")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Contexts(CancellationToken cancellationToken)
        => Ok(await service.GetContextsAsync(TenantId, cancellationToken));

    // ── Matter dashboard / cockpit ──────────────────────────────────────────────────────────────
    [HttpGet("matters")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Matters(CancellationToken cancellationToken)
        => Ok(await service.GetMattersAsync(TenantId, cancellationToken));

    // Distinct free-form facet values (matter type / jurisdiction / posture) to pre-populate dropdowns.
    [HttpGet("matters/facets")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterFacets(CancellationToken cancellationToken)
        => Ok(await service.GetMatterFacetsAsync(TenantId, cancellationToken));

    [HttpGet("matters/{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Matter(Guid matterId, CancellationToken cancellationToken)
    {
        var matter = await service.GetMatterAsync(TenantId, matterId, cancellationToken);
        return matter is null ? NotFound() : Ok(matter);
    }

    [HttpPost("matters")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CreateMatter([FromBody] DecisionMatterCreateRequest request, CancellationToken cancellationToken)
    {
        var id = await service.CreateMatterAsync(TenantId, ActorUserId, request, cancellationToken);
        return Ok(new { DecisionMatterId = id });
    }

    // Full decision-session history for a matter (dashboard/detail history list).
    [HttpGet("matters/{matterId:guid}/sessions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterSessions(Guid matterId, CancellationToken cancellationToken)
        => Ok(await service.GetMatterSessionsAsync(TenantId, matterId, cancellationToken));

    [HttpPut("matters/{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> UpdateMatter(Guid matterId, [FromBody] DecisionMatterUpdateRequest request, CancellationToken cancellationToken)
    {
        var updated = await service.UpdateMatterAsync(TenantId, ActorUserId, matterId, request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpPost("matters/{matterId:guid}/status")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> UpdateMatterStatus(Guid matterId, [FromBody] DecisionMatterStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!DecisionMatterStatusCodes.IsValid(request.StatusCode))
            return BadRequest($"'{request.StatusCode}' is not a valid matter status.");
        var updated = await service.UpdateMatterStatusAsync(TenantId, ActorUserId, matterId, request.StatusCode, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("matters/{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> DeleteMatter(Guid matterId, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteMatterAsync(TenantId, ActorUserId, matterId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    // Decision-history timeline for a session (sourced from POLOXI.Legal_DecisionEvent).
    [HttpGet("sessions/{sessionId:guid}/timeline")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Timeline(Guid sessionId, CancellationToken cancellationToken)
        => Ok(await service.GetTimelineAsync(TenantId, sessionId, cancellationToken));

    // Full persisted decision result for a session, including the V2 dependency graph when present.
    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Session(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await service.GetSessionResultAsync(TenantId, sessionId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // POLOXI Legal V2.1 — synchronous closed loop: apply one edge verification change and run
    // dependency propagation → Candidate×Branch recompetition → frontier/IV → ResearchNeed.
    [HttpPost("sessions/{sessionId:guid}/verify")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Verify(Guid sessionId, [FromBody] DecisionVerificationChangeRequest request, CancellationToken cancellationToken)
        => Ok(await service.ApplyVerificationChangeAsync(TenantId, ActorUserId, sessionId, request, cancellationToken));

    // POLOXI Bounded Research Loop — cockpit entrypoint ("Research this decision"). Runs the autonomous
    // Retrieval → Verification → Evidence Promotion → Dependency Update → Recompetition loop until an
    // explicit STOP condition. No-op (LOOP_DISABLED) unless the research-loop feature flag is enabled.
    [HttpPost("sessions/{sessionId:guid}/research")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Research(Guid sessionId, CancellationToken cancellationToken)
        => Ok(await service.RunResearchLoopAsync(TenantId, ActorUserId, sessionId, cancellationToken));
}
