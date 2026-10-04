namespace Legal.Api.Security;

// Central switch for the "Development" authentication all-access bypass. The Development scheme
// authenticates every request (no login) and, by default, is treated as an all-access System Admin
// pinned to the demo tenant — convenient for local work but it effectively disables tenant scoping
// and per-capability permission checks.
//
// Set configuration "DevAuth:GrantSystemAdmin" = false to restore real tenancy/permission enforcement
// even under the Development scheme: dev sessions then behave like a normal authenticated user whose
// access is governed by the forwarded tenant claim, role and permission claims. Default is true so the
// existing open-dev experience is preserved unless explicitly turned off.
public static class DevAuthBypass
{
    public static bool GrantSystemAdmin { get; private set; } = true;

    public static void Configure(IConfiguration configuration)
        => GrantSystemAdmin = configuration.GetValue("DevAuth:GrantSystemAdmin", true);

    // True when the principal is the open Development scheme AND the all-access bypass is enabled.
    public static bool IsAllAccessDevelopment(System.Security.Claims.ClaimsPrincipal user)
        => GrantSystemAdmin && user.Identity?.AuthenticationType == DevelopmentAuthenticationHandler.SchemeName;
}
