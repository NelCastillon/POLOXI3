using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Tenant Activity read surface (/admin/activity).
//
// Scope: the authenticated caller's OWN tenant only. Gated by the seeded
// Members.Manage permission (NAV_ALL also passes). Exposes read-only views over
// audit events, usage totals, and login history for security/compliance review.
// All queries are tenant-scoped so a tenant admin can never see another tenant's
// activity. Day windows and result counts are clamped in the service layer.
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/tenant_activity")]
[Authorize]
public sealed class TenantActivityController(IActivityService service) : ControllerBase
{
    private const string ManagePermission = "Members.Manage";

    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User)
        ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");

    private bool CanManage()
    {
        var granted = AuthenticatedRequestContext.GetGrantedPermissions(User);
        return granted.Contains("NAV_ALL")
            || granted.Contains(ManagePermission, StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditEvents([FromQuery] int days = 30, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        if (!CanManage()) return Forbid();
        var events = await service.ListAuditEventsAsync(TenantId, days, take, cancellationToken);
        return Ok(events);
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        if (!CanManage()) return Forbid();
        var usage = await service.SummarizeUsageAsync(TenantId, days, cancellationToken);
        return Ok(usage);
    }

    [HttpGet("logins")]
    public async Task<IActionResult> GetLoginHistory([FromQuery] int days = 30, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        if (!CanManage()) return Forbid();
        var logins = await service.ListLoginHistoryAsync(TenantId, days, take, cancellationToken);
        return Ok(logins);
    }
}
