using System.ComponentModel.DataAnnotations;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal V2.1 — Closed-loop contracts.
//
// These types wire the closed loop that turns a verification change into a NEW POLOXI investigation:
//
//   verification change → dependency propagation → DOMAIN-NEUTRAL branch signals →
//   POLOXI Candidate×Branch recompetition → entropy/frontier/Information-Value recalculation →
//   ResearchNeed → readiness/reopen → audit.
//
// ARCHITECTURAL INVARIANT: the graph never mutates authoritative POLOXI state directly and never
// scores candidates. It emits DOMAIN-NEUTRAL signals (below) keyed by authoritative BranchId /
// CandidateId; POLOXI Core (ScoreCandidates + DecisionCoreMath) remains the sole scorer.
// ─────────────────────────────────────────────────────────────────────────────────────────────

// The kinds of domain-neutral signal the Legal graph layer may emit to POLOXI Core. The names carry
// NO legal vocabulary — Core reacts to generic dependency/support/constraint changes only.
public static class DecisionBranchSignalKinds
{
    public const string EvidenceChanged   = "BRANCH_EVIDENCE_CHANGED";
    public const string SupportChanged     = "BRANCH_SUPPORT_CHANGED";
    public const string ConstraintChanged  = "BRANCH_CONSTRAINT_CHANGED";
    public const string ReopenRequested     = "BRANCH_REOPEN_REQUESTED";
    public const string RecompetitionRequested = "CANDIDATE_RECOMPETITION_REQUESTED";
}

// A single domain-neutral signal: "something a branch/candidate depends on changed, by this delta."
// SupportDelta is a signed multiplier in [-1,1] applied by POLOXI to the branch's inputs; POLOXI
// decides what (if anything) that means for candidate ranking.
public sealed record DecisionBranchSignal(
    string SignalKind,
    Guid? BranchId,
    Guid? CandidateId,
    double SupportDelta,
    bool ReopenRequested,
    string ReasonCode);

// The structured result of deterministic dependency propagation (§7). The graph produces THIS,
// never a rescored candidate. POLOXI consumes the affected ids + signals to recompete.
public sealed record DependencyImpact(
    IReadOnlyCollection<Guid> ChangedNodeIds,
    IReadOnlyCollection<Guid> ChangedEdgeIds,
    IReadOnlyCollection<Guid> AffectedBranchIds,
    IReadOnlyCollection<Guid> AffectedCandidateIds,
    IReadOnlyCollection<Guid> EssentialDependenciesFailed,
    IReadOnlyCollection<Guid> EssentialDependenciesUnverified,
    IReadOnlyCollection<DecisionBranchSignal> Signals,
    bool RecompetitionRequired,
    bool FrontierRecalculationRequired)
{
    public static DependencyImpact Empty { get; } = new(
        [], [], [], [], [], [], [], false, false);
}

// The outcome-directed research request produced from the highest-IV frontier item after a
// recompetition (§18). Reuses the structured context POLOXI already knows about the branch.
public sealed record DecisionResearchNeedDto(
    Guid DecisionResearchNeedId,
    Guid? DecisionBranchId,
    string? IssueLabel,
    string? PropositionToResolve,
    string? AuthorityKind,
    string? RequiredEvidenceKind,
    string? WhyDecisionRelevant,
    decimal ExpectedDiscrimination,
    decimal CurrentUncertainty,
    decimal InformationValue,
    string? FalsificationCondition,
    string StatusCode);

// The before/after audit of a Candidate×Branch recompetition triggered by a verification change.
public sealed record DecisionRecompetitionDto(
    Guid DecisionRecompetitionId,
    Guid? PreviousWinnerCandidateId,
    Guid? CurrentWinnerCandidateId,
    bool WinnerChanged,
    decimal PreviousEntropy,
    decimal CurrentEntropy,
    decimal PreviousMargin,
    decimal CurrentMargin,
    int ReopenedBranchCount,
    string? ReasonCode,
    DateTime CreatedDateUtc);

// The request that drives the synchronous closed-loop endpoint: set one edge's verification status
// and (optionally) run the full loop. IdempotencyKey guarantees a retried event runs once.
public sealed record DecisionVerificationChangeRequest(
    [Required] Guid EdgeId,
    [Required, StringLength(40)] string NewStatus,
    [StringLength(2000)] string? Notes = null,
    [StringLength(200)] string? IdempotencyKey = null,
    bool RunClosedLoop = true);

// The result surfaced from the closed-loop endpoint / session readback: the refreshed decision
// artifact plus the structured impact narrative and audit trail (no chain-of-thought).
public sealed record DecisionClosedLoopResultDto(
    Guid DecisionSessionId,
    bool Applied,
    bool AlreadyProcessed,
    DependencyImpact Impact,
    DecisionRecompetitionDto? Recompetition,
    DecisionResearchNeedDto? ResearchNeed,
    DecisionSearchResponse Decision,
    IReadOnlyCollection<string> AuditNarrative);

// ── Persistence records for the closed-loop tables (0221). ──────────────────────────────────────

public sealed record DecisionDependencyEventPersistence(
    Guid DecisionDependencyEventId,
    Guid DecisionSessionId,
    Guid TenantId,
    Guid? ActorUserId,
    Guid? MatterId,
    Guid? DecisionGraphEdgeId,
    string IdempotencyKey,
    string? PreviousStatus,
    string NewStatus,
    string? ImpactJson,
    int AffectedBranchCount,
    int AffectedCandidateCount,
    bool RecompetitionTriggered);

public sealed record DecisionRecompetitionPersistence(
    Guid DecisionRecompetitionId,
    Guid DecisionSessionId,
    Guid TenantId,
    Guid? ActorUserId,
    Guid? DecisionDependencyEventId,
    Guid? PreviousWinnerCandidateId,
    Guid? CurrentWinnerCandidateId,
    bool WinnerChanged,
    decimal PreviousEntropy,
    decimal CurrentEntropy,
    decimal PreviousMargin,
    decimal CurrentMargin,
    int ReopenedBranchCount,
    string? PreviousRankingJson,
    string? CurrentRankingJson,
    string? ReasonCode);

public sealed record DecisionResearchNeedPersistence(
    Guid DecisionResearchNeedId,
    Guid DecisionSessionId,
    Guid TenantId,
    Guid? ActorUserId,
    Guid? MatterId,
    Guid? DecisionBranchId,
    Guid? DecisionDependencyEventId,
    string? IssueLabel,
    string? PropositionToResolve,
    string? AuthorityKind,
    string? RequiredEvidenceKind,
    string? WhyDecisionRelevant,
    decimal ExpectedDiscrimination,
    decimal CurrentUncertainty,
    decimal InformationValue,
    string? FalsificationCondition,
    string StatusCode);

public sealed record DecisionFrontierSnapshotPersistence(
    Guid DecisionFrontierSnapshotId,
    Guid DecisionSessionId,
    Guid TenantId,
    Guid? ActorUserId,
    Guid? DecisionRecompetitionId,
    decimal Entropy,
    decimal Margin,
    int OpenFrontierCount,
    Guid? TopBranchId,
    decimal TopBranchInformationValue,
    string? FrontierJson);

// V2.1 closed-loop control settings loaded from POLOXI.Legal_DecisionSetting (0222). Feature flags
// default ON. POLOXI stays authoritative; these only gate graph-derived SIGNALS feeding Core.
public sealed record DecisionV21Settings(
    bool UseDependencyPropagation,
    bool UseGraphDrivenRecompetition,
    bool UseGraphFrontierSignals,
    int LoopMaxReopensPerBranch,
    int LoopMaxResearchActions,
    double LoopNoInformationGainEpsilon,
    bool BenchmarkEnabled);
