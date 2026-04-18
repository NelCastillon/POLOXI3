using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Iam;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/iam")]
public sealed class IamController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly IUserRoleService _userRoleService;
    private readonly IUserScopeService _userScopeService;
    private readonly ISecurityPolicyService _securityPolicyService;
    private readonly IUserService _userService;
    private readonly IRoleBundleService _roleBundleService;
    private readonly IUserPermissionService _userPermissionService;

    public IamController(
        IPermissionService permissionService,
        IUserRoleService userRoleService,
        IUserScopeService userScopeService,
        ISecurityPolicyService securityPolicyService,
        IUserService userService,
        IRoleBundleService roleBundleService,
        IUserPermissionService userPermissionService)
    {
        _permissionService = permissionService;
        _userRoleService = userRoleService;
        _userScopeService = userScopeService;
        _securityPolicyService = securityPolicyService;
        _userService = userService;
        _roleBundleService = roleBundleService;
        _userPermissionService = userPermissionService;
    }

    // ── Permissions ───────────────────────────────────────────────────────────

    [HttpGet("permissions/{id:guid}")]
    public async Task<IActionResult> GetPermissionById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _permissionService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> SearchPermissions([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] string? resourceCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _permissionService.SearchAsync(tenantId, searchTerm, resourceCode, pageNumber, pageSize, cancellationToken));

    [HttpPost("permissions")]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        var id = await _permissionService.CreateAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPatch("permissions/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivatePermission(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _permissionService.DeactivateAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    // ── Role Permissions ──────────────────────────────────────────────────────

    [HttpGet("permissions/{id:guid}/roles")]
    public async Task<IActionResult> GetPermissionRoles(Guid id, CancellationToken cancellationToken)
        => Ok(await _permissionService.GetByPermissionAsync(id, cancellationToken));

    [HttpGet("matrix")]
    public async Task<IActionResult> GetMatrix([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _permissionService.GetMatrixAsync(tenantId, cancellationToken));

    [HttpGet("permissions/{id:guid}/direct-users")]
    public async Task<IActionResult> GetPermissionDirectUsers(Guid id, CancellationToken cancellationToken)
        => Ok(await _userService.GetDirectUsersByPermissionAsync(id, cancellationToken));

    [HttpGet("roles/{roleId:guid}/permissions")]
    public async Task<IActionResult> GetRolePermissions(Guid roleId, CancellationToken cancellationToken)
        => Ok(await _permissionService.GetByRoleAsync(roleId, cancellationToken));

    [HttpPost("role-permissions")]
    public async Task<IActionResult> AssignPermissionToRole([FromBody] AssignRolePermissionRequest request, CancellationToken cancellationToken)
    {
        var id = await _permissionService.AssignToRoleAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("role-permissions")]
    public async Task<IActionResult> RevokePermissionFromRole([FromBody] RevokeRolePermissionRequest request, CancellationToken cancellationToken)
    {
        await _permissionService.RevokeFromRoleAsync(request, cancellationToken);
        return NoContent();
    }

    // ── User Roles ────────────────────────────────────────────────────────────

    [HttpGet("user-roles")]
    public async Task<IActionResult> SearchUserRoles([FromQuery] Guid tenantId, [FromQuery] Guid? userId, [FromQuery] Guid? roleId, [FromQuery] bool? isActive, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _userRoleService.SearchAsync(tenantId, userId, roleId, isActive, pageNumber, pageSize, cancellationToken));

    [HttpPost("user-roles")]
    public async Task<IActionResult> AssignRole([FromBody] AssignUserRoleRequest request, CancellationToken cancellationToken)
    {
        var id = await _userRoleService.AssignAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("user-roles")]
    public async Task<IActionResult> RevokeUserRole([FromBody] RevokeUserRoleRequest request, CancellationToken cancellationToken)
    {
        await _userRoleService.RevokeAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("user-roles/{id:guid}")]
    public async Task<IActionResult> RemoveRole(Guid id, [FromBody] RemoveRoleAssignmentRequest request, CancellationToken cancellationToken)
    {
        request.UserRoleId = id;
        await _userRoleService.RemoveAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("user-roles/{id:guid}/approve")]
    public async Task<IActionResult> ApproveRoleAssignment(Guid id, [FromBody] ApproveRoleAssignmentRequest request, CancellationToken cancellationToken)
    {
        request.UserRoleId = id;
        await _userRoleService.ApproveAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("user-roles/{id:guid}/extend")]
    public async Task<IActionResult> ExtendRoleAssignment(Guid id, [FromBody] ExtendRoleAssignmentRequest request, CancellationToken cancellationToken)
    {
        request.UserRoleId = id;
        await _userRoleService.ExtendAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("user-roles/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeRoleAssignment(Guid id, [FromBody] RevokeUserRoleRequest request, CancellationToken cancellationToken)
    {
        request.UserRoleId = id;
        await _userRoleService.RevokeAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("users/{userId:guid}/effective-permissions")]
    public async Task<IActionResult> GetEffectivePermissions(Guid userId, CancellationToken cancellationToken)
        => Ok(await _userRoleService.GetEffectivePermissionsAsync(userId, cancellationToken));

    // ── User Scopes ───────────────────────────────────────────────────────────

    [HttpGet("user-scopes")]
    public async Task<IActionResult> SearchUserScopes([FromQuery] Guid tenantId, [FromQuery] Guid? userId, [FromQuery] string? scopeTypeCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _userScopeService.SearchAsync(tenantId, userId, scopeTypeCode, pageNumber, pageSize, cancellationToken));

    [HttpGet("users/{userId:guid}/scopes")]
    public async Task<IActionResult> GetUserScopes(Guid userId, CancellationToken cancellationToken)
        => Ok(await _userScopeService.GetByUserAsync(userId, cancellationToken));

    [HttpPost("user-scopes")]
    public async Task<IActionResult> AssignUserScope([FromBody] AssignUserScopeRequest request, CancellationToken cancellationToken)
    {
        var id = await _userScopeService.AssignAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("user-scopes/{id:guid}")]
    public async Task<IActionResult> RevokeUserScope(Guid id, [FromQuery] Guid? revokedByUserId, CancellationToken cancellationToken)
    {
        await _userScopeService.RevokeAsync(id, revokedByUserId, cancellationToken);
        return NoContent();
    }

    // ── Security Policies ─────────────────────────────────────────────────────

    [HttpGet("security-policies/{id:guid}")]
    public async Task<IActionResult> GetSecurityPolicyById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _securityPolicyService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("security-policies")]
    public async Task<IActionResult> SearchSecurityPolicies([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] string? resourceCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _securityPolicyService.SearchAsync(tenantId, searchTerm, resourceCode, pageNumber, pageSize, cancellationToken));

    [HttpPost("security-policies")]
    public async Task<IActionResult> CreateSecurityPolicy([FromBody] CreateSecurityPolicyRequest request, CancellationToken cancellationToken)
    {
        var id = await _securityPolicyService.CreateAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPatch("security-policies/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateSecurityPolicy(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _securityPolicyService.DeactivateAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    // ── Role Bundles ──────────────────────────────────────────────────────────

    [HttpGet("role-bundles/{id:guid}")]
    public async Task<IActionResult> GetRoleBundleById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _roleBundleService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("role-bundles")]
    public async Task<IActionResult> SearchRoleBundles([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _roleBundleService.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost("role-bundles")]
    public async Task<IActionResult> CreateRoleBundle([FromBody] CreateRoleBundleRequest request, CancellationToken cancellationToken)
    {
        var id = await _roleBundleService.CreateAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("role-bundles/{id:guid}")]
    public async Task<IActionResult> UpdateRoleBundle(Guid id, [FromBody] UpdateRoleBundleRequest request, CancellationToken cancellationToken)
    {
        request.BundleId = id;
        await _roleBundleService.UpdateAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("role-bundles/{id:guid}/activate")]
    public async Task<IActionResult> ActivateRoleBundle(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _roleBundleService.SetActiveAsync(id, true, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("role-bundles/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateRoleBundle(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _roleBundleService.SetActiveAsync(id, false, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("role-bundles/{id:guid}/roles")]
    public async Task<IActionResult> GetBundleRoles(Guid id, CancellationToken cancellationToken)
        => Ok(await _roleBundleService.GetRolesAsync(id, cancellationToken));

    [HttpPut("role-bundles/{id:guid}/roles")]
    public async Task<IActionResult> SetBundleRoles(Guid id, [FromBody] SetBundleRolesRequest request, CancellationToken cancellationToken)
    {
        request.BundleId = id;
        await _roleBundleService.SetRolesAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("role-bundles/{id:guid}/assign-users")]
    public async Task<IActionResult> AssignBundleToUsers(Guid id, [FromBody] AssignBundleToUsersRequest request, CancellationToken cancellationToken)
    {
        request.BundleId = id;
        await _roleBundleService.AssignToUsersAsync(request, cancellationToken);
        return NoContent();
    }

    // ── Permission Overrides ──────────────────────────────────────────────────

    [HttpGet("permission-overrides")]
    public async Task<IActionResult> SearchPermissionOverrides([FromQuery] Guid tenantId, [FromQuery] Guid? userId, [FromQuery] Guid? permissionId, [FromQuery] bool? isGranted, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _userPermissionService.SearchAsync(tenantId, userId, permissionId, isGranted, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost("permission-overrides")]
    public async Task<IActionResult> GrantPermissionOverride([FromBody] GrantUserPermissionRequest request, CancellationToken cancellationToken)
    {
        var id = await _userPermissionService.GrantAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("permission-overrides/{id:guid}")]
    public async Task<IActionResult> UpdatePermissionOverride(Guid id, [FromBody] UpdateUserPermissionRequest request, CancellationToken cancellationToken)
    {
        request.UserPermissionId = id;
        await _userPermissionService.UpdateAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("permission-overrides/{id:guid}")]
    public async Task<IActionResult> RevokePermissionOverride(Guid id, [FromQuery] Guid? revokedByUserId, CancellationToken cancellationToken)
    {
        await _userPermissionService.RevokeAsync(id, revokedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("permission-overrides/conflicts")]
    public async Task<IActionResult> ValidatePermissionConflicts([FromQuery] Guid tenantId, [FromQuery] Guid? userId, CancellationToken cancellationToken)
        => Ok(await _userPermissionService.ValidateConflictsAsync(tenantId, userId, cancellationToken));

    [HttpGet("permission-overrides/effective-scope")]
    public async Task<IActionResult> PreviewEffectiveScope([FromQuery] Guid tenantId, [FromQuery] Guid userId, CancellationToken cancellationToken)
        => Ok(await _userPermissionService.PreviewEffectiveScopeAsync(tenantId, userId, cancellationToken));

    [HttpGet("permission-overrides/{id:guid}/scopes")]
    public async Task<IActionResult> GetPermissionScopes(Guid id, CancellationToken cancellationToken)
        => Ok(await _userPermissionService.GetScopesAsync(id, cancellationToken));

    [HttpPost("permission-overrides/{id:guid}/scopes")]
    public async Task<IActionResult> AddPermissionScope(Guid id, [FromBody] AddPermissionScopeRequest request, CancellationToken cancellationToken)
    {
        request.UserPermissionId = id;
        var scopeId = await _userPermissionService.AddScopeAsync(request, cancellationToken);
        return Ok(new { id = scopeId });
    }

    [HttpDelete("permission-overrides/{id:guid}/scopes/{scopeId:guid}")]
    public async Task<IActionResult> RemovePermissionScope(Guid id, Guid scopeId, CancellationToken cancellationToken)
    {
        await _userPermissionService.RemoveScopeAsync(scopeId, cancellationToken);
        return NoContent();
    }
}
