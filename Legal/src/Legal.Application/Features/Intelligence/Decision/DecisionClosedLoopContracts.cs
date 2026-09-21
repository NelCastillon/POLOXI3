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
    string StatusCode)
{
    public string ResearchNeedTypeCode { get; init; } = DecisionResearchNeedTypes.LegalAuthority;
}

public static class DecisionResearchNeedTypes
{
    public const string LegalAuthority = "LEGAL_AUTHORITY";
    public const string LegalRule = "LEGAL_RULE";
    public const string ProceduralStandard = "PROCEDURAL_STANDARD";
    public const string MatterFact = "MATTER_FACT";
    public const string MatterEvidence = "MATTER_EVIDENCE";
    public const string Application = "APPLICATION";
    public const string Derived = "DERIVED";
    public const string Mixed = "MIXED";

    public static bool RequiresMatterSources(string? code) =>
        string.Equals(code, MatterFact, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, MatterEvidence, StringComparison.OrdinalIgnoreCase);
}

public static class DecisionResearchSourceClasses
{
    public const string LegalAuthority = "LEGAL_AUTHORITY";
    public const string MatterDocument = "MATTER_DOCUMENT";
    public const string None = "NONE";
}

public sealed record DecisionResearchSemanticLeaf
{
    public required string ResearchKey { get; init; }
    public required string ResearchNeedType { get; init; }
    public required string ResearchQuestion { get; init; }
    public required string Proposition { get; init; }
    public required string SourceClass { get; init; }
    public bool Researchable { get; init; }
    public string? SearchQuery { get; init; }
    public IReadOnlyList<string> SearchConcepts { get; init; } = [];
    public IReadOnlyList<string> AuthorityKinds { get; init; } = [];
    public bool ApplicationDeferred { get; init; }
    public IReadOnlyList<string> CandidateDiscrimination { get; init; } = [];
    public string? ParentResearchKey { get; init; }
    public IReadOnlyList<string> Requires { get; init; } = [];
}

public sealed record DecisionResearchSemanticProposal
{
    public IReadOnlyList<DecisionResearchSemanticLeaf> Leaves { get; init; } = [];
}

public sealed record DecisionResearchabilityResult(
    bool IsAcceptable,
    IReadOnlyList<string> Defects,
    IReadOnlyList<DecisionResearchSemanticLeaf> ResearchableLeaves);

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
    string StatusCode)
{
    public string ResearchNeedTypeCode { get; init; } = DecisionResearchNeedTypes.LegalAuthority;
    public string SourceClassCode { get; init; } = DecisionResearchSourceClasses.LegalAuthority;
    public bool IsResearchable { get; init; } = true;
    public string? ResearchKey { get; init; }
    public string? ResearchQuestion { get; init; }
    public string? SearchQuery { get; init; }
    public string? SearchConceptsJson { get; init; }
    public string? AuthorityKindsJson { get; init; }
    public bool ApplicationDeferred { get; init; }
    public string? ParentResearchKey { get; init; }
    public string? RequiredResearchKeysJson { get; init; }
    public string? CandidateDiscriminationJson { get; init; }
    public string? SemanticProposalStatusCode { get; init; }
    public string? SemanticProposalReasonCode { get; init; }
}

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

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Bounded Research Loop control settings (loaded from POLOXI.Legal_DecisionSetting).
//
// Governs the AUTONOMOUS closed research loop: Retrieval → Verification → Evidence Promotion →
// Verified Signal → Dependency Propagation → Candidate×Branch Recompetition → frontier/IV/readiness
// recalculation, repeated until an explicit STOP condition. The loop is a bounded convergence engine,
// never "research until ready": every round is budget-checked. Default OFF so the shadow baseline is
// preserved until the loop is validated end to end.
//
// Stop conditions (any one halts the loop):
//   • DecisionReady reached,
//   • MaxRounds reached,
//   • MaxRetrievals reached (cumulative retrieval operations),
//   • no frontier item with information value ≥ MinFrontierInformationValue,
//   • no material state change after a round (|Δentropy| < NoStateChangeEpsilon AND no winner flip),
//   • all essential research needs exhausted.
// UseSeedRetriever routes retrieval to a deterministic seed source (test/demo) instead of the live
// provider, so the loop can be exercised end to end against seeded matters.
public sealed record DecisionResearchLoopSettings(
    bool Enabled,
    int MaxRounds,
    int MaxRetrievals,
    double MinFrontierInformationValue,
    double NoStateChangeEpsilon,
    bool UseSeedRetriever);

// Explicit STOP reasons for the bounded research loop. Every terminated loop carries exactly one,
// so the caller can distinguish healthy convergence (DecisionReady) from budget/threshold cut-offs.
public static class DecisionResearchLoopStopReasons
{
    public const string LoopDisabled          = "LOOP_DISABLED";            // feature flag OFF
    public const string DecisionReady         = "DECISION_READY";           // converged
    public const string MaxRounds             = "MAX_ROUNDS";               // round budget hit
    public const string RetrievalBudget       = "RETRIEVAL_BUDGET";         // cumulative retrieval cap hit
    public const string FrontierBelowThreshold= "FRONTIER_BELOW_THRESHOLD"; // no frontier item worth researching
    public const string NoVerifiableEdge      = "NO_VERIFIABLE_EDGE";       // nothing left to verify for the frontier
    public const string ResearchNeedUnresolved= "RESEARCH_NEED_UNRESOLVED"; // semantic proposal failed researchability after one repair
    public const string NoStateChange         = "NO_STATE_CHANGE";          // round produced no material movement
    public const string NoGraph               = "NO_GRAPH";                 // session has no dependency graph
    public const string RoundFailed           = "RESEARCH_ROUND_FAILED";    // a round faulted before commit; prior state preserved
    public const string AlreadyRunning        = "ALREADY_RUNNING";          // another loop is active for this session
}

// One iteration of the bounded loop: what was researched, what verification state resulted, and the
// entropy/margin movement it produced. Purely an audit surface (no chain-of-thought).
public sealed record DecisionResearchRoundDto(
    int RoundNumber,
    Guid? TargetBranchId,
    string? TargetBranchLabel,
    decimal TargetInformationValue,
    Guid? VerifiedEdgeId,
    string EdgeVerificationStatus,
    string EvidenceLifecycleState,
    int SourcesRetrieved,
    bool WinnerChanged,
    decimal EntropyBefore,
    decimal EntropyAfter,
    IReadOnlyCollection<string> Narrative)
{
    public string? PropositionToResolve { get; init; }
    public int SourcesEvaluated { get; init; }
    public IReadOnlyDictionary<string, int> VerificationDispositionCounts { get; init; }
        = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, int> AttachmentStateCounts { get; init; }
        = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public int AuthoritativeChanges { get; init; }
}

// Safe production diagnostic for a failed research round. Deliberately excludes stack traces,
// source locations, prompts, and provider payloads.
public sealed record DecisionResearchFailureDto(
    int RoundNumber,
    string Stage,
    string ExceptionType,
    string Reason,
    bool AuthoritativeStateChanged);

// The result of running the bounded autonomous research loop end to end: the ordered per-round audit,
// the explicit stop reason, cumulative budget usage, and the final decision artifact.
public sealed record DecisionResearchLoopResultDto(
    Guid DecisionSessionId,
    bool Enabled,
    int RoundsExecuted,
    int TotalRetrievals,
    string StopReason,
    IReadOnlyCollection<DecisionResearchRoundDto> Rounds,
    DecisionSearchResponse Decision,
    DecisionResearchFailureDto? Failure = null);
