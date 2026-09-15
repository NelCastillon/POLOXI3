using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Isolated controller for the self-contained POLOXI Legal Decision Intelligence module
// (/legal/decision). Evolves independently from the Intelligence Wide (/legal/search) controller.
[ApiController]
[Route("api/legal_decision")]
public sealed class LegalDecisionController(ILegalDecisionService service) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("decide")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Decide([FromBody] DecisionSearchRequest request, CancellationToken cancellationToken)
        => Ok(await service.DecideAsync(
            request with
            {
                TenantId = TenantId,
                UserId = ActorUserId,
                GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User)
            },
            cancellationToken));

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
}
