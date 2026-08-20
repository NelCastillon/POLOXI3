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

    public static bool CanManageAccounts(ClaimsPrincipal user, Guid tenantId)
        => HasTenantAccess(user, tenantId)
            && HasAnyPermission(user, "ACCOUNT_MANAGE", "ACCOUNT_EDIT", "ACCOUNT_CONFIG_MANAGE");

    public static bool CanViewPremiumFinance(ClaimsPrincipal user, Guid tenantId)
        => HasTenantAccess(user, tenantId)
            && HasAnyPermission(user, "PREMIUM_FINANCE_VIEW", "PREMIUM_FINANCE_MANAGE", "POLICY_VIEW", "POLICY_MANAGE", "BILLING_VIEW", "BILLING_MANAGE");

    public static bool CanManagePremiumFinance(ClaimsPrincipal user, Guid tenantId)
        => HasTenantAccess(user, tenantId)
            && HasAnyPermission(user, "PREMIUM_FINANCE_MANAGE", "POLICY_MANAGE", "POLICY_EDIT", "BILLING_MANAGE");

    public static bool HasPolicyEndorsementPermission(ClaimsPrincipal user, Guid tenantId, params string[] permissions)
        => HasTenantAccess(user, tenantId)
            && HasAnyPermission(user, [.. permissions, "ENDORSEMENT_MANAGE"]);

    public static bool CanAccessPolicyEndorsementWorkflow(ClaimsPrincipal user, Guid tenantId)
        => HasTenantAccess(user, tenantId)
            && GetGrantedPermissions(user).Any(permission =>
                permission == "NAV_ALL" || permission.StartsWith("ENDORSEMENT_", StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyCollection<string> GetGrantedPermissions(ClaimsPrincipal user)
    {
        var permissions = user.FindAll("permission")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (user.Identity?.AuthenticationType == "Development" || user.IsInRole("SYSTEM_ADMIN") || user.IsInRole("TENANT_ADMIN"))
            permissions.Add("NAV_ALL");

        return permissions;
    }

    public static Guid? GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("user_id")
            ?? user.FindFirstValue("userId")
            ?? user.FindFirstValue("UserId");

        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    public static Guid? GetTenantId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue("tenant_id")
            ?? user.FindFirstValue("tenantId")
            ?? user.FindFirstValue("TenantId");

        return Guid.TryParse(claim, out var tenantId) ? tenantId : null;
    }

    public static bool HasTenantAccess(ClaimsPrincipal user, Guid tenantId)
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
