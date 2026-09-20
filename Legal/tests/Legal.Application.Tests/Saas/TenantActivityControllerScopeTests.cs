using System.Reflection;
using System.Security.Claims;
using Legal.Api.Controllers;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Legal.Application.Tests.Saas;

public sealed class TenantActivityControllerScopeTests
{
    [Theory]
    [InlineData("SYSTEM_ADMIN")]
    [InlineData("SUPERADMIN")]
    public async Task SystemAdmin_CanReadSelectedMemberTenant(string role)
    {
        var currentTenant = Guid.NewGuid();
        var selectedTenant = Guid.NewGuid();
        var (controller, handler) = CreateController(currentTenant, role, ["Members.Manage"]);

        await controller.GetUserUsage(Guid.NewGuid(), selectedTenant, 30);

        Assert.Equal(selectedTenant, handler.LastArguments![0]);
    }

    [Fact]
    public async Task PlatformPermission_CanReadSelectedMemberTenant()
    {
        var currentTenant = Guid.NewGuid();
        var selectedTenant = Guid.NewGuid();
        var (controller, handler) = CreateController(currentTenant, "MEMBER", ["Members.Manage", "platform.users.manage"]);

        await controller.GetUserUsage(Guid.NewGuid(), selectedTenant, 30);

        Assert.Equal(selectedTenant, handler.LastArguments![0]);
    }

    [Fact]
    public async Task TenantAdmin_CannotOverrideAuthenticatedTenant()
    {
        var currentTenant = Guid.NewGuid();
        var selectedTenant = Guid.NewGuid();
        var (controller, handler) = CreateController(currentTenant, "TENANT_ADMIN", ["Members.Manage"]);

        await controller.GetUserUsage(Guid.NewGuid(), selectedTenant, 30);

        Assert.Equal(currentTenant, handler.LastArguments![0]);
    }

    private static (TenantActivityController Controller, ActivityServiceHandler Handler) CreateController(Guid tenantId, string role, string[] permissions)
    {
        var service = DispatchProxy.Create<IActivityService, ActivityServiceHandler>();
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var controller = new TenantActivityController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role))
                }
            }
        };
        return (controller, (ActivityServiceHandler)(object)service);
    }

    public class ActivityServiceHandler : DispatchProxy
    {
        public object?[]? LastArguments { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            LastArguments = args;
            return targetMethod?.Name == nameof(IActivityService.SummarizeUsageForUserAsync)
                ? Task.FromResult<IReadOnlyList<UsageSummaryDto>>([])
                : throw new NotSupportedException(targetMethod?.Name);
        }
    }
}