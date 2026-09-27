namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity contracts (Phase 1: Change Awareness).
//
// A legal conclusion must remain connected to the evidence and assumptions that made it valid. These
// records back the POLOXI.Legal_Decision* integrity tables (migration 0340). Judz owns change
// awareness and snapshot lineage; POLOXI Core remains the authoritative, stateless scoring engine.
//
// Two independent status axes are modeled deliberately:
//   • Readiness  — was the decision ready AT evaluation time?  (historical, immutable)
//   • Reliance   — does the decision remain current enough to rely on TODAY?  (dynamic)
// ─────────────────────────────────────────────────────────────────────────────────────────────

// ── Reliance status vocabulary (separate from readiness). ──────────────────────────────────────
public static class DecisionRelianceStatus
{
    // No known material change requires reevaluation.
    public const string Current = "CURRENT";
    // New information may affect the decision; targeted review pending.
    public const string ReviewPending = "REVIEW_PENDING";
    // A material dependency has changed or become disputed.
    public const string ReassessmentRequired = "REASSESSMENT_REQUIRED";
    // A newer evaluated decision snapshot has replaced this one.
    public const string Superseded = "SUPERSEDED";
    // The prior conclusion has been withdrawn through an authorized review.
    public const string Withdrawn = "WITHDRAWN";
}

// ── Change-event classification vocabulary. ────────────────────────────────────────────────────
public static class MatterChangeClassification
{
    // Duplicate/immaterial copy of already-reviewed evidence — preserve event, no reevaluation.
    public const string NoMaterialImpact = "NO_MATERIAL_IMPACT";
    // May affect existing propositions — mark relevant conclusions for targeted review.
    public const string PotentialImpact = "POTENTIAL_IMPACT";
    // Directly contradicts previously verified evidence — suspend reliance, trigger reassessment.
    public const string MaterialContradiction = "MATERIAL_CONTRADICTION";
    // A material new fact with no existing proposition — may require a new/reopened branch.
    public const string NewMaterialFact = "NEW_MATERIAL_FACT";
}

public static class MatterChangeProcessingStatus
{
    public const string Pending = "PENDING";
    public const string Processed = "PROCESSED";
    public const string Failed = "FAILED";
}

public static class MatterChangeSource
{
    public const string DocumentUpload = "DOCUMENT_UPLOAD";
    public const string CorrectedField = "CORRECTED_MATTER_FIELD";
    public const string NewAuthority = "NEW_AUTHORITY";
    public const string AttorneyClarification = "ATTORNEY_CLARIFICATION";
    public const string ExternalEvent = "EXTERNAL_EVENT";
}

public static class PropositionEvidenceLinkKind
{
    public const string Support = "SUPPORT";
    public const string Contradiction = "CONTRADICTION";
    public const string Context = "CONTEXT";
}

public static class PropositionEvidenceLinkStatus
{
    public const string Active = "ACTIVE";
    // A later contradicting source arrived; the supporting relationship is contested (not invalid).
    public const string Contested = "CONTESTED";
    public const string Withdrawn = "WITHDRAWN";
}

public static class DecisionImpactKind
{
    public const string Proposition = "PROPOSITION";
    public const string Candidate = "CANDIDATE";
    public const string Decision = "DECISION";
}

public static class DecisionImpactSeverity
{
    public const string None = "NONE";
    public const string Potential = "POTENTIAL";
    public const string Material = "MATERIAL";
}

public static class DecisionReviewTaskStatus
{
    public const string Open = "OPEN";
    public const string InProgress = "IN_PROGRESS";
    public const string Resolved = "RESOLVED";
    public const string Dismissed = "DISMISSED";
}

public static class DecisionReviewTaskPriority
{
    public const string Low = "LOW";
    public const string Normal = "NORMAL";
    public const string High = "HIGH";
    public const string Critical = "CRITICAL";
}

// ── Persistence records (mirror the 0340 tables). ──────────────────────────────────────────────

public sealed record DecisionSnapshotPersistence(
    Guid DecisionSnapshotId,
    Guid DecisionMatterId,
    Guid? DecisionSessionId,
    int SnapshotNumber,
    string? Title,
    string? PropositionStatement,
    string ReadinessStatusCode,
    string RelianceStatusCode,
    string? RelianceReason,
    bool IsAttorneyApproved,
    Guid? ApprovedByUserId,
    DateTime? ApprovedDateUtc,
    Guid? SupersededBySnapshotId,
    string? EvidenceSummaryJson,
    DateTime EvaluatedDateUtc,
    Guid TenantId,
    Guid? ActorUserId);

public sealed record MatterChangeEventPersistence(
    Guid MatterChangeEventId,
    Guid DecisionMatterId,
    string ChangeSourceCode,
    Guid? LegalDocumentId,
    Guid? LegalDocumentVersionId,
    string? SourceHash,
    string? SourceLabel,
    DateTime? DocumentDateUtc,
    string IdempotencyKey,
    string? ClassificationCode,
    string? Summary,
    string? CandidateFactsJson,
    string ProcessingStatusCode,
    string? ProcessingError,
    int AffectedPropositionCount,
    int AffectedCandidateCount,
    DateTime? ProcessedDateUtc,
    Guid TenantId,
    Guid? ActorUserId);

public sealed record DecisionImpactPersistence(
    Guid DecisionImpactId,
    Guid MatterChangeEventId,
    Guid DecisionMatterId,
    Guid? DecisionSnapshotId,
    string AffectedKindCode,
    string AffectedKey,
    string? AffectedLabel,
    string? PreviousStateCode,
    string? CurrentStateCode,
    string ImpactSeverityCode,
    string? Rationale,
    Guid TenantId,
    Guid? ActorUserId);

public sealed record DecisionReviewTaskPersistence(
    Guid DecisionReviewTaskId,
    Guid DecisionMatterId,
    Guid? MatterChangeEventId,
    Guid? DecisionSnapshotId,
    string TaskKindCode,
    string Title,
    string? Detail,
    string? RequiredAction,
    string PriorityCode,
    string StatusCode,
    Guid? AssignedToUserId,
    Guid? ResolvedByUserId,
    DateTime? ResolvedDateUtc,
    string? ResolutionNotes,
    Guid TenantId,
    Guid? ActorUserId);

public sealed record MatterDependencyPersistence(
    Guid DecisionDependencyId,
    Guid DecisionMatterId,
    Guid? DecisionSnapshotId,
    string DependentKindCode,
    string DependentKey,
    string DependsOnKindCode,
    string DependsOnKey,
    string RelationCode,
    bool IsEssential,
    decimal SupportWeight,
    string StatusCode,
    Guid TenantId,
    Guid? ActorUserId);

public sealed record PropositionEvidenceLinkPersistence(
    Guid PropositionEvidenceLinkId,
    Guid DecisionMatterId,
    Guid? DecisionSnapshotId,
    string PropositionKey,
    string? PropositionStatement,
    Guid? LegalDocumentVersionId,
    Guid? LegalDocumentPassageId,
    string LinkKindCode,
    decimal SupportWeight,
    string StatusCode,
    string? Notes,
    Guid TenantId,
    Guid? ActorUserId);

// ── Read DTOs for API/UI (Decision Change Review workspace). ───────────────────────────────────

public sealed record DecisionSnapshotDto(
    Guid DecisionSnapshotId,
    Guid DecisionMatterId,
    Guid? DecisionSessionId,
    int SnapshotNumber,
    string? Title,
    string? PropositionStatement,
    string ReadinessStatusCode,
    string RelianceStatusCode,
    string? RelianceReason,
    bool IsAttorneyApproved,
    Guid? SupersededBySnapshotId,
    DateTime EvaluatedDateUtc,
    DateTime CreatedDateUtc);

public sealed record DecisionImpactDto(
    Guid DecisionImpactId,
    Guid MatterChangeEventId,
    string AffectedKindCode,
    string AffectedKey,
    string? AffectedLabel,
    string? PreviousStateCode,
    string? CurrentStateCode,
    string ImpactSeverityCode,
    string? Rationale);

public sealed record DecisionReviewTaskDto(
    Guid DecisionReviewTaskId,
    Guid DecisionMatterId,
    Guid? MatterChangeEventId,
    Guid? DecisionSnapshotId,
    string TaskKindCode,
    string Title,
    string? Detail,
    string? RequiredAction,
    string PriorityCode,
    string StatusCode,
    DateTime CreatedDateUtc);

public sealed record MatterChangeEventDto(
    Guid MatterChangeEventId,
    Guid DecisionMatterId,
    string ChangeSourceCode,
    Guid? LegalDocumentId,
    Guid? LegalDocumentVersionId,
    string? SourceLabel,
    DateTime? DocumentDateUtc,
    string? ClassificationCode,
    string? Summary,
    string ProcessingStatusCode,
    string? ProcessingError,
    int AffectedPropositionCount,
    int AffectedCandidateCount,
    DateTime? ProcessedDateUtc,
    DateTime CreatedDateUtc);

// A fully-composed Decision Change Review for one change event: the source, the classification,
// the before/after impact rows, the affected snapshot, and the produced review tasks.
public sealed record DecisionChangeReviewDto(
    MatterChangeEventDto ChangeEvent,
    DecisionSnapshotDto? AffectedSnapshot,
    IReadOnlyCollection<DecisionImpactDto> Impacts,
    IReadOnlyCollection<DecisionReviewTaskDto> ReviewTasks);

// The matter-level Decision Change Review summary shown in the workspace list.
public sealed record MatterChangeReviewSummaryDto(
    Guid DecisionMatterId,
    string MatterTitle,
    int OpenReviewTaskCount,
    int PendingChangeCount,
    string? LatestRelianceStatusCode,
    DateTime? LatestChangeDateUtc);

// Result of processing one matter change through the Phase 1 pipeline.
public sealed record MatterChangeProcessingResult(
    Guid MatterChangeEventId,
    string ClassificationCode,
    int AffectedPropositionCount,
    int AffectedCandidateCount,
    IReadOnlyCollection<Guid> ReviewTaskIds);

// Attorney action on a change-review task (acknowledge / resolve / dismiss). The attorney stays in
// control; the system never auto-executes consequential decision changes from this action.
public sealed record UpdateReviewTaskStatusRequest(
    string StatusCode,
    string? ResolutionNotes);
