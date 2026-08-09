using Ams.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Ams.Api.Tests;

public sealed class PolicyBindControllerSecurityTests
{
    [Fact]
    public void Bind_RequiresAuthorization()
    {
        var method = typeof(PolicyBindController).GetMethod(nameof(PolicyBindController.Bind));

        Assert.NotNull(method);
        Assert.NotEmpty(method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
    }
}
