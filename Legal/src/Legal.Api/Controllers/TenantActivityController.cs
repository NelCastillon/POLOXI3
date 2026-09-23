using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Tenant Activity read surface (/admin/activity).
//
// Scope: tenant admins are restricted to their own tenant. System Admins may
// explicitly select a member tenant from the platform-wide member list. Gated by
// the seeded Members.Manage permission (NAV_ALL also passes). Exposes views over
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

    private Guid UserId => AuthenticatedRequestContext.GetUserId(User)
        ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    private bool CanManage()
    {
        var granted = AuthenticatedRequestContext.GetGrantedPermissions(User);
        return granted.Contains("NAV_ALL")
            || granted.Contains(ManagePermission, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsSystemAdmin()
        => AuthenticatedRequestContext.IsSystemAdmin(User);

    // Never honor a caller-supplied tenant for ordinary tenant administrators.
    private Guid ResolveMemberTenant(Guid? requestedTenantId)
        => IsSystemAdmin() && requestedTenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : TenantId;

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditEvents([FromQuery] int days = 30, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        if (!CanManage()) return Forbid();
        var events = await service.ListAuditEventsAsync(TenantId, days, take, cancellationToken);
        return Ok(events);
    }

    [HttpGet("me/audit/page")]
    public async Task<IActionResult> PageMyAuditEvents(
        [FromQuery] int days = 30,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var events = await service.PageAuditEventsForUserAsync(TenantId, UserId, days, search, page, pageSize, cancellationToken);
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

    [HttpGet("me/audit")]
    public async Task<IActionResult> GetMyAuditEvents([FromQuery] int days = 30, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var events = await service.ListAuditEventsForUserAsync(TenantId, UserId, days, take, cancellationToken);
        return Ok(events);
    }

    [HttpGet("users/{userId:guid}/audit/page")]
    public async Task<IActionResult> PageUserAuditEvents(
        Guid userId,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int days = 30,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!CanManage()) return Forbid();
        var events = await service.PageAuditEventsForUserAsync(ResolveMemberTenant(tenantId), userId, days, search, page, pageSize, cancellationToken);
        return Ok(events);
    }

    [HttpGet("me/usage")]
    public async Task<IActionResult> GetMyUsage([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var usage = await service.SummarizeUsageForUserAsync(TenantId, UserId, days, cancellationToken);
        return Ok(usage);
    }

    [HttpGet("users/{userId:guid}/audit")]
    public async Task<IActionResult> GetUserAuditEvents(Guid userId, [FromQuery] Guid? tenantId = null, [FromQuery] int days = 30, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        if (!CanManage()) return Forbid();
        var events = await service.ListAuditEventsForUserAsync(ResolveMemberTenant(tenantId), userId, days, take, cancellationToken);
        return Ok(events);
    }

    [HttpGet("users/{userId:guid}/usage")]
    public async Task<IActionResult> GetUserUsage(Guid userId, [FromQuery] Guid? tenantId = null, [FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        if (!CanManage()) return Forbid();
        var usage = await service.SummarizeUsageForUserAsync(ResolveMemberTenant(tenantId), userId, days, cancellationToken);
        return Ok(usage);
    }
}
