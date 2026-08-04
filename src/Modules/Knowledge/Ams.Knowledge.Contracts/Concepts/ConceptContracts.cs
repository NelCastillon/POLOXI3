namespace Ams.Knowledge.Contracts.Concepts;

public sealed record ConceptReferenceDto(
    Guid ConceptId,
    Guid ConceptSchemeId,
    string ConceptCode,
    string ConceptTypeCode,
    string PreferredLabel,
    int VersionNumber,
    Guid? TenantId);

public sealed record ConceptResolutionRequest(
    string Input,
    string? ConceptSchemeCode,
    Guid? CarrierId,
    Guid? CarrierProductId,
    string? StateCode,
    Guid? LineOfBusinessConceptId,
    Guid TenantId);

public sealed record ConceptCandidate(
    Guid ConceptId,
    string ConceptCode,
    string PreferredLabel,
    int VersionNumber,
    decimal Confidence,
    string MatchReasonCode);

public sealed record ConceptResolutionResult(
    bool Resolved,
    ConceptCandidate? Selected,
    IReadOnlyCollection<ConceptCandidate> Candidates,
    bool RequiresReview);

public interface IConceptResolver
{
    Task<ConceptResolutionResult> ResolveAsync(ConceptResolutionRequest request, CancellationToken cancellationToken = default);
}
