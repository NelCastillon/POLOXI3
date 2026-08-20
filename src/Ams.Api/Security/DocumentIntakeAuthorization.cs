using Microsoft.AspNetCore.Authorization;

namespace Ams.Api.Security;

public static class DocumentIntakePolicies
{
    public const string Read = "DMS.INTAKE.READ";
    public const string Upload = "DMS.INTAKE.UPLOAD";
    public const string Review = "DMS.INTAKE.REVIEW";
    public const string Reprocess = "DMS.INTAKE.REPROCESS";
    public const string Promote = "DMS.INTAKE.PROMOTE";
    public const string Admin = "DMS.INTAKE.ADMIN";
}

public sealed class DocumentIntakePermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class DocumentIntakePermissionAuthorizationHandler : AuthorizationHandler<DocumentIntakePermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DocumentIntakePermissionRequirement requirement)
    {
        if (context.User.Identity?.AuthenticationType == DevelopmentAuthenticationHandler.SchemeName
            || context.User.HasClaim("permission", requirement.Permission)
            || context.User.HasClaim("permission", "NAV_ALL")
            || context.User.IsInRole("SYSTEM_ADMIN")
            || context.User.IsInRole("TENANT_ADMIN"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
