using Legal.Api.Security;
using Legal.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// Enterprise Error Log read surface (/admin/errorlogs).
//
// Scope: tenant admins see errors captured within their own tenant (plus global NULL-tenant rows such
// as startup/worker failures). System Admins may pass tenant=null to view all tenants. Gated by the
// Members.Manage permission (NAV_ALL also passes). Day windows and result counts are clamped in the
// repository. Read-only: this controller never mutates the log store.
// ───────────────────────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/error_logs")]
[Authorize]
public sealed class ErrorLogController(IErrorLogRepository repository) : ControllerBase
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

    private bool IsSystemAdmin() => AuthenticatedRequestContext.IsSystemAdmin(User);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int days = 7,
        [FromQuery] int take = 200,
        [FromQuery] string? module = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? search = null,
        [FromQuery] bool allTenants = false,
        CancellationToken cancellationToken = default)
    {
        if (!CanManage()) return Forbid();

        // Only a System Admin may view across every tenant; everyone else is pinned to their tenant.
        Guid? tenantScope = allTenants && IsSystemAdmin() ? null : TenantId;

        var items = await repository.ListAsync(
            tenantScope, days, take, module, severity, search, cancellationToken);
        return Ok(items);
    }
}
