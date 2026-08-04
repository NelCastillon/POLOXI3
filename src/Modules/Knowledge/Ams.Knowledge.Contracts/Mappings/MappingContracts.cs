namespace Ams.Knowledge.Contracts.Mappings;

public sealed record ExternalMappingRequest(
    Guid TenantId,
    string SourceSystemTypeCode,
    Guid? SourceSystemId,
    string? ExternalCode,
    string ExternalValue,
    string? ExternalPath,
    Guid? CarrierProductId,
    string? StateCode,
    Guid? LineOfBusinessConceptId,
    DateTime EffectiveUtc);

public sealed record ExternalMappingResult(
    Guid ExternalConceptMappingId,
    Guid ConceptId,
    string ConceptCode,
    string PreferredLabel,
    int ConceptVersionNumber,
    decimal Confidence,
    string MatchTypeCode,
    bool IsApproved);

public interface IExternalMappingService
{
    Task<ExternalMappingResult?> ResolveMappingAsync(ExternalMappingRequest request, CancellationToken cancellationToken = default);
}
