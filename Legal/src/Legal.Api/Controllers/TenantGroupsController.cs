using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Tenant Groups control plane (/admin/groups).
//
// Scope: the authenticated caller's OWN tenant only. Gated by the seeded
// Members.Manage permission (NAV_ALL also passes). A group is a tenant-scoped
// collection that carries one or more platform roles; adding a user to a group
// grants that group's roles. All actions are tenant-scoped so a tenant admin can
// never reach into another tenant's groups.
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/tenant_groups")]
[Authorize]
public sealed class TenantGroupsController(IGroupService service) : ControllerBase
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
    public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var groups = await service.ListGroupsAsync(TenantId, cancellationToken);
        return Ok(groups);
    }

    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> GetGroup(Guid groupId, CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var group = await service.GetGroupAsync(TenantId, groupId, cancellationToken);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.CreateGroupAsync(TenantId, ActorUserId, request, cancellationToken));

    [HttpPut]
    public async Task<IActionResult> UpdateGroup([FromBody] UpdateGroupRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.UpdateGroupAsync(TenantId, ActorUserId, request, cancellationToken));

    [HttpDelete("{groupId:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid groupId, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.DeleteGroupAsync(TenantId, ActorUserId, groupId, cancellationToken));

    [HttpGet("{groupId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid groupId, CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        var members = await service.ListGroupMembersAsync(TenantId, groupId, cancellationToken);
        return Ok(members);
    }

    [HttpPost("{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> AddMember(Guid groupId, Guid userId, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.AddGroupMemberAsync(TenantId, ActorUserId, groupId, userId, cancellationToken));

    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid userId, CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.RemoveGroupMemberAsync(TenantId, ActorUserId, groupId, userId, cancellationToken));

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
