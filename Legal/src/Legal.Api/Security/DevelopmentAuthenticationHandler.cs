using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Legal.Api.Security;

public sealed class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Development";
    // Identity auth-type applied when the Web app forwards a real signed-in user
    // (with real permission claims). It intentionally differs from SchemeName so
    // the "Development" all-access bypass does NOT apply and per-capability
    // permission enforcement runs against the user's actual forwarded permissions.
    public const string ForwardedAuthenticationType = "Forwarded";
    private static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DevelopmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Prefer the acting user forwarded by the AMS Web app (the signed-in
        // user shown in the top header) so audit trails attribute actions to
        // the real current user. Fall back to the demo user when absent.
        var actingUserId = Request.Headers["X-Acting-User-Id"].ToString();
        var actingUserName = Unescape(Request.Headers["X-Acting-User-Name"].ToString());
        var actingUserEmail = Unescape(Request.Headers["X-Acting-User-Email"].ToString());
        var actingTenantId = Request.Headers["X-Acting-Tenant-Id"].ToString();
        var actingPermissions = Request.Headers["X-Acting-Permissions"].ToString();

        var userId = Guid.TryParse(actingUserId, out var forwardedId) && forwardedId != Guid.Empty
            ? forwardedId
            : DemoUserId;

        // A real forwarded user carries explicit permission claims; use a
        // non-Development auth type so the open dev bypass does not apply and
        // per-capability permissions are enforced. Absent forwarded permissions,
        // keep the "Development" demo identity for local development.
        var forwardedPermissions = actingPermissions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var authenticationType = forwardedPermissions.Length > 0 ? ForwardedAuthenticationType : SchemeName;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(actingUserName) ? "Development User" : actingUserName),
            new("sub", userId.ToString())
        };

        foreach (var permission in forwardedPermissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        if (!string.IsNullOrWhiteSpace(actingUserEmail))
        {
            claims.Add(new Claim(ClaimTypes.Email, actingUserEmail));
        }

        var effectiveTenantId = Guid.TryParse(actingTenantId, out var tenantId) && tenantId != Guid.Empty
            ? tenantId
            : DemoTenantId;
        claims.Add(new Claim("tenant_id", effectiveTenantId.ToString()));

        var identity = new ClaimsIdentity(claims, authenticationType);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static string? Unescape(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Uri.UnescapeDataString(value);
}
