using System.Security.Claims;

namespace Ams.Web.Services;

/// <summary>
/// Forwards the signed-in user's identity (the same user shown in the top header)
/// to the AMS API on every outgoing request so server-side auditing attributes
/// actions to the actual current user instead of a development placeholder.
/// </summary>
public sealed class ActingUserHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActingUserHeaderHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            SetHeader(request, "X-Acting-User-Id", user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value);
            SetHeader(request, "X-Acting-Tenant-Id", user.FindFirst("tenant_id")?.Value ?? user.FindFirst("tenantId")?.Value);
            SetHeader(request, "X-Acting-User-Name", Escape(user.Identity?.Name ?? user.FindFirst(ClaimTypes.Name)?.Value));
            SetHeader(request, "X-Acting-User-Email", Escape(user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("user_name")?.Value));
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void SetHeader(HttpRequestMessage request, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    private static string? Escape(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Uri.EscapeDataString(value);
}
