using Ams.Application.Features.Locations;

namespace Ams.Application.Abstractions.Services;

public interface IAddressLocationService
{
    Task<AddressEngineStatusDto> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AddressSuggestionDto>> AutocompleteAsync(AddressAutocompleteRequest request, CancellationToken cancellationToken = default);
    Task<AddressSuggestionDto?> GeocodeAsync(AddressGeocodeRequest request, CancellationToken cancellationToken = default);
    Task<AddressProviderConfigurationDto?> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateConfigurationAsync(UpdateAddressProviderConfigurationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> PersistResolutionAsync(PersistAddressResolutionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoStateDto>> GetStatesAsync(string? countryCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoCityDto>> SearchCitiesAsync(string? countryCode, string query, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoPostalCodeDto>> GetPostalCodesAsync(string? countryCode, string stateCode, string cityName, CancellationToken cancellationToken = default);
}
