using Ams.Application.Features.Locations;

namespace Ams.Application.Abstractions.Persistence;

public interface IAddressLocationRepository
{
    Task<AddressProviderConfigurationDto?> GetDefaultProviderAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateProviderAsync(UpdateAddressProviderConfigurationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertResolutionAsync(PersistAddressResolutionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoStateDto>> GetStatesAsync(string countryCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoCityDto>> SearchCitiesAsync(string countryCode, string query, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoPostalCodeDto>> GetPostalCodesAsync(string countryCode, string stateCode, string cityName, CancellationToken cancellationToken = default);
    Task LearnGeoAsync(string countryCode, string stateCode, string cityName, string? county, string? postalCode, decimal? latitude, decimal? longitude, Guid? userId, CancellationToken cancellationToken = default);
}
