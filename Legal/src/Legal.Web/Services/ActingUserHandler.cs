using System.Linq;
using System.Security.Claims;
using Legal.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Legal.Web.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Forwards the signed-in user's identity to Legal.Api on every outbound request.
//
// The API attributes actions to the acting user/tenant carried by X-Acting-*
// headers. In Production the API validates an HMAC signature over these headers
// (shared secret) to reject spoofed identities, so this handler also signs a
// canonical payload and attaches X-Acting-Timestamp / X-Acting-Signature.
// Identity is read from the current HttpContext principal established by the
// cookie sign-in.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ActingUserHandler(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        // Forward the real browser IP / User-Agent to the API so login history and
        // audit rows record the end user, not this server-side HttpClient. This must
        // run even for anonymous requests (e.g. the login POST) where no principal exists.
        if (httpContext is not null)
        {
            var clientIp = httpContext.Connection.RemoteIpAddress;
            if (clientIp is not null)
            {
                if (clientIp.IsIPv4MappedToIPv6)
                    clientIp = clientIp.MapToIPv4();
                SetHeader(request, "X-Forwarded-For", clientIp.ToString());
            }

            var browserUserAgent = httpContext.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrWhiteSpace(browserUserAgent))
            {
                request.Headers.Remove("User-Agent");
                request.Headers.TryAddWithoutValidation("User-Agent", browserUserAgent);
            }
        }

        var user = httpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            var userName = user.FindFirstValue(ClaimTypes.Name);
            var email = user.FindFirstValue(ClaimTypes.Email);
            var tenantId = user.FindFirstValue("tenant_id");
            var role = Escape(user.FindFirstValue(ClaimTypes.Role));
            var permissions = string.Join(',', user.FindAll("permission").Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)));

            SetHeader(request, "X-Acting-User-Id", userId);
            SetHeader(request, "X-Acting-User-Name", Escape(userName));
            SetHeader(request, "X-Acting-User-Email", Escape(email));
            SetHeader(request, "X-Acting-Tenant-Id", tenantId);
            SetHeader(request, "X-Acting-Role", role);
            SetHeader(request, "X-Acting-Permissions", permissions);

            // Sign the forwarded identity so the API can reject spoofed headers.
            var secret = configuration["Security:ActingIdentity:SharedSecret"];
            if (!string.IsNullOrWhiteSpace(secret))
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var payload = ActingIdentitySignature.BuildPayload(
                    timestamp, userId, Escape(userName), Escape(email), tenantId, role, permissions);
                SetHeader(request, ActingIdentitySignature.TimestampHeader, timestamp.ToString());
                SetHeader(request, ActingIdentitySignature.SignatureHeader, ActingIdentitySignature.Sign(payload, secret));
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void SetHeader(HttpRequestMessage request, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        request.Headers.Remove(name);
        request.Headers.Add(name, value);
    }

    private static string? Escape(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Uri.EscapeDataString(value);
}
