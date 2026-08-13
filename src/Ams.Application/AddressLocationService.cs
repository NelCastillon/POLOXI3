using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Locations;
using Ams.Application.Services;

namespace Ams.Application;

public sealed class AddressLocationService : IAddressLocationService
{
    private readonly IAddressLocationRepository _repository;
    private readonly IReadOnlyDictionary<string, IAddressProvider> _providers;

    public AddressLocationService(IAddressLocationRepository repository, IEnumerable<IAddressProvider> providers)
    {
        _repository = repository;
        _providers = providers.ToDictionary(provider => provider.ProviderCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AddressEngineStatusDto> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        var configuration = await _repository.GetDefaultProviderAsync(tenantId, cancellationToken);
        if (configuration is null)
            return new AddressEngineStatusDto { Message = "No address provider is configured for this tenant." };

        var providerExists = _providers.ContainsKey(configuration.ProviderCode);
        var available = configuration.IsReady && providerExists;
        return new AddressEngineStatusDto
        {
            IsAvailable = available,
            ProviderCode = configuration.ProviderCode,
            ProviderName = configuration.DisplayName,
            Message = available
                ? null
                : !configuration.IsEnabled
                    ? $"{configuration.DisplayName} address lookup is disabled. Manual address entry is available."
                    : string.IsNullOrWhiteSpace(configuration.MapsClientId)
                        ? $"{configuration.DisplayName} requires its Maps Account Client ID. Manual address entry is available."
                        : $"The {configuration.DisplayName} provider implementation is unavailable. Manual address entry is available.",
            MinimumQueryLength = configuration.MinimumQueryLength,
            DebounceMilliseconds = configuration.DebounceMilliseconds,
            MaximumSuggestions = configuration.MaximumSuggestions,
            DefaultCountrySet = configuration.DefaultCountrySet,
            DefaultLanguage = configuration.DefaultLanguage
        };
    }

    public async Task<IReadOnlyList<AddressSuggestionDto>> AutocompleteAsync(AddressAutocompleteRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var configuration = await RequireAvailableConfigurationAsync(request.TenantId, cancellationToken);
        var query = request.Query.Trim();
        if (query.Length < configuration.MinimumQueryLength) return [];

        var provider = _providers[configuration.ProviderCode];
        var limit = Math.Clamp(request.Limit ?? configuration.MaximumSuggestions, 1, configuration.MaximumSuggestions);
        return await provider.AutocompleteAsync(
            configuration,
            query,
            Normalize(request.CountrySet) ?? configuration.DefaultCountrySet,
            Normalize(request.Language) ?? configuration.DefaultLanguage,
            limit,
            cancellationToken);
    }

    public async Task<AddressSuggestionDto?> GeocodeAsync(AddressGeocodeRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var configuration = await RequireAvailableConfigurationAsync(request.TenantId, cancellationToken);
        return await _providers[configuration.ProviderCode].GeocodeAsync(configuration, request, cancellationToken);
    }

    public Task<AddressProviderConfigurationDto?> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return _repository.GetDefaultProviderAsync(tenantId, cancellationToken);
    }

    public Task UpdateConfigurationAsync(UpdateAddressProviderConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        if (!_providers.ContainsKey(request.ProviderCode))
            throw new InvalidOperationException($"Address provider '{request.ProviderCode}' is not installed.");
        return _repository.UpdateProviderAsync(request, cancellationToken);
    }

    public Task<Guid> PersistResolutionAsync(PersistAddressResolutionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        if (request.EntityId == Guid.Empty) throw new ArgumentException("EntityId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.EntityTypeCode)) throw new ArgumentException("EntityTypeCode is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.AddressFieldCode)) throw new ArgumentException("AddressFieldCode is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Address.FormattedAddress)) throw new ArgumentException("FormattedAddress is required.", nameof(request));
        return _repository.UpsertResolutionAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<GeoStateDto>> GetStatesAsync(string? countryCode, CancellationToken cancellationToken = default) =>
        _repository.GetStatesAsync(NormalizeCountry(countryCode), cancellationToken);

    public async Task<IReadOnlyList<GeoCityDto>> SearchCitiesAsync(string? countryCode, string query, int limit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        var normalizedCountry = NormalizeCountry(countryCode);
        var trimmedQuery = query.Trim();
        var effectiveLimit = Math.Clamp(limit, 1, 20);

        var results = new List<GeoCityDto>(
            await _repository.SearchCitiesAsync(normalizedCountry, trimmedQuery, effectiveLimit, cancellationToken));
        if (results.Count >= effectiveLimit || trimmedQuery.Length < FuzzyMinimumQueryLength) return results;

        var candidates = await _repository.GetCityFuzzyCandidatesAsync(normalizedCountry, trimmedQuery, FuzzyCandidatePoolSize, cancellationToken);
        var seen = results.Select(city => city.GeoCityId).ToHashSet();
        var querySoundex = SearchMatchingAlgorithms.Soundex(trimmedQuery.ToLowerInvariant());

        var ranked = candidates
            .Where(candidate => seen.Add(candidate.GeoCityId))
            .Select(candidate => new
            {
                City = candidate,
                Score = ScoreCity(candidate.CityName, trimmedQuery, querySoundex)
            })
            .Where(match => match.Score >= FuzzyMatchThreshold)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.City.CityName)
            .ThenBy(match => match.City.StateCode)
            .Take(effectiveLimit - results.Count)
            .Select(match => match.City);

        results.AddRange(ranked);
        return results;
    }

    private const int FuzzyCandidatePoolSize = 200;
    private const decimal FuzzyMatchThreshold = 55m;
    private const int FuzzyMinimumQueryLength = 4;

    private static decimal ScoreCity(string cityName, string query, string querySoundex)
    {
        var normalizedCity = cityName.Trim().ToLowerInvariant();
        var normalizedQuery = query.ToLowerInvariant();

        if (normalizedCity.Contains(normalizedQuery, StringComparison.Ordinal))
            return 100m - Math.Min(30m, normalizedCity.Length - normalizedQuery.Length);

        var score = SearchMatchingAlgorithms.EditSimilarity(normalizedCity, normalizedQuery);
        if (SearchMatchingAlgorithms.Soundex(normalizedCity) == querySoundex)
            score = Math.Min(100m, score + 15m);
        return score;
    }

    public Task<IReadOnlyList<GeoPostalCodeDto>> GetPostalCodesAsync(string? countryCode, string stateCode, string cityName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stateCode) || string.IsNullOrWhiteSpace(cityName))
            return Task.FromResult<IReadOnlyList<GeoPostalCodeDto>>([]);
        return _repository.GetPostalCodesAsync(NormalizeCountry(countryCode), stateCode.Trim(), cityName.Trim(), cancellationToken);
    }

    private static string NormalizeCountry(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode) ? "US" : countryCode.Trim().ToUpperInvariant();

    private async Task<AddressProviderConfigurationDto> RequireAvailableConfigurationAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var configuration = await _repository.GetDefaultProviderAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("No address provider is configured for this tenant.");
        if (!configuration.IsEnabled)
            throw new InvalidOperationException($"{configuration.DisplayName} address lookup is disabled.");
        if (string.IsNullOrWhiteSpace(configuration.MapsClientId))
            throw new InvalidOperationException($"{configuration.DisplayName} requires its Maps Account Client ID.");
        if (!_providers.ContainsKey(configuration.ProviderCode))
            throw new InvalidOperationException($"Address provider '{configuration.ProviderCode}' is not installed.");
        return configuration;
    }

    private static void EnsureTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
