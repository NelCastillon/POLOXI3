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

    // Matter jurisdiction / governing law (e.g. "California") threaded into evidence verification so
    // AuthorityVerifier can establish legal applicability of retrieved authorities. When a retrieved
    // source declares its own jurisdiction that takes precedence; this is the matter-level fallback so
    // authority applicability is not left AUTHORITY_JURISDICTION_NOT_ESTABLISHED. Null = no matter context.
    [StringLength(120)] public string? Jurisdiction { get; init; }

    // Practice-area Domain Pack (POLOXI.Legal_DecisionDomainPack.PackCode) that supplies domain
    // semantics (terminology, decision-hierarchy dimensions, evidence types, verification profiles)
    // to the decision. Advisory only: POLOXI Core reasoning is unchanged. Null = no domain pack.
    [StringLength(60)] public string? DomainPackCode { get; init; }
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
    int SortOrder)
{
    public string GenerationOriginCode { get; init; } = DecisionBranchGenerationOrigins.DynamicLlm;
    public Guid? DecisionDomainConceptId { get; init; }
    public string? DomainConceptCode { get; init; }
    public decimal? GuardrailMatchScore { get; init; }
    public string? GuardrailActionCode { get; init; }
    public int? GuardrailVersion { get; init; }
}

public static class DecisionBranchGenerationOrigins
{
    public const string DynamicLlm = "DYNAMIC_LLM";
    public const string DynamicLlmEnriched = "DYNAMIC_LLM_ENRICHED";
    public const string DomainFallback = "DOMAIN_FALLBACK";
}

public static class DecisionGuardrailActions
{
    public const string NovelAccepted = "NOVEL_ACCEPTED";
    public const string ConceptMatched = "CONCEPT_MATCHED";
    public const string ConstraintEnriched = "CONSTRAINT_ENRICHED";
    public const string FallbackAdded = "FALLBACK_ADDED";
}

public sealed record DecisionEvidenceDto(
    Guid DecisionEvidenceId,
    Guid? DecisionBranchId,
    string? SourceRef,
    string? SourceTitle,
    string? Snippet,
    decimal VerificationValue,
    string VerificationStatus)
{
    // Verification provenance (§14): traceable claim ↔ source ↔ passage ↔ verification chain.
    public string? SupportedObjective { get; init; }
    public string? SupportingPassage { get; init; }
    public string? LifecycleState { get; init; }
}

public sealed record DecisionEvidenceVerificationFactorDto(
    string FactorCode,
    string StateCode,
    string ReasonCode,
    string? Reason,
    string? VerifiedValue,
    string? SourceRef,
    string? SupportingPassage,
    string VerificationMethod);

public sealed record DecisionEvidenceVerificationDto(
    Guid DecisionEvidenceVerificationId,
    Guid DecisionEvidenceId,
    Guid? DecisionBranchId,
    string SourceTypeCode,
    string ProfileCode,
    string DispositionCode,
    bool IsVerified,
    bool IsDecisionAuthorized,
    IReadOnlyCollection<string> BlockingReasons,
    DateTime EvaluatedDateUtc,
    IReadOnlyCollection<DecisionEvidenceVerificationFactorDto> Factors)
{
    public int RetrievedCount { get; init; }
    public int PreScreenRejectedCount { get; init; }
    public int MechanicalVerificationCount { get; init; }
    public int SemanticVerificationCount { get; init; }
    public int PoloxiDeepeningCount { get; init; }
    public int CacheHitCount { get; init; }
    public int InputTokenCount { get; init; }
    public int OutputTokenCount { get; init; }
    public long LatencyMilliseconds { get; init; }
}

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

    // Audit-only summary of DB-backed Domain Pack guardrails applied to the dynamic hierarchy.
    // It never participates in candidate scoring, IV, frontier selection, or readiness.
    public DecisionDomainGuardrailSummaryDto? DomainGuardrails { get; init; }

    // ── POLOXI Legal V2 (dependency-aware) additions. Empty/null on the V1 path. ──
    public bool UsedDependencyGraph { get; init; }
    // When the graph was enabled but no typed graph was produced, this carries the deterministic
    // reason (e.g. "proposal returned no nodes", missing prompt, or parse failure) so the cockpit
    // can distinguish GraphEnabledButEmpty causes instead of showing a generic empty-graph notice.
    public string? GraphDiagnostic { get; init; }
    public IReadOnlyCollection<DecisionGraphNodeDto> GraphNodes { get; init; } = [];
    public IReadOnlyCollection<DecisionGraphEdgeDto> GraphEdges { get; init; } = [];
    public DecisionLosingSideTestDto? LosingSideTest { get; init; }
    public DecisionReadinessVerdictDto? ReadinessVerdict { get; init; }

    // ── POLOXI Legal V2.1 (closed-loop) additions. Empty/null unless the loop has run. ──
    public DecisionRecompetitionDto? LastRecompetition { get; init; }
    public DecisionResearchNeedDto? PendingResearchNeed { get; init; }

    // ── POLOXI Legal EA-7 (epistemic governance overlay). Null unless the bridge ran. Advisory by
    // default: annotation-only and never changes the verdicts above; all claims stay visible. ──
    public DecisionGovernanceVerdictDto? GovernanceVerdict { get; init; }

    // ── POLOXI Verified Decision Signals (advisory). Empty unless material support signals were
    // extracted/persisted for this session. Read-only projection for the cockpit; never scores. ──
    public IReadOnlyCollection<DecisionSupportSignalDto> VerifiedSignals { get; init; } = [];

    // Persisted factor-by-factor evidence verification diagnostics. Read-only and non-scoring.
    public IReadOnlyCollection<DecisionEvidenceVerificationDto> EvidenceVerifications { get; init; } = [];

    // ── POLOXI Legal B3 (Hallucination Solver) shadow/what-if snapshot. Populated when the solver
    // ran and recompeted the ranking on verified evidence. In ADVISORY mode this is a NON-DESTRUCTIVE
    // "what-if": the returned decision above is the original (B2) result, and this snapshot shows what
    // the ranking WOULD become if unsupported material support were removed. In ENFORCED mode the
    // returned decision equals this snapshot and IsAuthoritative is true. Null when the solver is off. ──
    public DecisionSolverShadowDto? SolverShadow { get; init; }

    // ── Decision Integrity Trace inputs + projection (cockpit). All additive/optional; they never
    // change the verdicts above. The projector reads these to build IntegrityTrace deterministically. ──

    // Proposal Integrity Gate summary (disposition/attempt/defects) surfaced from candidate discovery.
    public DecisionProposalIntegritySummaryDto? ProposalIntegrity { get; init; }

    // Bounded research-loop audit summary (rounds committed/rolled back, stop reason, per-round audit).
    public DecisionResearchLoopSummaryDto? ResearchSummary { get; init; }

    // Research-loop eligibility snapshot captured at the entry gate. Always present on a live run; lets
    // the Research stage report the exact NOT-RUN reason (setting disabled / graph disabled / V2 absent).
    public DecisionResearchEligibilityDto? ResearchEligibility { get; init; }

    // Per-claim output authorizations (ALLOW/QUALIFY/SUPPRESS/CORRECT) from the output-claim audit.
    public IReadOnlyCollection<DecisionOutputAuthorizationDto> OutputAuthorizations { get; init; } = [];

    // Aggregate output prose-transform outcome (required/applied counts + post-transform cleanliness).
    // Null when no enforcement pass ran. Distinguishes "transformation required" from "actually applied".
    public DecisionOutputTransformSummaryDto? OutputTransformSummary { get; init; }

    // Safe diagnostics for the composed-answer claim extraction boundary. No answer text or provider
    // payload is exposed; this records only execution state, input length, count, and a safe reason.
    public DecisionOutputClaimExtractionDto? OutputClaimExtraction { get; init; }

    // Deterministically-derived decision state version (1 + committed authoritative mutations).
    public long DecisionStateVersion { get; init; } = 1;

    // Per-phase execution timings for the Diagnostics view (label → milliseconds).
    public IReadOnlyCollection<DecisionPhaseTimingDto> PhaseTimings { get; init; } = [];

    // The projected Decision Integrity Trace. Null only on legacy paths that don't project it.
    public DecisionIntegrityTraceDto? IntegrityTrace { get; init; }
}

public sealed record DecisionDomainGuardrailSummaryDto(
    int DynamicBranchCount,
    int EnrichedBranchCount,
    int NovelBranchCount,
    int DormantFallbackCount,
    IReadOnlyCollection<string> AppliedConceptCodes,
    string PolicyCode);

public sealed record DecisionOutputClaimExtractionDto(
    bool Attempted,
    int AnswerLength,
    bool SubstantiveAnswer,
    int ClaimsReturned,
    string StatusCode,
    string? FailureReason);

// One per-claim output authorization surfaced to the cockpit trace (§34/§35). A namespace-local mirror
// of the epistemic OutputClaimAuthorization so the Decision contracts stay decoupled from Epistemic.
public sealed record DecisionOutputAuthorizationDto(
    Guid ClaimId,
    string ClaimText,
    string VerificationState,
    string DecisionAuthority,
    string Disposition,       // ALLOW | QUALIFY | SUPPRESS | CORRECT
    bool IsForeign,
    string Reason,
    bool ProseTransformed)
{
    public Guid? SourceBranchId { get; init; }
    public Guid? SourceCandidateId { get; init; }
    public Guid? DecisionEvidenceId { get; init; }
    public Guid? DecisionEvidenceAttachmentId { get; init; }
    public Guid? DecisionEvidenceVerificationId { get; init; }
    public Guid? SourceSnapshotId { get; init; }
    public string? PassageRef { get; init; }
    public bool IsMaterial { get; init; } = true;
    public string MappingState { get; init; } = "UNMAPPED";
    public Guid? SourcePropositionId { get; init; }
    public string? MappingReasonCode { get; init; }
}

// Aggregate outcome of the output prose-transform pass. RequiredCount is how many claims needed the
// prose rewritten; AppliedCount is how many were actually located and rewritten in the answer body. When
// AppliedCount < RequiredCount, unauthorized assertions may still stand verbatim (post-transform audit
// is NOT clean) even though every claim carries a disposition.
public sealed record DecisionOutputTransformSummaryDto(
    int RequiredCount,
    int AppliedCount,
    bool PostTransformClean)
{
    public int UnauthorizedAssertionsRemaining => Math.Max(0, RequiredCount - AppliedCount);
}

// One per-phase execution timing for the Diagnostics view.
public sealed record DecisionPhaseTimingDto(string Phase, long Milliseconds);

// The B3 solver's recompeted "what-if" ranking, kept alongside the original decision so the cockpit
// can show BOTH the baseline and the verified-evidence-only recompetition. The candidate/branch
// collections are the recompeted state; the scalar fields summarize the before/after transition.
public sealed record DecisionSolverShadowDto(
    string ModeCode,                    // Advisory | Enforced (Off never emits a shadow)
    bool IsAuthoritative,               // true only in Enforced mode (the returned decision equals this)
    string StatusCode,
    string? TerminalStateCode,
    Guid? PreviousWinnerCandidateId,
    Guid? CurrentWinnerCandidateId,
    bool WinnerChanged,
    decimal PreviousEntropy,
    decimal CurrentEntropy,
    decimal PreviousMargin,
    decimal CurrentMargin,
    int AppliedDeltaCount,
    int ReopenedBranchCount,
    IReadOnlyCollection<DecisionCandidateDto> Candidates,
    IReadOnlyCollection<DecisionBranchDto> Branches);

// A persisted material support signal ("this fact/authority, if verified, moves the outcome by this
// much"), surfaced read-only in the cockpit. Advisory: it annotates, it never rescoring the verdict.
public sealed record DecisionSupportSignalDto(
    Guid SignalId,
    string Statement,
    string OriginCode,
    string VerificationStateCode,
    bool RequiresVerification,
    decimal VerificationStrength,
    decimal DecisionImpact,
    Guid? SourceBranchId,
    Guid? SourceCandidateId,
    string? VerificationReason);

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
    // ── Practice-area classification (Domain Pack scoping, e.g. PERSONAL_INJURY). ──
    public string? PracticeAreaCode { get; init; }
    public string? ClaimTypeCode { get; init; }
    public string? DomainPackCode { get; init; }

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
    // ── Practice-area classification (Domain Pack scoping, e.g. PERSONAL_INJURY). ──
    [StringLength(60)] public string? PracticeAreaCode { get; init; }
    [StringLength(120)] public string? ClaimTypeCode { get; init; }
    [StringLength(60)] public string? DomainPackCode { get; init; }

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
    // ── Practice-area classification (Domain Pack scoping, e.g. PERSONAL_INJURY). ──
    [StringLength(60)] public string? PracticeAreaCode { get; init; }
    [StringLength(120)] public string? ClaimTypeCode { get; init; }
    [StringLength(60)] public string? DomainPackCode { get; init; }

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

// ── Domain Pack (practice-area domain semantics) DTOs
// A database-backed Domain Pack supplies terminology, decision-hierarchy dimensions, evidence
// classifications, verification profiles, and matter-type taxonomy for a practice area. POLOXI Core
// owns all decision reasoning; the pack is advisory domain configuration only.
public sealed record DecisionDomainPackDto(
    Guid DecisionDomainPackId,
    string PackCode,
    string PracticeAreaCode,
    string Name,
    string? Description,
    IReadOnlyCollection<DecisionDomainPackDimensionDto> Dimensions,
    IReadOnlyCollection<DecisionDomainPackEvidenceTypeDto> EvidenceTypes,
    IReadOnlyCollection<DecisionDomainPackVerificationProfileDto> VerificationProfiles,
    IReadOnlyCollection<DecisionDomainPackMatterTypeDto> MatterTypes)
{
    public IReadOnlyCollection<DecisionDomainConceptDto> Concepts { get; init; } = [];
    public IReadOnlyCollection<DecisionDomainConceptRelationDto> ConceptRelations { get; init; } = [];
}

public sealed record DecisionDomainPackDimensionDto(string DimensionCode, string Name, string? Description);

public sealed record DecisionDomainPackEvidenceTypeDto(string EvidenceTypeCode, string Name, string? DimensionCode, string? Description);

public sealed record DecisionDomainPackVerificationProfileDto(string ProfileCode, string Name, string? EvidenceTypeCode, string? Description);

public sealed record DecisionDomainPackMatterTypeDto(string MatterTypeCode, string Name, string? Description);

public sealed record DecisionDomainConceptDto(
    Guid DecisionDomainConceptId,
    string ConceptCode,
    string DimensionCode,
    string Name,
    string? Description,
    string ConceptKindCode,
    string SourceClassCode,
    string? VerificationProfileCode,
    string? JurisdictionCode,
    string? MatterTypeCode,
    bool IsRequiredCoverage,
    bool IsFallbackEligible,
    int SortOrder,
    int VersionNumber);

public sealed record DecisionDomainConceptRelationDto(
    Guid DecisionDomainConceptRelationId,
    string SourceConceptCode,
    string TargetConceptCode,
    string RelationTypeCode,
    string? ConstraintCode,
    string? Description,
    string? JurisdictionCode,
    string? MatterTypeCode,
    bool IsHardConstraint,
    int SortOrder);

// Well-known Domain Pack codes.
public static class DecisionDomainPackCodes
{
    public const string PersonalInjury = "PERSONAL_INJURY";
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

    // ── Practice-area classification option lists (DB-backed, e.g. Personal Injury). ──
    public IReadOnlyCollection<string> PracticeAreas { get; init; } = [];
    public IReadOnlyCollection<string> PiMatterTypes { get; init; } = [];
    public IReadOnlyCollection<string> PiClaimTypes { get; init; } = [];
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

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Explicit evidence verification LIFECYCLE (§14). Replaces the old multiplicative authority gate
// (identity × citation × holding × weight × support) with an ordered ladder of discrete, auditable
// stages. A source advances one rung at a time; the FIRST failed rung fixes a terminal failure state.
// Only the fully-climbed VERIFIED state grants positive decision authority — every partial/failure
// state contributes ZERO positive support (it may still contradict). This makes "why isn't this
// evidence trusted yet?" answerable by pointing at the exact rung that did not clear.
public static class DecisionEvidenceLifecycleStates
{
    // Progress ladder (in ascending order of establishment).
    public const string Retrieved = "RETRIEVED";                              // a source came back
    public const string IdentityVerified = "IDENTITY_VERIFIED";              // the source is who it claims to be
    public const string CitationVerified = "CITATION_VERIFIED";             // a resolvable citation/reference exists
    public const string PassageLocated = "PASSAGE_LOCATED";                 // a usable passage was found in the source
    public const string PropositionSupportVerified = "PROPOSITION_SUPPORT_VERIFIED"; // passage supports the objective
    public const string HoldingVerified = "HOLDING_VERIFIED";              // (where required) the holding is on point
    public const string AuthorityValidated = "AUTHORITY_VALIDATED";        // (where required) the authority is controlling
    public const string Verified = "VERIFIED";                            // fully established — positive authority

    // Terminal failure states — none grant positive authority.
    public const string PartiallySupported = "PARTIALLY_SUPPORTED";        // some support, below the required bar
    public const string Contradicted = "CONTRADICTED";                    // the source cuts against the objective
    public const string Unsupported = "UNSUPPORTED";                      // no support relationship established
    public const string Unverifiable = "UNVERIFIABLE";                   // a required rung could not be evaluated
    public const string RetrievalFailed = "RETRIEVAL_FAILED";           // retrieval itself failed
    public const string VerificationFailed = "VERIFICATION_FAILED";     // verification errored out

    // Only a fully-climbed VERIFIED lifecycle grants positive decision authority.
    public static bool GrantsPositiveAuthority(string? lifecycleState) =>
        string.Equals(lifecycleState, Verified, StringComparison.OrdinalIgnoreCase);

    // Map a granular lifecycle state onto the persisted status vocabulary (schema-compatible):
    // VERIFIED stays VERIFIED; CONTRADICTED maps to INVALIDATED; everything else is UNVERIFIED.
    public static string ToPersistedStatus(string? lifecycleState) => lifecycleState switch
    {
        Verified => DecisionVerificationStates.Verified,
        Contradicted => DecisionVerificationStates.Invalidated,
        _ => DecisionVerificationStates.Unverified,
    };
}

// Explicit research/retrieval outcome (§13). Retrieval failure must become explicit decision state
// rather than disappearing into logs, so the pipeline can distinguish "we searched and found nothing"
// from "our retrieval operation failed" from "no research was needed".
public static class DecisionResearchStates
{
    public const string NotNeeded = "NOT_NEEDED";
    public const string RequiredPending = "REQUIRED_PENDING";
    public const string Retrieved = "RETRIEVED";
    public const string SearchNoResults = "SEARCH_NO_RESULTS";
    public const string RetrievalFailed = "RETRIEVAL_FAILED";
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

// ── POLOXI Legal EA-7 (epistemic governance overlay) additions ──────────────────────────────────
// A single involved claim surfaced for UI annotation. ALL claims (authorized + unauthorized) are kept
// visible for reference; unauthorized ones carry the reason they cannot be relied upon.
public sealed record GovernanceClaimDto(
    Guid ClaimId,
    string Text,
    string VerificationStateCode,
    string DecisionAuthorityCode,
    bool IsAuthorized,
    bool IsEssential,
    string Annotation);

// The non-destructive governance verdict. The authoritative V2 verdict and every claim remain intact
// and visible; this record annotates them and (only in SoftGate/HardGate) reports a downgraded
// effective readiness. In Advisory mode OverrideApplied is always false.
public sealed record DecisionGovernanceVerdictDto(
    string OverrideMode,
    bool V2ReadinessSatisfied,
    bool EaReady,
    bool OutputClean,
    bool EffectiveReadinessSatisfied,
    bool OverrideApplied,
    int ProjectedClaimCount,
    int AuthorizedClaimCount,
    IReadOnlyCollection<string> Blockers,
    IReadOnlyCollection<string> OutputViolations,
    IReadOnlyCollection<GovernanceClaimDto> InvolvedClaims,
    IReadOnlyCollection<string> Narrative);
