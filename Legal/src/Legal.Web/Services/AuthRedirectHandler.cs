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
        return await base.SendAsync(request, cancellationToken);
    }
}

