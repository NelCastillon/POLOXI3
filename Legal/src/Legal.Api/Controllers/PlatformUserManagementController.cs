using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Platform User Management control plane (/platform/users) — Super Admin only.
//
// Scope: ALL tenants. Gated by NAV_ALL or the seeded platform.users.manage
// permission (SUPERADMIN holds every permission). Super Admin may target any
// tenant and grant any role, including the SUPERADMIN system role. Scope/role
// rules are enforced in UserManagementService (platformScope = true).
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/platform_users")]
[Authorize]
public sealed class PlatformUserManagementController(IUserManagementService service) : ControllerBase
{
    private const string ManagePermission = "platform.users.manage";

    private Guid? ActorUserId => AuthenticatedRequestContext.GetUserId(User);

    private bool CanManage()
    {
        var granted = AuthenticatedRequestContext.GetGrantedPermissions(User);
        return granted.Contains("NAV_ALL")
            || granted.Contains(ManagePermission, StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> GetMembers([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var scope = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId : null;
        var members = await service.ListMembersAsync(scope, cancellationToken);
        return Ok(members);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var roles = await service.ListAssignableRolesAsync(platformScope: true, cancellationToken);
        return Ok(roles);
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var tenants = await service.ListTenantsAsync(cancellationToken);
        return Ok(tenants);
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteMemberRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.InviteMemberAsync(null, true, ActorUserId, request, cancellationToken));

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateMemberRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.CreateMemberAsync(null, true, ActorUserId, request, cancellationToken));

    [HttpPut("role")]
    public async Task<IActionResult> ChangeRole([FromBody] ChangeMemberRoleRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.ChangeRoleAsync(null, true, ActorUserId, request, cancellationToken));

    [HttpPut("status")]
    public async Task<IActionResult> ChangeStatus([FromBody] ChangeMemberStatusRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.ChangeStatusAsync(null, true, ActorUserId, request, cancellationToken));

    [HttpDelete("{membershipId:guid}")]
    public async Task<IActionResult> Remove(Guid membershipId, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.RemoveMemberAsync(null, true, ActorUserId, membershipId, cancellationToken));

    private async Task<IActionResult> ExecuteAsync(Func<Task> action)
    {
        if (!CanManage()) return Forbid();
        try
        {
            await action();
            return NoContent();
        }
        catch (UserManagementForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        if (!CanManage()) return Forbid();
        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (UserManagementForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
