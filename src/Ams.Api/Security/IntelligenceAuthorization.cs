using Microsoft.AspNetCore.Authorization;

namespace Ams.Api.Security;

public static class IntelligencePolicies
{
    public const string Read="Intelligence.Read";
    public const string Search="Intelligence.Search";
    public const string Recommend="Intelligence.Recommend";
    public const string Review="Intelligence.Review";
    public const string Configure="Intelligence.Configure";
    public const string Evaluate="Intelligence.Evaluate";
    public const string AuditRead="Intelligence.Audit.Read";
    public const string Analyze="Intelligence.Analyze";
    public const string Reason="Intelligence.Reason";
    public const string FindingsRead="Intelligence.Findings.Read";
    public const string FindingsReview="Intelligence.Findings.Review";
    public const string RelationshipsRead="Intelligence.Relationships.Read";
    public const string GovernanceManage="Intelligence.Governance.Manage";
    public static readonly string[] All=[Read,Search,Recommend,Review,Configure,Evaluate,AuditRead,Analyze,Reason,FindingsRead,FindingsReview,RelationshipsRead,GovernanceManage];
}

public sealed class IntelligencePermissionRequirement(string permission):IAuthorizationRequirement
{
    public string Permission{get;}=permission;
}

public sealed class IntelligencePermissionAuthorizationHandler:AuthorizationHandler<IntelligencePermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,IntelligencePermissionRequirement requirement)
    {
        if(context.User.Identity?.AuthenticationType==DevelopmentAuthenticationHandler.SchemeName||context.User.HasClaim("permission",requirement.Permission)||context.User.HasClaim("permission","NAV_ALL")||context.User.IsInRole("SYSTEM_ADMIN")||context.User.IsInRole("TENANT_ADMIN"))context.Succeed(requirement);return Task.CompletedTask;
    }
}
