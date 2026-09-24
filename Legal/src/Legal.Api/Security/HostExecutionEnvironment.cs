using Legal.Application.Abstractions.Services;

namespace Legal.Api.Security;

// Host-backed IExecutionEnvironment. Bridges ASP.NET Core's IHostEnvironment to the Application layer
// so DEV Logic execution can be rejected server-side in a Production deployment.
public sealed class HostExecutionEnvironment(IHostEnvironment environment) : IExecutionEnvironment
{
    public bool IsProduction => environment.IsProduction();

    public string EnvironmentName => environment.EnvironmentName;
}
