using Ams.Application.Features.Locations;

namespace Ams.Application.Abstractions.Services;

public sealed class AddressProviderUnavailableException : Exception
{
    public AddressProviderUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public interface IAddressProvider
{
    string ProviderCode { get; }

    Task<IReadOnlyList<AddressSuggestionDto>> AutocompleteAsync(
        AddressProviderConfigurationDto configuration,
        string query,
        string? countrySet,
        string? language,
        int limit,
        CancellationToken cancellationToken = default);

    Task<AddressSuggestionDto?> GeocodeAsync(
        AddressProviderConfigurationDto configuration,
        AddressGeocodeRequest request,
        CancellationToken cancellationToken = default);
}
