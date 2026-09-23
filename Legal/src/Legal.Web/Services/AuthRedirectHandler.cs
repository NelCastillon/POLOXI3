using System.Net;
using Microsoft.AspNetCore.Components;

namespace Legal.Web.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Centralized 401/403 → /login redirect for every ApiClient call.
//
// When the session is missing or expired (Unauthorized) or the API rejects the
// forwarded identity (Forbidden), the user should be returned to sign in from
// ANY page rather than each page handling the status individually. This handler
// inspects every outbound response and, on 401/403, forces a navigation to
// /login and short-circuits with an OperationCanceledException so the calling
// page stops processing the failed request.
//
// HttpClientFactory builds message handlers in their own DI scope, so resolving
// NavigationManager here yields an uninitialized RemoteNavigationManager. The
// real, initialized circuit NavigationManager is reached through the
// CircuitServicesAccessor, which publishes the active circuit's service provider
// via an AsyncLocal for the duration of each inbound circuit activity.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class AuthRedirectHandler(CircuitServicesAccessor circuitServicesAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // Auth endpoints legitimately return 401 for bad credentials / unverified
        // accounts; those responses must reach the sign-in UI, not trigger a redirect.
        var isAuthEndpoint = request.RequestUri?.AbsolutePath.Contains("/auth/", StringComparison.OrdinalIgnoreCase) == true;
        if (!isAuthEndpoint && response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            response.Dispose();
            var navigation = circuitServicesAccessor.Services?.GetService(typeof(NavigationManager)) as NavigationManager;
            navigation?.NavigateTo("/login", forceLoad: true);
            throw new OperationCanceledException("The session is no longer authenticated; redirecting to sign in.");
        }

        return response;
    }
}

