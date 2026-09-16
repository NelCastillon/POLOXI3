using System.ComponentModel.DataAnnotations;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Contracts for the self-contained POLOXI Legal Decision Intelligence module (/legal/decision).
// The DECISION is the primary object (placement-map §2): a request opens/continues a decision
// session whose authoritative state (candidates, branches, evidence, frontier, flip points) is
// owned by POLOXI Core. The LLM only proposes semantics (§3: Proposal_LLM ≠ State_Authoritative).
// These types are intentionally separate from the Intelligence Wide (/legal/search) contracts.
// ─────────────────────────────────────────────────────────────────────────────────────────────

public sealed record DecisionSearchRequest(
    Guid TenantId,
    Guid UserId,
    [Required, StringLength(4000, MinimumLength = 2)] string Query,
    [Range(1, 100)] int MaximumResults = 25,
    [Required, StringLength(120)] string CorrelationId = "")
{
    public IReadOnlyCollection<string> GrantedPermissions { get; init; } = [];

    // POLOXI Engine toggle: true runs the full decision-state loop; false returns a direct LLM answer.
    public bool UsePoloxiEngine { get; init; } = true;

    // Model selection: null/empty = Auto (highest-priority active DB route); otherwise a specific ModelCode.
    [StringLength(100)] public string? ModelCode { get; init; }

    // Context (POLOXI.Legal_DecisionContext.ContextCode): null/empty/GENERAL default; LEGAL grounds in law.
    [StringLength(50)] public string? ContextCode { get; init; }

    // Continuation of a session that ended USER_CLARIFICATION_REQUIRED (§7 clarification loop).
    public Guid? ParentDecisionSessionId { get; init; }
    [StringLength(500)] public string? ClarificationAnswer { get; init; }
    [StringLength(300)] public string? ClarificationTarget { get; init; }

    // Matter this decision belongs to (cockpit / dashboard grouping). Null = ad-hoc decision.
    public Guid? MatterId { get; init; }

    // Counterfactual "what if" assumption injected into the effective query (flip-point Test flow).
    // e.g. "Assume Dr. Smith's expert declaration is EXCLUDED." Re-decides under the hypothetical.
    [StringLength(1000)] public string? CounterfactualAssumption { get; init; }

    // POLOXI Legal V2 ablation toggle: when true, runs the dependency-aware layer (typed legal
    // graph + independent verifier + local propagation + strongest-loser gate + dependency-based
    // readiness) on top of the V1 loop. null defers to Decision.V2.UseDependencyGraph.Default.
    public bool? UseDependencyGraph { get; init; }

    // Structured posture context (from the matter) fed into the DECISION_GRAPH proposal so the
    // typed legal graph and readiness reflect what decision is actually being asked NOW.
    [StringLength(200)] public string? Posture { get; init; }
    [StringLength(300)] public string? MotionTarget { get; init; }
}

// DB-backed model option for the decision Model dropdown.
public sealed record DecisionModelOptionDto(string ModelCode, string DeploymentName, string? ProviderTypeCode);

// DB-backed context option for the decision Context dropdown.
public sealed record DecisionContextDto(string ContextCode, string DisplayName, string? Description, bool IsDefault);

public static class DecisionContexts
{
    public const string General = "GENERAL";
    public const string Legal = "LEGAL";
}

// Branch lifecycle states (§5). Reversible uncertainty, irreversible invalidation.
public static class DecisionBranchStates
{
    public const string Active = "ACTIVE";
    public const string Dormant = "DORMANT";
    public const string Resolved = "RESOLVED";
    public const string Reopened = "REOPENED";
}

// Session/terminal status codes (§33,§34).
public static class DecisionStatusCodes
{
    public const string Running = "RUNNING";
    public const string DecisionReady = "DECISION_READY";
    // Leading outcome identified, but executable high-value research still remains on the frontier.
    // This is NOT terminal-because-nothing-is-left; it is an honest "provisional" verdict.
    public const string ProvisionalDecision = "PROVISIONAL_DECISION";
    public const string UserClarificationRequired = "USER_CLARIFICATION_REQUIRED";
    public const string ResearchExhausted = "RESEARCH_EXHAUSTED";
    public const string BudgetExhausted = "BUDGET_EXHAUSTED";
    public const string Abstained = "ABSTAINED";
    public const string Failed = "FAILED";
}

// Multi-dimensional epistemic state of a candidate (§9) — never a single opaque confidence.
public sealed record DecisionCandidateDto(
    Guid DecisionCandidateId,
    string CandidateCode,
    string DisplayName,
    string? Outcome,
    decimal LegalSupport,
    decimal FactSupport,
    decimal EvidenceSupport,
    decimal AuthoritySupport,
    decimal Verification,
    decimal Uncertainty,
    decimal Discrimination,
    decimal RankingImpact,
    decimal Diversity,
    decimal RedundancyPenalty,
    decimal CompositeScore,
    decimal DecisionSupportCeiling,
    int RankOrder,
    bool IsWinner,
    bool IsEliminated);

public sealed record DecisionBranchDto(
    Guid DecisionBranchId,
    Guid? ParentDecisionBranchId,
    int LevelNumber,
    string BranchCode,
    string DisplayName,
    string? Interpretation,
    string BranchStateCode,
    decimal InformationValue,
    decimal DecisionRelevance,
    decimal FlipPotential,
    decimal EvidenceAvailability,
    decimal AdvScore,
    bool IsOnFrontier,
    string? StopReason,
    int SortOrder);

public sealed record DecisionEvidenceDto(
    Guid DecisionEvidenceId,
    Guid? DecisionBranchId,
    string? SourceRef,
    string? SourceTitle,
    string? Snippet,
    decimal VerificationValue,
    string VerificationStatus);

public sealed record DecisionFlipPointDto(
    Guid DecisionFlipPointId,
    Guid? DecisionBranchId,
    string Description,
    decimal ChangeCost,
    bool WinnerChanges,
    int RankDelta);

// The structured decision artifact (§35): a snapshot of Core state, not an LLM conclusion.
public sealed record DecisionSearchResponse(
    Guid DecisionSessionId,
    string Query,
    string StatusCode,
    string? TerminalStateCode,
    string TerminationReasonCode,
    int DepthReached,
    int LlmCallCount,
    decimal CandidateEntropy,
    decimal DecisionMargin,
    decimal ContractCompleteness,
    string? FinalAnswer,
    Guid? WinnerCandidateId,
    IReadOnlyCollection<DecisionCandidateDto> Candidates,
    IReadOnlyCollection<DecisionBranchDto> Branches,
    IReadOnlyCollection<DecisionEvidenceDto> Evidence,
    IReadOnlyCollection<DecisionFlipPointDto> FlipPoints,
    long DurationMilliseconds)
{
    public string? ClarificationQuestion { get; init; }
    public string? ClarificationTarget { get; init; }

    // Matter linkage (cockpit deep-link) and the assumption this decision was run under, if any.
    public Guid? MatterId { get; init; }
    public string? CounterfactualAssumption { get; init; }

    // Deterministically-derived Next Best Action (§ next action): the single highest-impact
    // investigation POLOXI recommends. Null when the decision is fully ready / no open frontier.
    public DecisionNextActionDto? NextBestAction { get; init; }

    // Decision readiness checklist (shown separately from outcome strength; §readiness).
    public IReadOnlyCollection<DecisionReadinessItemDto> Readiness { get; init; } = [];

    // ── POLOXI Legal V2 (dependency-aware) additions. Empty/null on the V1 path. ──
    public bool UsedDependencyGraph { get; init; }
    public IReadOnlyCollection<DecisionGraphNodeDto> GraphNodes { get; init; } = [];
    public IReadOnlyCollection<DecisionGraphEdgeDto> GraphEdges { get; init; } = [];
    public DecisionLosingSideTestDto? LosingSideTest { get; init; }
    public DecisionReadinessVerdictDto? ReadinessVerdict { get; init; }

    // ── POLOXI Legal V2.1 (closed-loop) additions. Empty/null unless the loop has run. ──
    public DecisionRecompetitionDto? LastRecompetition { get; init; }
    public DecisionResearchNeedDto? PendingResearchNeed { get; init; }
}

// The single highest-impact recommended investigation, derived from the decision frontier.
public sealed record DecisionNextActionDto(
    string Title,
    string ImpactCode,      // VERY HIGH | HIGH | MEDIUM | LOW
    string Rationale,
    Guid? TargetBranchId,
    decimal ExpectedInformationValue,
    decimal FlipPotential);

// One item on the decision-readiness checklist (satisfied / attention).
public sealed record DecisionReadinessItemDto(
    string Label,
    bool Satisfied,
    string? Detail);

// A matter aggregate that groups decision sessions (dashboard card).
public sealed record DecisionMatterDto(
    Guid DecisionMatterId,
    string Title,
    string? MatterTypeCode,
    string? Jurisdiction,
    string? Posture,
    string? Description,
    string StatusCode,
    DateTime CreatedDateUtc,
    DateTime? ModifiedDateUtc)
{
    // ── Structured Type dimension (legacy MatterTypeCode + Subtype). ──
    public string? Subtype { get; init; }

    // ── Structured Jurisdiction dimension (legacy Jurisdiction + these). ──
    public string? CourtSystem { get; init; }
    public string? State { get; init; }
    public string? CourtLevel { get; init; }
    public string? County { get; init; }
    public string? GoverningLaw { get; init; }

    // ── Structured Posture dimension (legacy Posture + these). ──
    public string? MovingParty { get; init; }
    public string? RespondingParty { get; init; }
    public string? MotionTarget { get; init; }
    public string? RequestedDisposition { get; init; }

    // Derived from the latest decision session for this matter (null when no decision yet).
    public Guid? LatestSessionId { get; init; }
    public string? CurrentOutcome { get; init; }
    public string? DecisionStatusCode { get; init; }
    public int CriticalFlipPointCount { get; init; }
    public DateTime? LastDecidedUtc { get; init; }
}

// Request to create a matter.
public sealed record DecisionMatterCreateRequest(
    [Required, StringLength(300, MinimumLength = 2)] string Title,
    [StringLength(80)] string? MatterTypeCode,
    [StringLength(120)] string? Jurisdiction,
    [StringLength(200)] string? Posture,
    [StringLength(4000)] string? Description)
{
    // ── Structured Type dimension. ──
    [StringLength(120)] public string? Subtype { get; init; }

    // ── Structured Jurisdiction dimension. ──
    [StringLength(120)] public string? CourtSystem { get; init; }
    [StringLength(120)] public string? State { get; init; }
    [StringLength(120)] public string? CourtLevel { get; init; }
    [StringLength(120)] public string? County { get; init; }
    [StringLength(120)] public string? GoverningLaw { get; init; }

    // ── Structured Posture dimension. ──
    [StringLength(200)] public string? MovingParty { get; init; }
    [StringLength(200)] public string? RespondingParty { get; init; }
    [StringLength(300)] public string? MotionTarget { get; init; }
    [StringLength(300)] public string? RequestedDisposition { get; init; }
}

// Request to update a matter's metadata (status is changed via the status endpoint).
public sealed record DecisionMatterUpdateRequest(
    [Required, StringLength(300, MinimumLength = 2)] string Title,
    [StringLength(80)] string? MatterTypeCode,
    [StringLength(120)] string? Jurisdiction,
    [StringLength(200)] string? Posture,
    [StringLength(4000)] string? Description)
{
    // ── Structured Type dimension. ──
    [StringLength(120)] public string? Subtype { get; init; }

    // ── Structured Jurisdiction dimension. ──
    [StringLength(120)] public string? CourtSystem { get; init; }
    [StringLength(120)] public string? State { get; init; }
    [StringLength(120)] public string? CourtLevel { get; init; }
    [StringLength(120)] public string? County { get; init; }
    [StringLength(120)] public string? GoverningLaw { get; init; }

    // ── Structured Posture dimension. ──
    [StringLength(200)] public string? MovingParty { get; init; }
    [StringLength(200)] public string? RespondingParty { get; init; }
    [StringLength(300)] public string? MotionTarget { get; init; }
    [StringLength(300)] public string? RequestedDisposition { get; init; }
}

// Request to transition a matter's lifecycle status (OPEN <-> CLOSED).
public sealed record DecisionMatterStatusUpdateRequest(
    [Required, StringLength(60)] string StatusCode);

// Matter lifecycle status codes (POLOXI.Legal_DecisionMatter.StatusCode).
public static class DecisionMatterStatusCodes
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";

    public static bool IsValid(string? statusCode) =>
        string.Equals(statusCode, Open, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(statusCode, Closed, StringComparison.OrdinalIgnoreCase);
}

// A single decision session in a matter's history (dashboard/detail history list).
public sealed record DecisionSessionSummaryDto(
    Guid DecisionSessionId,
    string QueryText,
    string StatusCode,
    string? TerminalStateCode,
    string? CurrentOutcome,
    decimal DecisionMargin,
    decimal CandidateEntropy,
    int DepthReached,
    int LlmCallCount,
    long DurationMs,
    int CriticalFlipPointCount,
    DateTime CreatedDateUtc);

// Distinct free-form facet values already stored across matters (tenant-scoped) used to
// pre-populate the create/edit matter dropdowns. DB is the source of truth; no hardcoded lists.
public sealed record DecisionMatterFacetsDto(
    IReadOnlyCollection<string> MatterTypeCodes,
    IReadOnlyCollection<string> Jurisdictions,
    IReadOnlyCollection<string> Postures)
{
    // ── Option lists for the structured metadata dropdowns (DB-backed, tenant-scoped). ──
    public IReadOnlyCollection<string> Subtypes { get; init; } = [];
    public IReadOnlyCollection<string> CourtSystems { get; init; } = [];
    public IReadOnlyCollection<string> States { get; init; } = [];
    public IReadOnlyCollection<string> CourtLevels { get; init; } = [];
    public IReadOnlyCollection<string> GoverningLaws { get; init; } = [];
}

// A decision-history timeline event (sourced from Legal_DecisionEvent for a session).
public sealed record DecisionTimelineEventDto(
    Guid DecisionEventId,
    int Sequence,
    string EventType,
    string StageCode,
    string? PayloadJson,
    DateTime CreatedDateUtc);

// External support classes (IP-protecting translation of raw composite scores).
public static class DecisionSupport
{
    public const string StronglySupported = "STRONGLY SUPPORTED";
    public const string Viable = "VIABLE";
    public const string Weak = "WEAK";

    // Deterministic mapping from a composite score to an attorney-facing support class.
    public static string Classify(decimal compositeScore) => compositeScore switch
    {
        >= 0.66m => StronglySupported,
        >= 0.40m => Viable,
        _ => Weak
    };
}

// Async start+poll transport (mirrors the Wide operation store contracts).
public sealed record DecisionSearchOperationStartResponse(Guid OperationId);

public sealed record DecisionSearchOperationStatusResponse(Guid OperationId, string StatusCode, DecisionSearchResponse? Response, string? ErrorMessage);

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal V2 — Dependency-Aware Decision Verification contracts (typed legal graph layer)
// ─────────────────────────────────────────────────────────────────────────────────────────────

// Node kinds and edge relations mirror the DB-backed graph (0213). Kept as constants so Core, the
// repository, and the UI share a single vocabulary without hardcoding scattered string literals.
public static class DecisionGraphNodeKinds
{
    public const string Evidence = "EVIDENCE";
    public const string Fact = "FACT";
    public const string Proposition = "PROPOSITION";
    public const string Element = "ELEMENT";
    public const string Burden = "BURDEN";
    public const string Procedure = "PROCEDURE";
    public const string Strategy = "STRATEGY";
    public const string Candidate = "CANDIDATE";
    public const string Branch = "BRANCH";
}

public static class DecisionGraphRelations
{
    public const string Supports = "SUPPORTS";
    public const string Requires = "REQUIRES";
    public const string Satisfies = "SATISFIES";
    public const string Establishes = "ESTABLISHES";
    public const string DependsOn = "DEPENDS_ON";
    public const string Contradicts = "CONTRADICTS";
}

public static class DecisionVerificationStates
{
    public const string Unverified = "UNVERIFIED";
    public const string Verified = "VERIFIED";
    public const string Invalidated = "INVALIDATED";
}

// A typed graph node surfaced to the cockpit (kind identifies which table it came from).
public sealed record DecisionGraphNodeDto(
    Guid NodeId,
    string NodeKind,
    string NodeCode,
    string DisplayName,
    string? Statement,
    decimal Support,
    bool IsEssential,
    bool IsSatisfied,
    string VerificationStatus,
    int SortOrder);

// A typed dependency edge; the unit the independent verifier marks VERIFIED/INVALIDATED.
public sealed record DecisionGraphEdgeDto(
    Guid EdgeId,
    string RelationCode,
    string SourceNodeKind,
    Guid SourceNodeId,
    string TargetNodeKind,
    Guid TargetNodeId,
    decimal SupportWeight,
    decimal Materiality,
    bool IsEssential,
    bool IsDispositive,
    string VerificationStatus,
    string? VerificationNotes,
    string? PropagatedStateCode);

// The strongest-losing-side adversarial test result (mandatory gate before DECISION_READY).
public sealed record DecisionLosingSideTestDto(
    Guid? ChallengerCandidateId,
    string? StrongestCaseSummary,
    decimal ChallengerStrength,
    decimal WinnerStrength,
    bool WinnerSurvived);

// The dependency-constrained readiness verdict (replaces score-only readiness in V2).
public sealed record DecisionReadinessVerdictDto(
    bool Satisfied,
    IReadOnlyCollection<string> Blockers,
    IReadOnlyCollection<DecisionReadinessItemDto> Predicate);
