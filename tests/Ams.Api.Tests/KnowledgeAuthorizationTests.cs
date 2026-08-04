using System.Security.Claims;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Ams.Api.Tests;

public sealed class KnowledgeAuthorizationTests
{
    [Fact]
    public async Task Handler_AllowsMatchingPermission()
    {
        var requirement = new KnowledgePermissionRequirement(KnowledgePolicies.MappingsApprove);
        var user = Principal(new Claim("permission", KnowledgePolicies.MappingsApprove));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await new KnowledgePermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_DeniesUnrelatedPermission()
    {
        var requirement = new KnowledgePermissionRequirement(KnowledgePolicies.Publish);
        var user = Principal(new Claim("permission", KnowledgePolicies.ConceptsRead));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await new KnowledgePermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [InlineData("SYSTEM_ADMIN")]
    [InlineData("TENANT_ADMIN")]
    public async Task Handler_AllowsAdministrativeRoles(string role)
    {
        var requirement = new KnowledgePermissionRequirement(KnowledgePolicies.AuditRead);
        var context = new AuthorizationHandlerContext([requirement], Principal(new Claim(ClaimTypes.Role, role)), null);

        await new KnowledgePermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Test"));
}
