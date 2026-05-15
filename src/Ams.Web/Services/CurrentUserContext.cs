using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ams.Web.Services;

public sealed class CurrentUserContext
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly NavigationManager _navigationManager;

    public CurrentUserContext(AuthenticationStateProvider authenticationStateProvider, NavigationManager navigationManager)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _navigationManager = navigationManager;
    }

    public async Task<CurrentUserContextModel?> GetAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var tenantId = GetClaimGuid(user, "tenant_id", "tenantId", "TenantId");
        var userId = GetClaimGuid(user, ClaimTypes.NameIdentifier, "sub", "user_id", "userId", "UserId");

        return tenantId.HasValue && userId.HasValue
            ? new CurrentUserContextModel(
                tenantId.Value,
                userId.Value,
                user.FindFirst("tenant_name")?.Value,
                user.FindFirst("tenant_code")?.Value,
                [.. user.FindAll(ClaimTypes.Role).Select(c => c.Value)])
            : null;
    }

    public async Task<CurrentUserContextModel> RequireAsync()
    {
        var context = await GetAsync();
        if (context is not null)
        {
            return context;
        }

        var relativeUri = _navigationManager.ToBaseRelativePath(_navigationManager.Uri);
        var returnUrl = string.IsNullOrWhiteSpace(relativeUri) ? "/" : $"/{relativeUri}";
        var loginUrl = $"/login?error={Uri.EscapeDataString("Your session is missing tenant context. Please sign in again.")}&returnUrl={Uri.EscapeDataString(returnUrl)}";
        _navigationManager.NavigateTo(loginUrl, forceLoad: true);

        return new CurrentUserContextModel(Guid.Empty, Guid.Empty, null, null, []);
    }

    private static Guid? GetClaimGuid(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return null;
    }
}

public sealed record CurrentUserContextModel(Guid TenantId, Guid UserId, string? TenantName, string? TenantCode, IReadOnlyList<string> RoleCodes)
{
    public bool IsTenantAdministrator => RoleCodes.Any(role =>
        string.Equals(role, "TENANT_ADMIN", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase));

    public Guid? UserScopeId => IsTenantAdministrator ? null : UserId;
}
