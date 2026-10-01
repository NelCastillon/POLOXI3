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
    DateTime CreatedDateUtc);

// The single active approved matter assessment for a node/scoring context (§5 invariant).
// PreviousSiblingValue/NextSiblingValue carry the comparable sibling band the attorney placed WITHIN;
// they let POLOXI express the placement as a relative-position magnitude (the intelligence encoded by
// where the attorney inserted/overwrote the value). Null when no comparable band existed at placement.
public sealed record ApprovedMatterAssessmentDto(
    Guid ApprovalId,
    Guid DecisionNodeId,
    Guid AssessmentId,
    decimal ConfirmedValue,
    Guid ApprovedByUserId,
    string ApprovedByDisplayName,
    string GovernancePolicyCode,
    DateTime ApprovedDateUtc,
    decimal? PreviousSiblingValue = null,
    decimal? NextSiblingValue = null);

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
    int OpenChallengeCount,
    long NodeVersion = 1);

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

// ── Attorney Decision Input write path — commands, analyses, preview and results ─────────────────
// These records drive the interactive Add-Proposition workflow (Define → Placement → Preview → Confirm).
// POLOXI remains the single authoritative evaluator; nothing here computes a new final score.

// §21 Define step: create a draft attorney decision node under a selected parent/candidate scope.
public sealed record CreateAttorneyInputCommand(
    Guid MatterId,
    Guid? ParentNodeId,
    Guid CandidateNodeId,      // owning L1 candidate for scope
    string NodeKindCode,       // Candidate | Factor | Proposition
    int NodeLevel,             // 1..n (L1/L2 not forcibly atomized; L3+ APR candidates)
    string NodeText,
    string? Rationale);

// A draft node returned by CreateDraftAsync with deterministic + AI structural checks (§8, §9).
public sealed record AttorneyInputDraft(
    Guid DraftId,
    Guid MatterId,
    Guid? ParentNodeId,
    Guid CandidateNodeId,
    string NodeKindCode,
    int NodeLevel,
    string NodeText,
    string? Rationale,
    string AtomicityDisposition,     // Atomic | Compound | Ambiguous | Duplicate | InvalidPremise | SemanticDrift | Unresolved | NotApplicable
    string ParentFidelity,           // Good | Mismatch | Unknown
    string? SuggestedParentNodeId,
    IReadOnlyCollection<AttorneyDuplicateCandidateDto> PossibleDuplicates,
    string LegalContextSummary,
    bool AiChecksAvailable);

public sealed record AttorneyDuplicateCandidateDto(
    Guid DecisionNodeId,
    string CanonicalKey,
    string NodeText,
    decimal SimilarityScore);

// §21 Placement step: request the suggested midpoint + neighbor bounds for a placement position.
public sealed record AnalyzePlacementCommand(
    Guid MatterId,
    Guid? ParentNodeId,
    Guid CandidateNodeId,
    int NodeLevel,
    Guid? PreviousSiblingId,
    Guid? NextSiblingId);

// §4 midpoint suggestion result. SuggestedMidpoint is a suggestion, not an evidence score.
public sealed record PlacementAnalysis(
    Guid? PreviousSiblingId,
    string? PreviousSiblingText,
    decimal? PreviousSiblingValue,
    Guid? NextSiblingId,
    string? NextSiblingText,
    decimal? NextSiblingValue,
    decimal? SuggestedMidpoint,
    decimal LowerBound,
    decimal UpperBound,
    string MethodCode,               // Midpoint | BoundedEntry | PendingAssessment
    bool Comparable,                 // false when interpolation is not valid (§4)
    string? Disclosure);             // required disclosure text that position has scoring meaning

// §15 Preview step: non-committing projection through the existing POLOXI scoring path.
public sealed record PreviewAttorneyInputCommand(
    Guid MatterId,
    Guid SessionId,
    Guid CandidateNodeId,
    Guid? ParentNodeId,
    int NodeLevel,
    string NodeText,
    decimal ConfirmedValue,
    Guid? PreviousSiblingId,
    Guid? NextSiblingId,
    Guid BaseSnapshotId);

public sealed record DecisionMutationPreview(
    Guid PreviewToken,
    Guid BaseSnapshotId,
    Guid CandidateNodeId,
    string CandidateText,
    decimal CurrentCandidateScore,
    decimal ProjectedCandidateScore,
    decimal CandidateScoreDelta,
    decimal UncertaintyDelta,
    decimal InformationValueDelta,
    string InformationValueBand,     // Low | Medium | High
    bool FrontierChanged,
    string? ReadinessBefore,
    string? ReadinessAfter,
    IReadOnlyCollection<string> AffectedPath,
    IReadOnlyCollection<string> IntegrityFindings,
    bool ScoreProjectionAdvisory,    // true when the authoritative recompute is deferred to async closure
    string ExplanationSummary);      // §17 CDI "why it moves"

// §16 Confirm/Commit step: commit the node + assessment (+optional approval) in one transaction.
public sealed record CommitAttorneyInputCommand(
    Guid MatterId,
    Guid CandidateNodeId,
    Guid? ParentNodeId,
    string NodeKindCode,
    int NodeLevel,
    string NodeText,
    string? Rationale,
    decimal ConfirmedValue,
    decimal? SuggestedMidpoint,
    Guid? PreviousSiblingId,
    decimal? PreviousSiblingValue,
    Guid? NextSiblingId,
    decimal? NextSiblingValue,
    string MethodCode,
    Guid PreviewToken,
    Guid BaseSnapshotId,
    long ExpectedHierarchyVersion,
    Guid IdempotencyKey,
    string? GovernancePolicyCode,    // when the committing attorney is authorized to self-approve
    bool RequestApproval);

public sealed record CommitResult(
    Guid DecisionNodeId,
    Guid AssessmentId,
    Guid? ApprovalId,
    long NodeVersion,
    string NodeStatusCode,
    Guid ChangeEventId,
    bool ReevaluationQueued);

// §5 node-scoped assessment submission (multi-attorney, never auto-averaged).
public sealed record SubmitAttorneyAssessmentCommand(
    Guid MatterId,
    Guid DecisionNodeId,
    decimal ConfirmedValue,
    decimal? SuggestedMidpoint,
    Guid? PreviousSiblingId,
    decimal? PreviousSiblingValue,
    Guid? NextSiblingId,
    decimal? NextSiblingValue,
    string MethodCode,
    string? Rationale,
    Guid BaseSnapshotId,
    Guid IdempotencyKey);

// §5 approve a specific assessment as the single active Approved Matter Assessment for the node.
public sealed record ApproveMatterAssessmentCommand(
    Guid MatterId,
    Guid DecisionNodeId,
    Guid AssessmentId,
    string GovernancePolicyCode,
    Guid BaseSnapshotId,
    Guid IdempotencyKey);

// §3 raise a challenge against a node or relationship.
public sealed record RaiseChallengeCommand(
    Guid MatterId,
    Guid DecisionNodeId,
    string ChallengeTypeCode,        // Structural | Placement | Duplicate | Premise | Coverage | Relationship | Evidence | Authority
    string ChallengeText,
    Guid IdempotencyKey);

// §4 reposition an existing node — creates a new version and DecisionDelta.
public sealed record RepositionNodeCommand(
    Guid MatterId,
    Guid DecisionNodeId,
    decimal ConfirmedValue,
    Guid? PreviousSiblingId,
    decimal? PreviousSiblingValue,
    Guid? NextSiblingId,
    decimal? NextSiblingValue,
    string MethodCode,
    string? Rationale,
    long ExpectedNodeVersion,
    Guid BaseSnapshotId,
    Guid IdempotencyKey);
