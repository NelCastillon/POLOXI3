namespace Legal.Web.Services.Clio;

/// <summary>
/// The result of a successful Clio authorization-code token exchange.
/// </summary>
public sealed record ClioToken(
    string AccessToken,
    string? RefreshToken,
    string TokenType,
    DateTimeOffset? ExpiresAtUtc)
{
    public bool IsExpired => ExpiresAtUtc is { } expiry && expiry <= DateTimeOffset.UtcNow;
}

/// <summary>
/// Demo-only, in-memory Clio token store. A single process-wide token is kept
/// for the hackathon demo; it is intentionally not persisted and is lost on app
/// restart. Swap for an encrypted per-tenant store before any real deployment.
/// </summary>
public sealed class ClioTokenStore
{
    private readonly object _gate = new();
    private ClioToken? _token;

    public void Set(ClioToken token)
    {
        lock (_gate)
        {
            _token = token;
        }
    }

    public ClioToken? Get()
    {
        lock (_gate)
        {
            return _token;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _token = null;
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (_gate)
            {
                return _token is { } token && !token.IsExpired;
            }
        }
    }
}
