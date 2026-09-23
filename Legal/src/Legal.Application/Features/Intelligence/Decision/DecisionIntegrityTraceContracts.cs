namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — Decision Integrity Trace (cockpit projection).
//
// This is a PROJECTION of authoritative decision state that already exists — never a second source of
// truth. It answers one question in a few seconds: did POLOXI move from a valid proposal, through
// research and verification, to a decision-safe output, and is its internal state coherent?
//
// The trace deliberately separates THREE independent concepts:
//   • System Integrity  — did the machinery execute soundly? (HEALTHY / ATTENTION / FAILED)
//   • Decision Readiness — is there enough verified support to decide? (may be NOT READY on a HEALTHY run)
//   • Output Integrity   — is the emitted answer free of unauthorized assertions? (CLEAN / ATTENTION)
//
// A perfectly-functioning POLOXI run can legitimately show HEALTHY + NOT READY: the system worked and
// correctly determined there isn't enough verified support yet. NOT READY must never look like a fault.
// ────────────────────────────────────────────────────────────────────────────────────────────────

// The small shared status vocabulary for every one of the nine stages. UI maps these to glyphs
// (✓ ◐ ● ✕ ↶ – ○) with the text carrying the meaning (never color alone).
public enum IntegrityTraceStatus
{
    NotRun,     // – stage did not execute in this run
    Running,    // ● stage is currently executing
    Passed,     // ✓ stage completed soundly
    Partial,    // ◐ stage completed with unresolved/partial results (legitimate, not a fault)
    Failed,     // ✕ stage failed in a way that requires attention
    Skipped,    // ○ stage was intentionally bypassed
    RolledBack  // ↶ stage faulted before commit; prior authoritative state was preserved
}

// Overall SYSTEM integrity — deterministic, never a weighted score. Severity increases downward:
// Healthy < Incomplete < AttentionRequired < Failed. HEALTHY is reserved for runs where every required
// control executed (or was explicitly Skipped with a reason). INCOMPLETE means one or more required
// controls were not evaluated (NotRun / NOT OBSERVED) — the machinery did not fault, but coverage is not
// complete, so the run must not be vouched for as HEALTHY.
public enum IntegrityState
{
    Healthy,
    Incomplete,
    AttentionRequired,
    Failed
}

// Internal-state coherence across evidence ↔ graph ↔ candidates ↔ frontier ↔ output authorization.
// Distinct from DecisionReady: INVALID is a system-integrity problem, not ordinary decision uncertainty.
public enum StateConsistency
{
    Valid,
    Invalid,
    Unknown
}

// Decision-readiness summary shown alongside (but separate from) system integrity.
public enum TraceReadinessState
{
    Ready,
    Provisional,
    NotReady,
    Unknown
}

// Output-integrity summary shown alongside (but separate from) system integrity.
public enum OutputIntegrityState
{
    Clean,
    AttentionRequired,
    NotRun
}

// One key/value line inside an expanded stage (e.g. "Stop reason" → "MAX_ROUNDS"). Ordered for display.
public sealed record IntegrityStageDetailDto(string Label, string Value);

// A drill-down child row inside a stage (a research round, a verified evidence item, a transformed
// claim). Kept generic so the same shape serves every stage's expansion.
public sealed record IntegrityStageChildDto(
    string Title,
    IntegrityTraceStatus Status,
    string? Summary,
    IReadOnlyList<IntegrityStageDetailDto> Detail);

// A single trace stage: its status, a one-line compact summary, ordered expanded detail, and optional
// drillable children. Every value is sourced from the authoritative response — nothing is recomputed
// with new semantics here.
public sealed record IntegrityStageDto
{
    public required string Key { get; init; }          // stable id, e.g. "PROPOSAL"
    public required string Label { get; init; }        // display, e.g. "Proposal"
    public required IntegrityTraceStatus Status { get; init; }
    public string? CompactSummary { get; init; }       // e.g. "ACCEPT · Attempt 1"
    public IReadOnlyList<IntegrityStageDetailDto> Detail { get; init; } = [];
    public IReadOnlyList<IntegrityStageChildDto> Children { get; init; } = [];
}

// Before/after decision movement summary for the compact panel (e.g. "C1 .65 → .58  LEADER CHANGED").
public sealed record DecisionMovementDto(
    string? PreviousLeaderLabel,
    string? CurrentLeaderLabel,
    bool LeaderChanged,
    decimal PreviousEntropy,
    decimal CurrentEntropy,
    decimal PreviousMargin,
    decimal CurrentMargin);

// Developer/admin-only diagnostics: correlation IDs + per-phase timing. Never shown in attorney mode.
public sealed record TraceDiagnosticsDto
{
    public Guid SessionId { get; init; }
    public string? CorrelationId { get; init; }
    public long DecisionStateVersion { get; init; }
    public long TotalDurationMs { get; init; }
    public IReadOnlyList<IntegrityStageDetailDto> PhaseTimings { get; init; } = [];
}

// The complete projected trace attached to the decision artifact.
public sealed record DecisionIntegrityTraceDto
{
    public required Guid SessionId { get; init; }

    public long DecisionStateVersion { get; init; }

    // The three independent top-level concepts.
    public IntegrityState Integrity { get; init; }
    public TraceReadinessState Readiness { get; init; }
    public OutputIntegrityState OutputIntegrity { get; init; }
    public StateConsistency Consistency { get; init; }

    // The nine ordered stages.
    public required IntegrityStageDto Proposal { get; init; }
    public required IntegrityStageDto Research { get; init; }
    public required IntegrityStageDto Verification { get; init; }
    public required IntegrityStageDto Promotion { get; init; }
    public required IntegrityStageDto Propagation { get; init; }
    public required IntegrityStageDto Recompetition { get; init; }
    public required IntegrityStageDto Frontier { get; init; }
    public required IntegrityStageDto OutputControl { get; init; }
    public required IntegrityStageDto FinalAudit { get; init; }

    // Compact-panel extras.
    public DecisionMovementDto? DecisionMovement { get; init; }
    public string? NextResearchTarget { get; init; }
    public string? ResearchStopReason { get; init; }

    // Developer-only.
    public TraceDiagnosticsDto? Diagnostics { get; init; }

    // Ordered enumeration of all nine stages for convenient rendering.
    public IReadOnlyList<IntegrityStageDto> Stages =>
    [
        Proposal, Research, Verification, Promotion, Propagation,
        Recompetition, Frontier, OutputControl, FinalAudit
    ];
}

// The proposal-integrity summary threaded out of the discovery gate for the Proposal stage.
public sealed record DecisionProposalIntegritySummaryDto(
    string Mode,               // SHADOW | ACTIVE
    string Disposition,        // ACCEPT | REPAIR | REGENERATE | CLARIFY | DEGRADED
    int Attempt,               // 1, or 2 when a recovery ran
    bool RecoveryAttempted,
    bool Recovered,
    bool StructurallyValid,
    IReadOnlyCollection<string> Defects);

// The research-loop summary threaded onto the response for the Research stage. Mirrors the audit
// surface of DecisionResearchLoopResultDto without re-running anything.
public sealed record DecisionResearchLoopSummaryDto(
    bool Enabled,
    int RoundsExecuted,
    int RoundsCommitted,
    int RoundsRolledBack,
    int TotalRetrievals,
    string StopReason,
    IReadOnlyCollection<DecisionResearchRoundDto> Rounds,
    DecisionResearchFailureDto? Failure = null);

// Research-loop ELIGIBILITY snapshot captured at the entry gate, BEFORE the loop runs. Unlike the
// summary (present only when the loop executed), this is always available and records exactly which
// prerequisite failed so the Research stage can say "NOT RUN · <reason>" instead of a generic message.
// The gate is: useGraph && v2 is not null && settings.Enabled && a frontier item ≥ MinFrontierIV.
public sealed record DecisionResearchEligibilityDto(
    bool EnabledSetting,   // Decision.ResearchLoop.Enabled as loaded at runtime (post-parse)
    bool UseGraph,         // request.UseDependencyGraph ?? v2Settings.UseDependencyGraphDefault
    bool V2Available,      // the dependency-graph result was produced this run
    bool Eligible,         // all prerequisites satisfied — the loop was entered
    string NotRunReason,   // DecisionResearchNotRunReasons.* — Eligible when the loop ran
    int FrontierCount = 0,                        // number of ACTIVE frontier branches this run
    decimal HighestFrontierInformationValue = 0m, // max IV across the frontier (0 when none)
    decimal MinFrontierInformationValue = 0m,     // configured MinFrontierInformationValue threshold
    bool RetrievalBudget = false);                // true when the retrieval budget allows ≥1 round

// Explicit, projectable reasons the bounded research loop did not execute. Exactly one applies.
// INVARIANT (§14): a NotRun research stage MUST carry a concrete reason — never a bare "no execution".
public static class DecisionResearchNotRunReasons
{
    public const string Eligible       = "ELIGIBLE";           // prerequisites met; the loop was entered
    public const string SettingDisabled = "SETTING_DISABLED";  // Decision.ResearchLoop.Enabled = false at runtime
    public const string GraphDisabled  = "GRAPH_DISABLED";     // useGraph = false (dependency graph toggle off)
    public const string V2Unavailable  = "V2_UNAVAILABLE";     // graph toggle on but no V2 result was produced
    public const string NoFrontier     = "NO_FRONTIER";        // no ACTIVE frontier branch to research
    public const string FrontierBelowThreshold = "FRONTIER_BELOW_THRESHOLD"; // frontier IV under MinFrontierIV
    public const string SettingsUnavailable = "SETTINGS_UNAVAILABLE"; // could not load loop settings this run
}
