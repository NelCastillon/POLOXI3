namespace Legal.Application.Features.Intelligence.Decision;

// ── Attorney Decision Input (ADI) / "Human Intelligence" read models ──────────────────────────────
// DB-backed projections for the read-only Human Intelligence tab. These describe the canonical
// decision-node hierarchy plus the multi-attorney relative assessments and the single approved matter
// assessment per node. POLOXI remains the authoritative evaluator; nothing here computes scoring.

// A single attorney's independent relative-value assessment for a decision node (§4, §5).
public sealed record AttorneyRelativeAssessmentDto(
    Guid AssessmentId,
    Guid DecisionNodeId,
    Guid AttorneyUserId,
    string AttorneyDisplayName,
    string? AttorneyRole,
    decimal ConfirmedValue,
    decimal? SuggestedMidpoint,
    Guid? PreviousSiblingId,
    decimal? PreviousSiblingValue,
    Guid? NextSiblingId,
    decimal? NextSiblingValue,
    string MethodCode,
    string? Rationale,
    string StatusCode,
    long AssessmentVersion,
    DateTimeOffset CreatedDateUtc);

// The single active approved matter assessment for a node/scoring context (§5 invariant).
public sealed record ApprovedMatterAssessmentDto(
    Guid ApprovalId,
    Guid DecisionNodeId,
    Guid AssessmentId,
    decimal ConfirmedValue,
    Guid ApprovedByUserId,
    string ApprovedByDisplayName,
    string GovernancePolicyCode,
    DateTimeOffset ApprovedDateUtc);

// A canonical decision node with its attorney signals for the Human Intelligence panel.
public sealed record AttorneyDecisionNodeDto(
    Guid DecisionNodeId,
    Guid? ParentNodeId,
    string CanonicalKey,
    string NodeKindCode,
    int NodeLevel,
    string NodeText,
    string OriginCode,
    string? PlacementKey,
    string StructuralStateCode,
    string EvidenceStateCode,
    string AuthorityStateCode,
    decimal? EvaluatedValue,
    IReadOnlyCollection<AttorneyRelativeAssessmentDto> Assessments,
    ApprovedMatterAssessmentDto? ApprovedAssessment,
    int OpenChallengeCount);

// Aggregate matter-level payload for the Human Intelligence tab.
public sealed record MatterHumanIntelligenceDto(
    Guid MatterId,
    bool AttorneyDecisionInputEnabled,
    int NodeCount,
    int AttorneyCount,
    int AssessmentCount,
    int ApprovedAssessmentCount,
    int OpenChallengeCount,
    IReadOnlyCollection<AttorneyDecisionNodeDto> Nodes);
