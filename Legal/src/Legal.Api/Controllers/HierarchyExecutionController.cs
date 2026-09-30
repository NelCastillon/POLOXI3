using Legal.Api.Security;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// POLOXI Hierarchy Execution Lineage & Authority API (schema migration 0366). Surfaces the immutable
// per-run hierarchy history for a decision context and the explicit authority-promotion action.
// Tenant-scoped through AuthenticatedRequestContext; POLOXI scoring is untouched by this controller.
[ApiController]
[Route("api/legal_decision/hierarchy")]
public sealed class HierarchyExecutionController(ILegalHierarchyExecutionRepository repository) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    // Lists every RUN for a decision context (matter + contract + version), newest run first.
    [HttpGet("runs")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Runs(
        [FromQuery] Guid matterId, [FromQuery] Guid contractId, [FromQuery] int contractVersion,
        CancellationToken cancellationToken)
        => Ok(await repository.GetRunsAsync(TenantId, matterId, contractId, contractVersion, cancellationToken));

    // The current authoritative execution pointer for a decision context (204 when none promoted yet).
    [HttpGet("authority")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Authority(
        [FromQuery] Guid matterId, [FromQuery] Guid contractId, [FromQuery] int contractVersion,
        CancellationToken cancellationToken)
    {
        var authority = await repository.GetCurrentAuthorityAsync(TenantId, matterId, contractId, contractVersion, cancellationToken);
        return authority is null ? NoContent() : Ok(authority);
    }

    // One execution header plus its ordered node lineage.
    [HttpGet("executions/{hierarchyExecutionId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Execution(Guid hierarchyExecutionId, CancellationToken cancellationToken)
    {
        var detail = await repository.GetExecutionAsync(TenantId, hierarchyExecutionId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    // Promote a validated execution to AUTHORITATIVE for its decision context.
    [HttpPost("executions/{hierarchyExecutionId:guid}/promote")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> Promote(
        Guid hierarchyExecutionId, [FromBody] PromoteHierarchyAuthorityRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PromoteHierarchyAuthorityCommand(
            hierarchyExecutionId, request.RowVersion, request.AuthorityReasonCode);
        try
        {
            var result = await repository.PromoteAuthorityAsync(TenantId, ActorUserId, command, cancellationToken);
            return Ok(result);
        }
        catch (HierarchyExecutionConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (HierarchyExecutionStateException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

// Body for a promotion request: RowVersion enforces optimistic concurrency on the target execution.
public sealed record PromoteHierarchyAuthorityRequest(byte[] RowVersion, string AuthorityReasonCode);
