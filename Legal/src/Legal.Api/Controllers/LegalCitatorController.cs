using Legal.Api.Security;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Citator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Judz Citator API — good-law validity lookup for cited authorities. Returns the computed validity
// flag (Good Law / Caution / Negative / Unknown) and recorded treatments for the requested authority
// identities. All data is DB-backed and tenant-scoped from the authenticated context. Advisory only.
[ApiController]
[Route("api/legal_citator")]
public sealed class LegalCitatorController(ICitatorService citatorService) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    public sealed record CitatorValidityRequest(IReadOnlyList<string> AuthorityIdentities);

    // Resolves the validity flag for each requested authority identity (one result per input,
    // including Unknown for authorities without citator data).
    [HttpPost("validity")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetValidity(CitatorValidityRequest request, CancellationToken cancellationToken)
        => Ok(await citatorService.GetValidityAsync(TenantId, request.AuthorityIdentities ?? [], cancellationToken));

    // ── Management surface (DB-backed treatment authoring) ──

    // The DB-backed treatment-code vocabulary a user picks from when recording a treatment.
    [HttpGet("treatment-codes")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetTreatmentCodes(CancellationToken cancellationToken)
        => Ok(await citatorService.GetTreatmentCodesAsync(cancellationToken));

    // All recorded treatments for a single cited authority (newest first).
    [HttpGet("treatments")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ListTreatments([FromQuery] string authority, CancellationToken cancellationToken)
        => Ok(await citatorService.ListTreatmentsAsync(TenantId, authority ?? string.Empty, cancellationToken));

    // Records or updates one treatment. The implied validity flag is derived server-side from the code.
    [HttpPost("treatments")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveTreatment([FromBody] SaveCitationTreatmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await citatorService.SaveTreatmentAsync(TenantId, ActorUserId, request, cancellationToken);
            return Ok(new { CitationTreatmentId = id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // Soft-deletes a treatment row. 404 if nothing changed.
    [HttpDelete("treatments/{citationTreatmentId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteTreatment(Guid citationTreatmentId, CancellationToken cancellationToken)
    {
        var deleted = await citatorService.DeleteTreatmentAsync(TenantId, ActorUserId, citationTreatmentId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
