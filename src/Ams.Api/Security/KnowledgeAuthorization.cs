using Microsoft.AspNetCore.Authorization;

namespace Ams.Api.Security;

public static class KnowledgePolicies
{
    public const string ConceptsRead = "Knowledge.Concepts.Read";
    public const string ConceptsManage = "Knowledge.Concepts.Manage";
    public const string MappingsRead = "Knowledge.Mappings.Read";
    public const string MappingsManage = "Knowledge.Mappings.Manage";
    public const string MappingsApprove = "Knowledge.Mappings.Approve";
    public const string RulesManage = "Knowledge.Rules.Manage";
    public const string Publish = "Knowledge.Publish";
    public const string Import = "Knowledge.Import";
    public const string AuditRead = "Knowledge.Audit.Read";
}

public sealed class KnowledgePermissionRequirement : IAuthorizationRequirement
{
    public KnowledgePermissionRequirement(string permission) => Permission = permission;
    public string Permission { get; }
}

public sealed class KnowledgePermissionAuthorizationHandler : AuthorizationHandler<KnowledgePermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, KnowledgePermissionRequirement requirement)
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
