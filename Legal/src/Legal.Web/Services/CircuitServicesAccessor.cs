using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Legal.Web.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Exposes the active Blazor circuit's service provider to code that runs outside
// component scope (e.g. HttpClient DelegatingHandlers). HttpClientFactory builds
// message handlers in their own DI scope, so a NavigationManager resolved there
// is an uninitialized RemoteNavigationManager. Capturing the circuit's service
// provider via an AsyncLocal — set on every inbound circuit activity — lets the
// handler resolve the real, initialized circuit NavigationManager instead.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> _current = new();

    public IServiceProvider? Services
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

// Publishes the circuit's IServiceProvider into the AsyncLocal for the duration
// of each inbound activity (event handlers, lifecycle callbacks, etc.), so async
// flows started there — including ApiClient HTTP calls — can reach it.
public sealed class CircuitServicesAccessorHandler(IServiceProvider services, CircuitServicesAccessor accessor) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
        => async context =>
        {
            accessor.Services = services;
            try
            {
                await next(context);
            }
            finally
            {
                accessor.Services = null;
            }
        };
}
