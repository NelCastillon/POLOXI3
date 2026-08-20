using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Locations;
using Ams.Infrastructure.Services;
using System.Text.Json;
using Xunit;

namespace Ams.Application.Tests;

public sealed class AddressLocationServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetStatusAsync_ReturnsManualEntryFallback_WhenClientIdIsMissing()
    {
        var repository = new FakeRepository { Configuration = Configuration(mapsClientId: null) };
        var service = new AddressLocationService(repository, [new FakeProvider()]);

        var status = await service.GetStatusAsync(TenantId);

        Assert.False(status.IsAvailable);
        Assert.Contains("Manual address entry", status.Message);
        Assert.Equal(repository.Configuration.MinimumQueryLength, status.MinimumQueryLength);
        Assert.Equal(repository.Configuration.DebounceMilliseconds, status.DebounceMilliseconds);
    }

    [Fact]
    public async Task AutocompleteAsync_UsesDatabaseDefaultsAndCapsLimit()
    {
        var provider = new FakeProvider();
        var configuration = Configuration("maps-client-id");
        var service = new AddressLocationService(new FakeRepository { Configuration = configuration }, [provider]);

        await service.AutocompleteAsync(new AddressAutocompleteRequest
        {
            TenantId = TenantId,
            Query = " 3857 West Main ",
            Limit = 20
        });

        Assert.Equal("3857 West Main", provider.LastQuery);
        Assert.Equal(configuration.DefaultCountrySet, provider.LastCountrySet);
        Assert.Equal(configuration.DefaultLanguage, provider.LastLanguage);
        Assert.Equal(configuration.MaximumSuggestions, provider.LastLimit);
    }

    [Fact]
    public async Task AutocompleteAsync_DoesNotCallProvider_BelowConfiguredMinimum()
    {
        var provider = new FakeProvider();
        var service = new AddressLocationService(new FakeRepository { Configuration = Configuration("maps-client-id") }, [provider]);

        var results = await service.AutocompleteAsync(new AddressAutocompleteRequest
        {
            TenantId = TenantId,
            Query = "38"
        });

        Assert.Empty(results);
        Assert.Null(provider.LastQuery);
    }

    [Fact]
    public async Task PersistResolutionAsync_PassesTenantScopedResolutionToRepository()
    {
        var repository = new FakeRepository { Configuration = Configuration("maps-client-id") };
        var service = new AddressLocationService(repository, [new FakeProvider()]);
        var request = new PersistAddressResolutionRequest
        {
            TenantId = TenantId,
            EntityTypeCode = "Account",
            EntityId = Guid.NewGuid(),
            AddressFieldCode = "Primary",
            Address = new AddressResolutionInput
            {
                FormattedAddress = "3857 West Main Street",
                IsProviderValidated = false
            }
        };

        var id = await service.PersistResolutionAsync(request);

        Assert.Equal(repository.ResolutionId, id);
        Assert.Same(request, repository.LastResolution);
    }

    [Fact]
    public void AzureMapsParser_MapsGeoJsonAddressAndCoordinates()
    {
        using var document = JsonDocument.Parse("""
            {
              "features": [
                {
                  "id": "address.123",
                  "geometry": { "coordinates": [-87.6298, 41.8781] },
                  "properties": {
                    "confidence": "High",
                    "address": {
                      "formattedAddress": "3857 West Main Street, Chicago, IL 60601",
                      "addressLine": "3857 West Main Street",
                      "locality": "Chicago",
                      "adminDistricts": [
                        { "shortName": "IL" },
                        { "name": "Cook County" }
                      ],
                      "postalCode": "60601",
                      "countryRegion": { "ISO": "US" }
                    }
                  }
                }
              ]
            }
            """);

        var result = Assert.Single(AzureMapsAddressProvider.ParseFeatures(document.RootElement, 8));

        Assert.Equal("address.123", result.ProviderPlaceId);
        Assert.Equal("3857 West Main Street", result.AddressLine1);
        Assert.Equal("Chicago", result.City);
        Assert.Equal("IL", result.StateCode);
        Assert.Equal("Cook County", result.County);
        Assert.Equal("US", result.CountryCode);
        Assert.Equal(41.8781m, result.Latitude);
        Assert.Equal(-87.6298m, result.Longitude);
        Assert.Equal("High", result.ConfidenceCode);
    }

    private static AddressProviderConfigurationDto Configuration(string? mapsClientId) => new()
    {
        AddressProviderConfigurationId = Guid.NewGuid(),
        TenantId = TenantId,
        ProviderCode = "AzureMaps",
        DisplayName = "Azure Maps",
        ServiceEndpoint = "https://atlas.microsoft.com",
        AutocompletePath = "/geocode:autocomplete",
        GeocodePath = "/geocode",
        ApiVersion = "2025-01-01",
        AuthenticationScope = "https://atlas.microsoft.com/.default",
        MapsClientId = mapsClientId,
        DefaultCountrySet = "US",
        DefaultLanguage = "en-US",
        MinimumQueryLength = 3,
        DebounceMilliseconds = 300,
        MaximumSuggestions = 8,
        RequestTimeoutSeconds = 15,
        IsDefault = true,
        IsEnabled = true
    };

    private sealed class FakeRepository : IAddressLocationRepository
    {
        public AddressProviderConfigurationDto? Configuration { get; set; }
        public Guid ResolutionId { get; } = Guid.NewGuid();
        public PersistAddressResolutionRequest? LastResolution { get; private set; }

        public Task<AddressProviderConfigurationDto?> GetDefaultProviderAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(Configuration);

        public Task UpdateProviderAsync(UpdateAddressProviderConfigurationRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid> UpsertResolutionAsync(PersistAddressResolutionRequest request, CancellationToken cancellationToken = default)
        {
            LastResolution = request;
            return Task.FromResult(ResolutionId);
        }

        public Task<IReadOnlyList<GeoStateDto>> GetStatesAsync(string countryCode, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GeoStateDto>>([]);

        public Task<IReadOnlyList<GeoCityDto>> SearchCitiesAsync(string countryCode, string query, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GeoCityDto>>([]);

        public Task<IReadOnlyList<GeoCityDto>> GetCityFuzzyCandidatesAsync(string countryCode, string query, int maxCandidates, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GeoCityDto>>([]);

        public Task<IReadOnlyList<GeoPostalCodeDto>> GetPostalCodesAsync(string countryCode, string stateCode, string cityName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GeoPostalCodeDto>>([]);

        public Task LearnGeoAsync(string countryCode, string stateCode, string cityName, string? county, string? postalCode, decimal? latitude, decimal? longitude, Guid? userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProvider : IAddressProvider
    {
        public string ProviderCode => "AzureMaps";
        public string? LastQuery { get; private set; }
        public string? LastCountrySet { get; private set; }
        public string? LastLanguage { get; private set; }
        public int LastLimit { get; private set; }

        public Task<IReadOnlyList<AddressSuggestionDto>> AutocompleteAsync(
            AddressProviderConfigurationDto configuration,
            string query,
            string? countrySet,
            string? language,
            int limit,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            LastCountrySet = countrySet;
            LastLanguage = language;
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<AddressSuggestionDto>>([]);
        }

        public Task<AddressSuggestionDto?> GeocodeAsync(
            AddressProviderConfigurationDto configuration,
            AddressGeocodeRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<AddressSuggestionDto?>(null);
    }
}
