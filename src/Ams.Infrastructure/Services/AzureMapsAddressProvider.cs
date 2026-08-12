using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Locations;
using Azure.Core;

namespace Ams.Infrastructure.Services;

public sealed class AzureMapsAddressProvider : IAddressProvider
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, AccessToken> TokenCache = new();
    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;

    public AzureMapsAddressProvider(HttpClient httpClient, TokenCredential credential)
    {
        _httpClient = httpClient;
        _credential = credential;
    }

    public string ProviderCode => "AzureMaps";

    public async Task<IReadOnlyList<AddressSuggestionDto>> AutocompleteAsync(
        AddressProviderConfigurationDto configuration,
        string query,
        string? countrySet,
        string? language,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var isSearchApi = configuration.AutocompletePath.Contains("/search/", StringComparison.OrdinalIgnoreCase);
        var uri = BuildUri(
            configuration,
            configuration.AutocompletePath,
            ("query", query),
            (isSearchApi ? "limit" : "top", limit.ToString(CultureInfo.InvariantCulture)),
            ("countrySet", countrySet),
            ("language", language));

        using var response = await SendAsync(configuration, uri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseFeatures(document.RootElement, configuration.MaximumSuggestions);
    }

    public async Task<AddressSuggestionDto?> GeocodeAsync(
        AddressProviderConfigurationDto configuration,
        AddressGeocodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = string.Join(", ", new[]
        {
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateCode,
            request.PostalCode,
            request.CountryCode
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var uri = BuildUri(
            configuration,
            configuration.GeocodePath,
            ("query", query),
            ("top", "1"),
            ("countrySet", request.CountryCode ?? configuration.DefaultCountrySet),
            ("language", configuration.DefaultLanguage));

        using var response = await SendAsync(configuration, uri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseFeatures(document.RootElement, 1).FirstOrDefault();
    }

    internal static IReadOnlyList<AddressSuggestionDto> ParseFeatures(JsonElement root, int limit)
    {
        if (!TryGetArray(root, out var items)) return [];

        var suggestions = new List<AddressSuggestionDto>();
        foreach (var item in items.EnumerateArray())
        {
            if (suggestions.Count >= limit) break;
            var suggestion = ParseFeature(item);
            if (suggestion is not null) suggestions.Add(suggestion);
        }

        return suggestions;
    }

    private async Task<AccessToken> GetCachedTokenAsync(string scope, CancellationToken cancellationToken)
    {
        if (TokenCache.TryGetValue(scope, out var cached)
            && cached.ExpiresOn - TokenRefreshMargin > DateTimeOffset.UtcNow)
        {
            return cached;
        }

        var token = await _credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken);
        TokenCache[scope] = token;
        return token;
    }

    private async Task<HttpResponseMessage> SendAsync(
        AddressProviderConfigurationDto configuration,
        Uri uri,
        CancellationToken cancellationToken)
    {
        AccessToken token;
        try
        {
            token = await GetCachedTokenAsync(configuration.AuthenticationScope, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AddressProviderUnavailableException(
                "Azure Maps authentication is unavailable. Sign in with an Azure identity that has Azure Maps data access.",
                ex);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));
        request.Headers.Add("x-ms-client-id", configuration.MapsClientId);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
        try
        {
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AddressProviderUnavailableException("Azure Maps did not respond before the configured timeout.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AddressProviderUnavailableException("Azure Maps could not be reached.", ex);
        }
    }

    private static Uri BuildUri(
        AddressProviderConfigurationDto configuration,
        string path,
        params (string Name, string? Value)[] values)
    {
        var query = new List<string>();
        var basePath = path.TrimStart('/');
        var separatorIndex = basePath.IndexOf('?');
        if (separatorIndex >= 0)
        {
            var embedded = basePath[(separatorIndex + 1)..];
            basePath = basePath[..separatorIndex];
            if (!string.IsNullOrWhiteSpace(embedded)) query.Add(embedded);
        }

        if (!query.Any(part => part.Contains("api-version=", StringComparison.OrdinalIgnoreCase)))
        {
            query.Add($"api-version={Uri.EscapeDataString(configuration.ApiVersion)}");
        }

        query.AddRange(values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Name)}={Uri.EscapeDataString(item.Value!)}"));

        return new Uri($"{configuration.ServiceEndpoint.TrimEnd('/')}/{basePath}?{string.Join('&', query)}");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 500) detail = detail[..500];
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "Azure Maps rejected the local Azure identity. Sign in again and verify the Maps Account Client ID.",
            System.Net.HttpStatusCode.Forbidden =>
                "Azure Maps denied address search access. Assign the Azure Maps Search and Render Data Reader role to the application identity.",
            System.Net.HttpStatusCode.TooManyRequests =>
                "Azure Maps address search is temporarily rate limited. Try again shortly.",
            _ => $"Azure Maps address search is unavailable (HTTP {(int)response.StatusCode})."
        };
        throw new AddressProviderUnavailableException(
            string.IsNullOrWhiteSpace(detail) ? message : $"{message} Provider response: {detail}");
    }

    private static bool TryGetArray(JsonElement root, out JsonElement items)
    {
        if (root.TryGetProperty("features", out items) && items.ValueKind == JsonValueKind.Array) return true;
        if (root.TryGetProperty("results", out items) && items.ValueKind == JsonValueKind.Array) return true;
        items = default;
        return false;
    }

    private static AddressSuggestionDto? ParseFeature(JsonElement feature)
    {
        var properties = GetObject(feature, "properties") ?? feature;
        var address = GetObject(properties, "address") ?? properties;
        var formatted = FirstString(address, "formattedAddress", "freeformAddress", "label")
            ?? FirstString(properties, "formattedAddress", "label", "name");
        if (string.IsNullOrWhiteSpace(formatted)) return null;

        var coordinates = GetObject(feature, "geometry") is { } geometry
            && geometry.TryGetProperty("coordinates", out var coordinateArray)
            && coordinateArray.ValueKind == JsonValueKind.Array
            ? coordinateArray
            : default;

        decimal? longitude = null;
        decimal? latitude = null;
        if (coordinates.ValueKind == JsonValueKind.Array && coordinates.GetArrayLength() >= 2)
        {
            if (coordinates[0].TryGetDecimal(out var parsedLongitude)) longitude = parsedLongitude;
            if (coordinates[1].TryGetDecimal(out var parsedLatitude)) latitude = parsedLatitude;
        }
        else if (GetObject(feature, "position") is { } position)
        {
            if (position.TryGetProperty("lon", out var lon) && lon.TryGetDecimal(out var parsedLon)) longitude = parsedLon;
            if (position.TryGetProperty("lat", out var lat) && lat.TryGetDecimal(out var parsedLat)) latitude = parsedLat;
        }

        var addressLine = FirstString(address, "addressLine", "streetAddress");
        if (string.IsNullOrWhiteSpace(addressLine))
        {
            var streetNumber = FirstString(address, "streetNumber", "houseNumber");
            var streetName = FirstString(address, "streetName", "street");
            addressLine = string.Join(' ', new[] { streetNumber, streetName }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(addressLine)) addressLine = null;
        }

        return new AddressSuggestionDto
        {
            ProviderCode = "AzureMaps",
            ProviderPlaceId = FirstString(feature, "id") ?? FirstString(properties, "id"),
            FormattedAddress = formatted,
            AddressLine1 = addressLine,
            City = FirstString(address, "locality", "municipality", "city", "municipalitySubdivision"),
            StateCode = FirstAdminDistrict(address, 0) ?? FirstString(address, "countrySubdivision", "state", "adminDistrict"),
            PostalCode = FirstString(address, "postalCode", "extendedPostalCode"),
            CountryCode = CountryCode(address),
            County = FirstAdminDistrict(address, 1) ?? FirstString(address, "countrySecondarySubdivision", "county"),
            Latitude = latitude,
            Longitude = longitude,
            ConfidenceCode = FirstString(properties, "confidence", "matchConfidence")
        };
    }

    private static JsonElement? GetObject(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static string? FirstString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString();
        }
        return null;
    }

    private static string? FirstAdminDistrict(JsonElement address, int index)
    {
        if (!address.TryGetProperty("adminDistricts", out var districts)
            || districts.ValueKind != JsonValueKind.Array
            || districts.GetArrayLength() <= index)
            return null;
        return FirstString(districts[index], "shortName", "name");
    }

    private static string? CountryCode(JsonElement address)
    {
        if (GetObject(address, "countryRegion") is { } region)
            return FirstString(region, "ISO", "iso", "code", "name");
        return FirstString(address, "countryCode", "countryRegionIso2", "country");
    }
}
