using Microsoft.AspNetCore.Authorization;

namespace Ams.Web.Security;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Permission) ||
            context.User.HasClaim("permission", "NAV_ALL") ||
            context.User.IsInRole("SYSTEM_ADMIN") ||
            context.User.IsInRole("TENANT_ADMIN"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
