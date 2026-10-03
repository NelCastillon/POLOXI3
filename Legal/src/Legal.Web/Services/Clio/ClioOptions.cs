namespace Legal.Web.Services.Clio;

/// <summary>
/// Binds the Clio Manage OAuth app configuration from the "Clio" configuration
/// section. ClientId / ClientSecret must be supplied via User Secrets or
/// environment variables (never committed to appsettings.json or source control).
/// </summary>
public sealed class ClioOptions
{
    public const string SectionName = "Clio";

    /// <summary>Clio Manage OAuth application client id.</summary>
    public string? ClientId { get; set; }

    /// <summary>Clio Manage OAuth application client secret (server-side only).</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Must exactly match a Redirect URI registered on the Clio app.</summary>
    public string RedirectUri { get; set; } = "https://localhost:7250/integrations/clio/callback";

    /// <summary>Clio authorization endpoint.</summary>
    public string AuthorizationEndpoint { get; set; } = "https://app.clio.com/oauth/authorize";

    /// <summary>Clio token endpoint used for the authorization-code exchange.</summary>
    public string TokenEndpoint { get; set; } = "https://app.clio.com/oauth/token";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
