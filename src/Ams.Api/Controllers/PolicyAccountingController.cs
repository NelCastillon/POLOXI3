using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/policies/{policyId:guid}/accounting")]
public sealed class PolicyAccountingController(IPolicyAccountingService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid policyId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!CanAccess(tenantId)) return Forbid();
        var dashboard = await service.GetPolicyDashboardAsync(tenantId, policyId, cancellationToken);
        return dashboard is null ? NotFound() : Ok(dashboard);
    }

    [HttpPost("carrier-payables/{carrierPayableId:guid}/remit")]
    public async Task<IActionResult> Remit(Guid policyId, Guid carrierPayableId, [FromBody] RemitCarrierPayableRequest request, CancellationToken cancellationToken)
    {
        if (!CanAccess(request.TenantId) || !CanManageAccounting()) return Forbid();
        var dashboard = await service.GetPolicyDashboardAsync(request.TenantId, policyId, cancellationToken);
        if (dashboard?.CarrierPayableId != carrierPayableId) return NotFound();
        var actorUserId = GetAuthenticatedUserId();
        var authenticatedRequest = request with { UserId = actorUserId };
        return Ok(new { JournalEntryId = await service.RemitCarrierPayableAsync(carrierPayableId, authenticatedRequest, cancellationToken) });
    }

    [HttpPost("invoices/{invoiceId:guid}/email")]
    public async Task<IActionResult> EmailInvoice(Guid policyId, Guid invoiceId, [FromBody] EmailPolicyInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!CanAccess(request.TenantId) || !CanManageAccounting()) return Forbid();
        var authenticatedRequest = request with { UserId = GetAuthenticatedUserId() };
        return Ok(await service.EmailInvoiceAsync(policyId, invoiceId, authenticatedRequest, cancellationToken));
    }

    private bool CanAccess(Guid tenantId)
    {
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        return tenantId != Guid.Empty
            && Guid.TryParse(claim, out var authenticatedTenantId)
            && authenticatedTenantId == tenantId
            && (User.HasClaim("permission", "ACCOUNTING_VIEW")
                || User.HasClaim("permission", "POLICY_VIEW")
                || User.HasClaim("permission", "NAV_ALL")
                || User.IsInRole("SYSTEM_ADMIN")
                || User.IsInRole("TENANT_ADMIN")
                || User.Identity?.AuthenticationType == "Development");
    }

    private bool CanManageAccounting()
        => User.HasClaim("permission", "ACCOUNTING_MANAGE")
            || User.HasClaim("permission", "NAV_ALL")
            || User.IsInRole("SYSTEM_ADMIN")
            || User.IsInRole("TENANT_ADMIN")
            || User.Identity?.AuthenticationType == "Development";

    private Guid? GetAuthenticatedUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("user_id")
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("UserId");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
