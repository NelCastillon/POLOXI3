using Legal.Application.Abstractions.Services;

namespace Legal.Api.Security;

// Host implementation of IEpistemicTenantAccessor: reads the authenticated tenant claim from the
// current HTTP request so DB-backed EpistemicAuthoritySettings resolve tenant overrides at runtime.
public sealed class HttpEpistemicTenantAccessor(IHttpContextAccessor httpContextAccessor) : IEpistemicTenantAccessor
{
    public Guid? TenantId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user is null ? null : AuthenticatedRequestContext.GetTenantId(user);
        }
    }
}
