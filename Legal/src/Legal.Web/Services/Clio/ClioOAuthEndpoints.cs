using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Legal.Web.Services.Clio;

/// <summary>
/// Maps the server-side Clio Manage OAuth 2.0 authorization-code endpoints:
///   GET /integrations/clio/connect  -> redirects the browser to Clio's consent screen
///   GET /integrations/clio/callback -> exchanges the returned code for an access token
/// The client secret is never exposed to the browser; the token exchange runs
/// entirely on the server.
/// </summary>
public static class ClioOAuthEndpoints
{
    private const string StateCookieName = "judz.clio.oauth.state";

    public static IEndpointRouteBuilder MapClioOAuth(this IEndpointRouteBuilder app)
    {
        // ── Step 1: send the browser to Clio's authorization endpoint ──────────
        app.MapGet("/integrations/clio/connect", (HttpContext http, IOptions<ClioOptions> options) =>
        {
            var clio = options.Value;
            if (!clio.IsConfigured)
                return Results.BadRequest(
                    "Clio is not configured. Set Clio:ClientId and Clio:ClientSecret via User Secrets or environment variables.");

            // Cryptographically random, single-use anti-forgery value.
            var state = Base64Url(RandomNumberGenerator.GetBytes(32));
            http.Response.Cookies.Append(StateCookieName, state, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/integrations/clio"
            });

            var authUrl = QueryHelpers_AddQuery(clio.AuthorizationEndpoint, new Dictionary<string, string?>
            {
                ["response_type"] = "code",
                ["client_id"] = clio.ClientId,
                ["redirect_uri"] = clio.RedirectUri,
                ["state"] = state
            });

            return Results.Redirect(authUrl);
        });

        // ── Step 2: handle Clio's redirect back and exchange the code ──────────
        app.MapGet("/integrations/clio/callback", async (
            HttpContext http,
            IOptions<ClioOptions> options,
            ClioTokenStore tokenStore,
            IHttpClientFactory httpClientFactory,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ClioOAuth");
            var clio = options.Value;

            var error = http.Request.Query["error"].ToString();
            if (!string.IsNullOrEmpty(error))
            {
                logger.LogWarning("Clio authorization denied: {Error}", error);
                return Results.Redirect("/integrations/clio?error=" + Uri.EscapeDataString(error));
            }

            var code = http.Request.Query["code"].ToString();
            var returnedState = http.Request.Query["state"].ToString();
            var expectedState = http.Request.Cookies[StateCookieName];
            http.Response.Cookies.Delete(StateCookieName, new CookieOptions { Path = "/integrations/clio" });

            if (string.IsNullOrEmpty(code))
                return Results.Redirect("/integrations/clio?error=missing_code");

            if (string.IsNullOrEmpty(expectedState) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(returnedState),
                    System.Text.Encoding.UTF8.GetBytes(expectedState)))
            {
                logger.LogWarning("Clio callback state mismatch.");
                return Results.Redirect("/integrations/clio?error=state_mismatch");
            }

            var client = httpClientFactory.CreateClient("Clio");
            using var request = new HttpRequestMessage(HttpMethod.Post, clio.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = clio.RedirectUri,
                    ["client_id"] = clio.ClientId!,
                    ["client_secret"] = clio.ClientSecret!
                })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Clio token exchange failed ({Status}): {Payload}", (int)response.StatusCode, payload);
                return Results.Redirect("/integrations/clio?error=token_exchange_failed");
            }

            var token = ParseToken(payload);
            if (token is null)
                return Results.Redirect("/integrations/clio?error=invalid_token_response");

            tokenStore.Set(token);
            logger.LogInformation("Clio connected; access token acquired (expires {Expiry}).", token.ExpiresAtUtc);
            return Results.Redirect("/integrations/clio?connected=1");
        });

        // ── Disconnect: drop the in-memory token ───────────────────────────────
        app.MapPost("/integrations/clio/disconnect", (ClioTokenStore tokenStore) =>
        {
            tokenStore.Clear();
            return Results.Redirect("/integrations/clio");
        });

        return app;
    }

    private static ClioToken? ParseToken(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("access_token", out var accessTokenEl))
            return null;

        var accessToken = accessTokenEl.GetString();
        if (string.IsNullOrEmpty(accessToken))
            return null;

        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "bearer" : "bearer";

        DateTimeOffset? expiresAt = null;
        if (root.TryGetProperty("expires_in", out var ei) && ei.TryGetInt64(out var seconds))
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(seconds);

        return new ClioToken(accessToken, refreshToken, tokenType, expiresAt);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // Small local helper to avoid pulling in Microsoft.AspNetCore.WebUtilities explicitly.
    private static string QueryHelpers_AddQuery(string uri, IDictionary<string, string?> parameters)
    {
        var query = string.Join('&', parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
        var separator = uri.Contains('?') ? "&" : "?";
        return string.IsNullOrEmpty(query) ? uri : uri + separator + query;
    }
}
