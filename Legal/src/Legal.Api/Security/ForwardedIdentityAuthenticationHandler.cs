using System.Security.Claims;
using System.Text.Encodings.Web;
using Legal.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Legal.Api.Security;

// ─────────────────────────────────────────────────────────────────────────────
// Production authentication scheme for forwarded acting identities.
//
// Unlike DevelopmentAuthenticationHandler, this scheme REQUIRES a valid HMAC
// signature (shared secret) over the X-Acting-* headers before trusting them,
// closing the header-spoofing gap. There is no demo fallback and no all-access
// bypass: the identity carries only the forwarded permission claims, so
// per-capability enforcement always applies. Requests without a valid signature
// fail authentication (fail closed).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ForwardedIdentityAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ForwardedIdentity";
    public const string AuthenticationType = DevelopmentAuthenticationHandler.ForwardedAuthenticationType;

    private readonly string? _sharedSecret;

    public ForwardedIdentityAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _sharedSecret = configuration["Security:ActingIdentity:SharedSecret"];
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrWhiteSpace(_sharedSecret))
            return Task.FromResult(AuthenticateResult.Fail("Acting-identity shared secret is not configured."));

        var actingUserId = Request.Headers["X-Acting-User-Id"].ToString();
        var actingUserName = Request.Headers["X-Acting-User-Name"].ToString();
        var actingUserEmail = Request.Headers["X-Acting-User-Email"].ToString();
        var actingTenantId = Request.Headers["X-Acting-Tenant-Id"].ToString();
        var actingRole = Request.Headers["X-Acting-Role"].ToString();
        var actingPermissions = Request.Headers["X-Acting-Permissions"].ToString();
        var timestampHeader = Request.Headers[ActingIdentitySignature.TimestampHeader].ToString();
        var signature = Request.Headers[ActingIdentitySignature.SignatureHeader].ToString();

        if (!long.TryParse(timestampHeader, out var unixTimeSeconds))
            return Task.FromResult(AuthenticateResult.Fail("Missing or invalid acting-identity timestamp."));

        // Payload must be reconstructed from the exact (escaped) header values the Web tier signed.
        var payload = ActingIdentitySignature.BuildPayload(
            unixTimeSeconds, actingUserId, actingUserName, actingUserEmail, actingTenantId, actingRole, actingPermissions);

        if (!ActingIdentitySignature.Verify(payload, signature, _sharedSecret, unixTimeSeconds, DateTimeOffset.UtcNow))
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired acting-identity signature."));

        if (!Guid.TryParse(actingUserId, out var userId) || userId == Guid.Empty)
            return Task.FromResult(AuthenticateResult.Fail("Forwarded acting user is missing."));
        if (!Guid.TryParse(actingTenantId, out var tenantId) || tenantId == Guid.Empty)
            return Task.FromResult(AuthenticateResult.Fail("Forwarded acting tenant is missing."));

        var userName = Unescape(actingUserName);
        var userEmail = Unescape(actingUserEmail);
        var role = Unescape(actingRole);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(userName) ? userId.ToString() : userName),
            new("sub", userId.ToString()),
            new("tenant_id", tenantId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(userEmail))
            claims.Add(new Claim(ClaimTypes.Email, userEmail));

        if (!string.IsNullOrWhiteSpace(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in actingPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim("permission", permission));

        var identity = new ClaimsIdentity(claims, AuthenticationType);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static string? Unescape(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Uri.UnescapeDataString(value);
}
