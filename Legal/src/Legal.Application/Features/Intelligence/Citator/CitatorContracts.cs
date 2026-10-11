namespace Legal.Application.Features.Intelligence.Citator;

// ── Citator read contracts. The citator reports how later authorities have TREATED a cited authority
//    and the resulting good-law validity flag. All values are DB-backed and tenant-scoped; absence of
//    data yields the Unknown flag (advisory, never blocking). No hardcoded operational data. ──

// A DB-backed flag level (validity signal vocabulary).
public sealed record CitationFlagLevelDto(
    string FlagLevelCode,
    string DisplayName,
    string ColorHex,
    int SeverityOrder,
    bool IsDefault);

// A single recorded treatment of a cited authority by a later authority.
public sealed record AuthorityTreatmentDto(
    Guid CitationTreatmentId,
    string CitedAuthorityIdentity,
    string? CitedAuthorityName,
    string? CitingAuthorityName,
    string? CitingAuthorityRef,
    string TreatmentCode,
    string TreatmentDisplayName,
    string PolarityCode,
    string FlagLevelCode,
    DateOnly? TreatmentDate,
    string? Jurisdiction,
    string? SourceProviderCode,
    string? Notes);

// The computed good-law validity of one authority: the worst flag among its recorded treatments,
// with the treatments that produced it. FlagLevelCode is "Unknown" when there is no citator data.
public sealed record AuthorityValidityDto(
    string AuthorityIdentity,
    string FlagLevelCode,
    string FlagDisplayName,
    string FlagColorHex,
    bool HasNegativeTreatment,
    int TreatmentCount,
    IReadOnlyList<AuthorityTreatmentDto> Treatments);

// ── Citator management (write) contracts. Treatments are tenant-scoped and append-only; editing an
//    existing row updates it in place by id, and removal is a soft delete (IsDeleted). The implied
//    validity flag is DB-derived from the chosen treatment code, so callers only pick a code. ──

// A DB-backed treatment-code option (the vocabulary a user picks from when recording a treatment).
public sealed record CitationTreatmentCodeDto(
    string TreatmentCode,
    string DisplayName,
    string? Description,
    string PolarityCode,
    int SeverityRank,
    string FlagLevelCode);

// Create or update one treatment record. CitationTreatmentId null => create; set => update that row.
// FlagLevelCode is derived server-side from TreatmentCode and never trusted from the client.
public sealed record SaveCitationTreatmentRequest(
    Guid? CitationTreatmentId,
    string CitedAuthorityIdentity,
    string? CitedAuthorityName,
    string? CitingAuthorityName,
    string? CitingAuthorityRef,
    string TreatmentCode,
    DateOnly? TreatmentDate,
    string? Jurisdiction,
    string? SourceProviderCode,
    string? Notes);
