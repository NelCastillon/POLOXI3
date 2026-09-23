using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Owner-only Account Settings (/account/settings) — organization/tenant profile.
//
// Scope: the authenticated caller's OWN tenant only, and ONLY when that caller
// holds the OWNER role. No permission grant widens this surface; a tenant ADMIN,
// MEMBER, or VIEWER is forbidden, and no one can reach another tenant. The Owner
// may view and edit the tenant Name and Slug; all validation and the unique-slug
// rule are enforced in TenantProfileService.
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/account_settings")]
[Authorize]
public sealed class AccountSettingsController(ITenantProfileService service) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User)
        ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid? ActorUserId => AuthenticatedRequestContext.GetUserId(User);

    // Only the tenant OWNER may view or edit the organization profile.
    private bool IsOwner()
        => User.IsInRole("OWNER")
        || AuthenticatedRequestContext.GetGrantedPermissions(User).Contains("NAV_ALL");

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!IsOwner()) return Forbid();
        var profile = await service.GetProfileAsync(TenantId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateTenantProfileRequest request, CancellationToken cancellationToken)
    {
        if (!IsOwner()) return Forbid();
        try
        {
            var updated = await service.UpdateProfileAsync(TenantId, ActorUserId, request, cancellationToken);
            return Ok(updated);
        }
        catch (UserManagementForbiddenException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
