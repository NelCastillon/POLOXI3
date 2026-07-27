namespace Ams.Application.Abstractions.Services;

public interface IPhoneScreeningProvider
{
    string ProviderCode { get; }
    Task<PhoneScreeningProviderResult> ScreenAsync(Guid tenantId, string normalizedPhoneNumber, CancellationToken cancellationToken = default);
}

public sealed class PhoneScreeningProviderResult
{
    public string RegistryCode { get; init; } = string.Empty;
    public string? JurisdictionCode { get; init; }
    public string ResultCode { get; init; } = string.Empty;
    public DateTime ScreenedDateUtc { get; init; }
    public DateTime? ValidThroughDateUtc { get; init; }
    public string? ProviderReference { get; init; }
    public string? RawResponseHash { get; init; }
    public string? ErrorDetails { get; init; }
}
