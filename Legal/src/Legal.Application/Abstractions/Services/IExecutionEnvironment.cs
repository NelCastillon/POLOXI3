namespace Legal.Application.Abstractions.Services;

// Minimal host-environment abstraction so the Application layer can enforce environment-based rules
// (e.g. DEV Logic must never run in a Production deployment) without taking a hard dependency on the
// ASP.NET Core hosting types. The API host provides the concrete implementation.
public interface IExecutionEnvironment
{
    // True when the current deployment is a Production environment. When true, DEV Logic execution is
    // rejected server-side even if the request/UI attempts to select it.
    bool IsProduction { get; }

    // The raw environment name (e.g. "Development", "Staging", "Production") for diagnostics.
    string EnvironmentName { get; }
}
