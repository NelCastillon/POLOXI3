using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Commissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/commission-accounting")]
public sealed class CommissionAccountingController(ICommissionAccountingService service) : ControllerBase
{
    [HttpGet("workspace")]
    public async Task<IActionResult> GetWorkspace([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => TryTenant(tenantId, out var denied) ? Ok(await service.GetWorkspaceAsync(tenantId, cancellationToken)) : denied;

    [HttpGet("statements/{statementId:guid}/lines")]
    public async Task<IActionResult> GetStatementLines(Guid statementId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => TryTenant(tenantId, out var denied) ? Ok(await service.GetStatementLinesAsync(tenantId, statementId, cancellationToken)) : denied;

    [HttpPost("statements/import")]
    public async Task<IActionResult> ImportStatement([FromBody] ImportCarrierCommissionStatementRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var result = await service.ImportStatementAsync(request with { ImportedByUserId = GetUserId() }, cancellationToken);
        return Created($"api/commission-accounting/statements/{result.CarrierCommissionStatementId}", result);
    }

    [HttpPost("statements/{statementId:guid}/match")]
    public async Task<IActionResult> RunMatching(Guid statementId, [FromBody] RunCommissionMatchingRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        return Ok(await service.RunMatchingAsync(request with { CarrierCommissionStatementId = statementId, UserId = GetUserId() }, cancellationToken));
    }

    [HttpPost("matches/{matchId:guid}/approve")]
    public async Task<IActionResult> ApproveMatch(Guid matchId, [FromBody] ApproveCommissionMatchRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        await service.ApproveMatchAsync(request with { CommissionReconciliationMatchId = matchId, ApprovedByUserId = GetUserId() }, cancellationToken);
        return NoContent();
    }

    [HttpPost("exceptions/{exceptionId:guid}/resolve")]
    public async Task<IActionResult> ResolveException(Guid exceptionId, [FromBody] ResolveCommissionReconciliationExceptionRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        await service.ResolveExceptionAsync(request with { CommissionReconciliationExceptionId = exceptionId, ResolvedByUserId = GetUserId() }, cancellationToken);
        return NoContent();
    }

    [HttpPost("payables/generate")]
    public async Task<IActionResult> CreatePayables([FromBody] CreateCommissionPayableBatchRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        return Ok(await service.CreatePayablesAsync(request with { CreatedByUserId = GetUserId() }, cancellationToken));
    }

    [HttpPost("payables/{payableId:guid}/approve")]
    public async Task<IActionResult> ApprovePayable(Guid payableId, [FromBody] ApproveCommissionPayableRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        await service.ApprovePayableAsync(request with { CommissionPayableId = payableId, ApprovedByUserId = GetUserId() }, cancellationToken);
        return NoContent();
    }

    [HttpPost("expected-receivables/synchronize")]
    public async Task<IActionResult> SynchronizeExpectedReceivables([FromBody] SynchronizeCommissionExpectedReceivablesRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var synchronized = await service.SynchronizeExpectedReceivablesAsync(request with { UserId = GetUserId() }, cancellationToken);
        return Ok(new { synchronized });
    }

    private bool TryTenant(Guid requestedTenantId, out IActionResult denied)
    {
        denied = Forbid();
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        return requestedTenantId != Guid.Empty && Guid.TryParse(claim, out var authenticatedTenantId) && authenticatedTenantId == requestedTenantId;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
