using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Iam;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _service;
    public UsersController(IUserService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => AuthenticatedRequestContext.HasTenantAccess(User, tenantId) ? Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken)) : Forbid();

    [HttpGet("job-titles")]
    public async Task<IActionResult> GetJobTitles([FromQuery] Guid tenantId, [FromQuery] Guid? departmentId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.HasTenantAccess(User, tenantId) ? Ok(await _service.GetJobTitlesAsync(tenantId, departmentId, cancellationToken)) : Forbid();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        request.UserId = id;
        await _service.UpdateAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.SetActiveAsync(id, true, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.SetActiveAsync(id, false, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id, [FromQuery] DateTime? lockoutEnd, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.LockAsync(id, lockoutEnd, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.UnlockAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/mfa")]
    public async Task<IActionResult> SetMfa(Guid id, [FromQuery] bool enabled, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.SetMfaAsync(id, enabled, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/branch")]
    public async Task<IActionResult> AssignBranch(Guid id, [FromQuery] Guid? branchId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.AssignBranchAsync(id, branchId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/permissions")]
    public async Task<IActionResult> GetDirectPermissions(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetDirectPermissionsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/permissions")]
    public async Task<IActionResult> GrantPermission(Guid id, [FromBody] GrantUserPermissionRequest request, CancellationToken cancellationToken)
    {
        request.UserId = id;
        var permId = await _service.GrantPermissionAsync(request, cancellationToken);
        return Ok(new { id = permId });
    }

    [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
    public async Task<IActionResult> RevokePermission(Guid id, Guid permissionId, [FromQuery] Guid? revokedByUserId, CancellationToken cancellationToken)
    {
        await _service.RevokePermissionAsync(permissionId, revokedByUserId, cancellationToken);
        return NoContent();
    }
}
