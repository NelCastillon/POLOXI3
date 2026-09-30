using System.ComponentModel.DataAnnotations;

namespace Legal.Application.Features.Intelligence.Decision;

// ─── Decision Contract — first-class, versioned Judz problem specification. ─────────────────────────
// POLOXI remains the single authoritative evaluator; these contracts carry only the attorney-defined
// decision boundary. Status transitions happen through governed commands, never a free dropdown.

public static class DecisionContractStatuses
{
    public const string Draft = "DRAFT";
    public const string ReadyForReview = "READY_FOR_REVIEW";
    public const string Approved = "APPROVED";
    public const string Active = "ACTIVE";
    public const string Superseded = "SUPERSEDED";
    public const string Rejected = "REJECTED";
}

public static class DecisionContractOptionGroups
{
    public const string ProceduralPosture = "DC_PROCEDURAL_POSTURE";
    public const string CaseType = "DC_CASE_TYPE";
    public const string LegalFramework = "DC_LEGAL_FRAMEWORK";
    public const string CandidateType = "DC_CANDIDATE_TYPE";
    public const string CandidateState = "DC_CANDIDATE_STATE";
    public const string BurdenParty = "DC_BURDEN_PARTY";
    public const string DecisionStandard = "DC_DECISION_STANDARD";
    public const string SemanticMode = "DC_SEMANTIC_MODE";
    public const string DecisionHorizon = "DC_DECISION_HORIZON";
    public const string Jurisdiction = "JURISDICTION";
}

public static class DecisionContractFactStates
{
    public const string Known = "KNOWN";
    public const string Disputed = "DISPUTED";
    public const string Unknown = "UNKNOWN";
}

public static class DecisionContractTagKinds
{
    public const string StatuteRule = "STATUTE_RULE";
    public const string KeyIssue = "KEY_ISSUE";
}

// ─── Read model / workspace projection ───────────────────────────────────────────────────────────

public sealed record DecisionContractOptionDto(string FieldCode, string Value, string DisplayName, int SortOrder);

// Read-only view of a POLOXI scoring weight (from POLOXI.Legal_DecisionSetting). Displayed for
// transparency only — the Decision Contract never edits POLOXI configuration or scores candidates.
public sealed record PoloxiWeightDto(string SettingKey, string DisplayName, decimal Value, string? Description);

public sealed record DecisionContractCandidateDto(
    Guid DecisionContractCandidateId,
    string CandidateCode,
    string OutcomeText,
    string? CandidateTypeCode,
    string StateCode,
    int DisplayOrder);

public sealed record DecisionContractFactBoundaryDto(
    Guid DecisionContractFactBoundaryId,
    Guid? FactId,
    string FactStateCode,
    string SnapshotText,
    int DisplayOrder);

public sealed record DecisionContractTagDto(
    Guid DecisionContractTagId,
    string TagKindCode,
    string TagText,
    int DisplayOrder);

public sealed record DecisionContractReviewDto(
    Guid DecisionContractReviewId,
    string ReviewActionCode,
    Guid? ReviewerUserId,
    string? ReviewerDisplayName,
    string? Comment,
    DateTime CreatedDateUtc);

public sealed record DecisionContractVersionSummaryDto(
    Guid DecisionContractId,
    int VersionNumber,
    string StatusCode,
    Guid? CreatedByUserId,
    string? CreatedByDisplayName,
    DateTime CreatedDateUtc,
    Guid? ApprovedByUserId,
    string? ApprovedByDisplayName,
    DateTime? ApprovedDateUtc);

public sealed record DecisionContractDto(
    Guid DecisionContractId,
    Guid MatterId,
    int VersionNumber,
    string StatusCode,
    // Decision
    string? DecisionQuestion,
    string? ClientObjective,
    string? SuccessDefinition,
    DateOnly? DecisionDate,
    // Legal context
    string? Jurisdiction,
    string? CourtOrForum,
    string? GoverningLaw,
    string? ProceduralPosture,
    string? CaseType,
    string? ApplicableLegalFramework,
    // Burden & standard
    string? MovingParty,
    string? InitialBurden,
    string? UltimateBurden,
    string? StandardOfProofOrReview,
    string? BurdenNotes,
    // Boundaries
    string? EvidenceBoundary,
    string? AuthorityBoundary,
    string? SourceRestrictions,
    DateOnly? AuthorityCutoffDate,
    // Additional settings
    string? DecisionHorizon,
    bool ExternalResearchPermitted,
    bool ReviewBeforeActivation,
    bool ReviewBeforeFinal,
    string SemanticValidationMode,
    string? Notes,
    // Governance
    Guid? CreatedByUserId,
    string? CreatedByDisplayName,
    DateTime CreatedDateUtc,
    Guid? ApprovedByUserId,
    string? ApprovedByDisplayName,
    DateTime? ApprovedDateUtc,
    byte[] RowVersion,
    IReadOnlyList<DecisionContractCandidateDto> Candidates,
    IReadOnlyList<DecisionContractFactBoundaryDto> FactBoundaries,
    IReadOnlyList<DecisionContractTagDto> Tags);

public sealed record DecisionContractValidationIssueDto(
    string Code,
    string Section,
    string Severity, // ERROR | WARNING | INFO
    string Message,
    string? Field);

public sealed record DecisionContractSectionCompletionDto(
    string SectionCode,
    string Title,
    bool Complete);

public sealed record DecisionContractWorkspaceDto(
    Guid MatterId,
    string? MatterTitle,
    DecisionContractDto Contract,
    int CompletionPercent,
    IReadOnlyList<DecisionContractSectionCompletionDto> Sections,
    IReadOnlyList<DecisionContractValidationIssueDto> Validation,
    IReadOnlyList<DecisionContractVersionSummaryDto> VersionHistory,
    IReadOnlyList<DecisionContractReviewDto> Reviews,
    IReadOnlyList<PoloxiWeightDto> PoloxiWeights,
    bool CanEdit,
    bool CanSubmit,
    bool CanApprove,
    bool CanReturn,
    bool CanActivate,
    bool CanCreateVersion);

// ─── Commands (section updates carry RowVersion for optimistic concurrency) ────────────────────────

public sealed record DecisionContractDecisionCommand(
    Guid DecisionContractId,
    byte[] RowVersion,
    [StringLength(2000)] string? DecisionQuestion,
    [StringLength(1000)] string? ClientObjective,
    [StringLength(1500)] string? SuccessDefinition,
    DateOnly? DecisionDate);

public sealed record DecisionContractLegalContextCommand(
    Guid DecisionContractId,
    byte[] RowVersion,
    [StringLength(300)] string? Jurisdiction,
    [StringLength(500)] string? CourtOrForum,
    [StringLength(1000)] string? GoverningLaw,
    [StringLength(300)] string? ProceduralPosture,
    [StringLength(300)] string? CaseType,
    [StringLength(1000)] string? ApplicableLegalFramework,
    IReadOnlyList<string>? StatutesOrRules,
    IReadOnlyList<string>? KeyIssues);

public sealed record DecisionContractBurdenCommand(
    Guid DecisionContractId,
    byte[] RowVersion,
    [StringLength(300)] string? MovingParty,
    [StringLength(1000)] string? InitialBurden,
    [StringLength(1000)] string? UltimateBurden,
    [StringLength(1000)] string? StandardOfProofOrReview,
    [StringLength(2000)] string? BurdenNotes);

public sealed record DecisionContractBoundariesCommand(
    Guid DecisionContractId,
    byte[] RowVersion,
    [StringLength(2000)] string? EvidenceBoundary,
    [StringLength(2000)] string? AuthorityBoundary,
    [StringLength(2000)] string? SourceRestrictions,
    DateOnly? AuthorityCutoffDate,
    IReadOnlyList<DecisionContractFactBoundaryInput>? Facts);

public sealed record DecisionContractFactBoundaryInput(
    Guid? FactId,
    string FactStateCode,
    string SnapshotText);

public sealed record DecisionContractCandidatesCommand(
    Guid DecisionContractId,
    byte[] RowVersion,
    IReadOnlyList<DecisionContractCandidateInput> Candidates);

public sealed record DecisionContractCandidateInput(
    string CandidateCode,
    string OutcomeText,
    string? CandidateTypeCode,
    string StateCode);

public sealed record DecisionContractSettingsCommand(
    Guid DecisionContractId,
    byte[] RowVersion,
    [StringLength(300)] string? DecisionHorizon,
    bool ExternalResearchPermitted,
    bool ReviewBeforeActivation,
    bool ReviewBeforeFinal,
    [StringLength(40)] string SemanticValidationMode,
    [StringLength(2000)] string? Notes);

public sealed record DecisionContractLifecycleCommand(
    Guid DecisionContractId,
    byte[] RowVersion,
    [StringLength(2000)] string? Comment);
