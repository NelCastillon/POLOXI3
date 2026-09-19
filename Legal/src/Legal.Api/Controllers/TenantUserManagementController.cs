using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Tenant User Management control plane (/admin/users).
//
// Scope: the authenticated caller's OWN tenant only. Gated by the seeded
// Members.Manage permission (NAV_ALL also passes). A tenant admin may grant only
// the non-privileged tenant roles (OWNER/ADMIN/MEMBER/VIEWER) — enforced in
// UserManagementService. All actions are tenant-scoped so a tenant admin can
// never reach into another tenant or escalate to SUPERADMIN.
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/tenant_users")]
[Authorize]
public sealed class TenantUserManagementController(
    IUserManagementService service,
    IInvitationService invitationService) : ControllerBase
{
    private const string ManagePermission = "Members.Manage";

    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User)
        ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid? ActorUserId => AuthenticatedRequestContext.GetUserId(User);

    private bool CanManage()
    {
        var granted = AuthenticatedRequestContext.GetGrantedPermissions(User);
        return granted.Contains("NAV_ALL")
            || granted.Contains(ManagePermission, StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> GetMembers(CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var members = await service.ListMembersAsync(TenantId, cancellationToken);
        return Ok(members);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var roles = await service.ListAssignableRolesAsync(platformScope: false, cancellationToken);
        return Ok(roles);
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteMemberRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.InviteMemberAsync(TenantId, false, ActorUserId, request, cancellationToken));

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateMemberRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.CreateMemberAsync(TenantId, false, ActorUserId, request, cancellationToken));

    [HttpPut("role")]
    public async Task<IActionResult> ChangeRole([FromBody] ChangeMemberRoleRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.ChangeRoleAsync(TenantId, false, ActorUserId, request, cancellationToken));

    [HttpPut("status")]
    public async Task<IActionResult> ChangeStatus([FromBody] ChangeMemberStatusRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.ChangeStatusAsync(TenantId, false, ActorUserId, request, cancellationToken));

    [HttpDelete("{membershipId:guid}")]
    public async Task<IActionResult> Remove(Guid membershipId, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.RemoveMemberAsync(TenantId, false, ActorUserId, membershipId, cancellationToken));

    // ── Invitations (Phase B) ───────────────────────────────────────────────
    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var invitations = await invitationService.ListInvitationsAsync(TenantId, cancellationToken);
        return Ok(invitations);
    }

    [HttpPost("invitations")]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => invitationService.CreateInvitationAsync(TenantId, ActorUserId, request, cancellationToken));

    [HttpPost("invitations/{invitationId:guid}/resend")]
    public async Task<IActionResult> ResendInvitation(Guid invitationId, CancellationToken cancellationToken)
        => await ExecuteAsync(() => invitationService.ResendInvitationAsync(TenantId, ActorUserId, invitationId, cancellationToken));

    [HttpPost("invitations/{invitationId:guid}/revoke")]
    public async Task<IActionResult> RevokeInvitation(Guid invitationId, CancellationToken cancellationToken)
        => await ExecuteAsync(() => invitationService.RevokeInvitationAsync(TenantId, ActorUserId, invitationId, cancellationToken));

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
