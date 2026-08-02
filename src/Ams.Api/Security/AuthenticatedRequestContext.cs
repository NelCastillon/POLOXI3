using System.Security.Claims;

namespace Ams.Api.Security;

public static class AuthenticatedRequestContext
{
    public static bool CanViewPolicy(ClaimsPrincipal user, Guid tenantId)
        => HasTenantAccess(user, tenantId)
            && HasAnyPermission(user, "POLICY_VIEW", "POLICY_MANAGE", "POLICY_EDIT");

    public static bool CanManagePolicy(ClaimsPrincipal user, Guid tenantId)
        => HasTenantAccess(user, tenantId)
            && HasAnyPermission(user, "POLICY_MANAGE", "POLICY_EDIT");

    public static Guid? GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("user_id")
            ?? user.FindFirstValue("userId")
            ?? user.FindFirstValue("UserId");

        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static bool HasTenantAccess(ClaimsPrincipal user, Guid tenantId)
    {
        var claim = user.FindFirstValue("tenant_id")
            ?? user.FindFirstValue("tenantId")
            ?? user.FindFirstValue("TenantId");

        return tenantId != Guid.Empty
            && Guid.TryParse(claim, out var authenticatedTenantId)
            && authenticatedTenantId == tenantId;
    }

    private static bool HasAnyPermission(ClaimsPrincipal user, params string[] permissions)
        => user.Identity?.AuthenticationType == "Development"
            || user.HasClaim("permission", "NAV_ALL")
            || user.IsInRole("SYSTEM_ADMIN")
            || user.IsInRole("TENANT_ADMIN")
            || permissions.Any(permission => user.HasClaim("permission", permission));
}
