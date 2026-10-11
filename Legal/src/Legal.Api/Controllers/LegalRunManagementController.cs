using Legal.Api.Security;
using Legal.Application.Abstractions.Intelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Judz Run & Assessment Management API — read access to the per-matter execution ledger and the
// guarded current-assessment pointer. All data is DB-backed and tenant-scoped from the authenticated
// context. Write/execution flows (coordinator, publication, reassessment) are later milestones.
[ApiController]
[Route("api/legal_runs")]
public sealed class LegalRunManagementController(IAnalysisRunLedgerService ledgerService) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");

    private Guid? ActorUserId => AuthenticatedRequestContext.GetUserId(User);

    // Returns the full Run & Assessment Management page view model for a matter: matter identity,
    // current assessment pointer, and the run ledger with steps and attempts.
    [HttpGet("matters/{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetPage(Guid matterId, CancellationToken cancellationToken)
        => Ok(await ledgerService.GetPageAsync(TenantId, matterId, cancellationToken));

    // Soft-deletes a single run and its dependent history for the matter. Tenant-scoped, write-once.
    [HttpDelete("runs/{analysisRunId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> DeleteRun(Guid analysisRunId, CancellationToken cancellationToken)
    {
        var deleted = await ledgerService.DeleteRunAsync(TenantId, analysisRunId, ActorUserId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
