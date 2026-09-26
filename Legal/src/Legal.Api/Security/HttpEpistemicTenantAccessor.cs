using Legal.Application.Abstractions.Services;

namespace Legal.Api.Security;

// Host implementation of IEpistemicTenantAccessor: reads the authenticated tenant claim from the
// current HTTP request so DB-backed EpistemicAuthoritySettings resolve tenant overrides at runtime.
// Long-running Wide searches run on a background Task with their own DI scope and no HttpContext, so
// the tenant claim is unavailable there. Those scopes seed the ambient tenant explicitly via
// SetAmbientTenant so tenant-scoped retrieval (e.g. official legal authority sources) still resolves.
public sealed class HttpEpistemicTenantAccessor(IHttpContextAccessor httpContextAccessor) : IEpistemicTenantAccessor
{
    private Guid? _ambientTenantId;

    public Guid? TenantId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var claimTenant = user is null ? null : AuthenticatedRequestContext.GetTenantId(user);
            return claimTenant ?? _ambientTenantId;
        }
    }

    // Seed the tenant for scopes that run detached from an HTTP request (background Wide pipeline).
    // The HTTP claim always takes precedence when present; this is only a fallback for non-HTTP scopes.
    public void SetAmbientTenant(Guid tenantId) => _ambientTenantId = tenantId == Guid.Empty ? null : tenantId;
}
