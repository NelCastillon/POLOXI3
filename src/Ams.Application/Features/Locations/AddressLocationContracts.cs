using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Locations;

public sealed class AddressProviderConfigurationDto
{
    public Guid AddressProviderConfigurationId { get; set; }
    public Guid TenantId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ServiceEndpoint { get; set; } = string.Empty;
    public string AutocompletePath { get; set; } = string.Empty;
    public string GeocodePath { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string AuthenticationScope { get; set; } = string.Empty;
    public string? MapsClientId { get; set; }
    public string? DefaultCountrySet { get; set; }
    public string? DefaultLanguage { get; set; }
    public int MinimumQueryLength { get; set; }
    public int DebounceMilliseconds { get; set; }
    public int MaximumSuggestions { get; set; }
    public int RequestTimeoutSeconds { get; set; }
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsReady => IsEnabled && !string.IsNullOrWhiteSpace(MapsClientId);
}

public sealed class AddressEngineStatusDto
{
    public bool IsAvailable { get; set; }
    public string? ProviderCode { get; set; }
    public string? ProviderName { get; set; }
    public string? Message { get; set; }
    public int MinimumQueryLength { get; set; }
    public int DebounceMilliseconds { get; set; }
    public int MaximumSuggestions { get; set; }
    public string? DefaultCountrySet { get; set; }
    public string? DefaultLanguage { get; set; }
}

public sealed class GeoStateDto
{
    public string CountryCode { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
}

public sealed class GeoCityDto
{
    public Guid GeoCityId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string? County { get; set; }
}

public sealed class GeoPostalCodeDto
{
    public string PostalCode { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public sealed class AddressAutocompleteRequest
{
    public Guid TenantId { get; set; }
    [Required, StringLength(300, MinimumLength = 2)]
    public string Query { get; set; } = string.Empty;
    [StringLength(200)]
    public string? CountrySet { get; set; }
    [StringLength(20)]
    public string? Language { get; set; }
    [Range(1, 20)]
    public int? Limit { get; set; }
}

public sealed class AddressGeocodeRequest
{
    public Guid TenantId { get; set; }
    [Required, StringLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;
    [StringLength(200)]
    public string? AddressLine2 { get; set; }
    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(50)]
    public string? StateCode { get; set; }
    [StringLength(20)]
    public string? PostalCode { get; set; }
    [StringLength(10)]
    public string? CountryCode { get; set; }
}

public sealed class AddressSuggestionDto
{
    public string ProviderCode { get; set; } = string.Empty;
    public string? ProviderPlaceId { get; set; }
    public string FormattedAddress { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateCode { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
    public string? County { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ConfidenceCode { get; set; }
}

public sealed class AddressResolutionInput
{
    [StringLength(50)]
    public string? ProviderCode { get; set; }
    [StringLength(200)]
    public string? ProviderPlaceId { get; set; }
    [StringLength(300)]
    public string? QueryText { get; set; }
    [Required, StringLength(500)]
    public string FormattedAddress { get; set; } = string.Empty;
    [StringLength(200)]
    public string? AddressLine1 { get; set; }
    [StringLength(200)]
    public string? AddressLine2 { get; set; }
    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(50)]
    public string? StateCode { get; set; }
    [StringLength(20)]
    public string? PostalCode { get; set; }
    [StringLength(10)]
    public string? CountryCode { get; set; }
    [StringLength(100)]
    public string? County { get; set; }
    [Range(-90, 90)]
    public decimal? Latitude { get; set; }
    [Range(-180, 180)]
    public decimal? Longitude { get; set; }
    [StringLength(50)]
    public string? ConfidenceCode { get; set; }
    public bool IsProviderValidated { get; set; }
}

public sealed class UpdateAddressProviderConfigurationRequest
{
    public Guid TenantId { get; set; }
    [Required, StringLength(50)]
    public string ProviderCode { get; set; } = string.Empty;
    [StringLength(100)]
    public string? MapsClientId { get; set; }
    [StringLength(200)]
    public string? DefaultCountrySet { get; set; }
    [StringLength(20)]
    public string? DefaultLanguage { get; set; }
    [Range(2, 20)]
    public int MinimumQueryLength { get; set; }
    [Range(100, 5000)]
    public int DebounceMilliseconds { get; set; }
    [Range(1, 20)]
    public int MaximumSuggestions { get; set; }
    [Range(1, 120)]
    public int RequestTimeoutSeconds { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class PersistAddressResolutionRequest
{
    public Guid TenantId { get; set; }
    [Required, StringLength(100)]
    public string EntityTypeCode { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    [Required, StringLength(100)]
    public string AddressFieldCode { get; set; } = string.Empty;
    [Required]
    public AddressResolutionInput Address { get; set; } = new();
    public Guid? UserId { get; set; }
}
