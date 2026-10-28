using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

    // Execution mode (DEV Logic / PROD Logic). Resolved once per NEW session into an immutable snapshot
    // that selects the default model deployment, replay permission, and environment restriction. A
    // clarification continuation inherits the parent session's mode. Defaults to Dev.
    public DecisionExecutionMode Mode { get; init; } = DecisionExecutionMode.Dev;

    // Opt-in deterministic AI response replay (DEV only). Honored only when the resolved mode allows
    // replay; PROD always executes live regardless of this flag.
    public bool EnableReplay { get; init; }

    // Model selection: null/empty = Auto (highest-priority active DB route); otherwise a specific ModelCode.
    [StringLength(100)] public string? ModelCode { get; init; }

    // Context (POLOXI.Legal_DecisionContext.ContextCode): null/empty/GENERAL default; LEGAL grounds in law.
    [StringLength(50)] public string? ContextCode { get; init; }

    // Continuation of a session that ended USER_CLARIFICATION_REQUIRED (§7 clarification loop).
    public Guid? ParentDecisionSessionId { get; init; }
    [StringLength(2000)] public string? ClarificationAnswer { get; init; }
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
    [StringLength(120)] public string? GoverningLaw { get; init; }
    [StringLength(300)] public string? CourtOrForum { get; init; }
    public DateOnly? AuthorityCutoffDate { get; init; }

    // Practice-area Domain Pack (POLOXI.Legal_DecisionDomainPack.PackCode) that supplies domain
    // semantics (terminology, decision-hierarchy dimensions, evidence types, verification profiles)
    // to the decision. Advisory only: POLOXI Core reasoning is unchanged. Null = no domain pack.
    [StringLength(60)] public string? DomainPackCode { get; init; }

    // Discovery-prompt assembly mode. Original (default) keeps the existing discovery behavior byte-for-byte.
    // DomainPack assembles a separate discovery prompt from the exact stored original prompt plus the Domain
    // Pack mode instructions and the selected pack's structured configuration. In DomainPack mode the selected
    // DomainPackCode MUST resolve to a valid authorized pack; otherwise the decision fails with a clear
    // validation error (no silent fallback). POLOXI Core, output schema, normalization, and canonical outcome
    // injection are unchanged in both modes.
    public DecisionDiscoveryMode DiscoveryMode { get; init; } = DecisionDiscoveryMode.Original;

    // First-class, immutable Personal Injury Profile projection. When present it is projected into the
    // discovery proposal as a distinct STRUCTURED context section (not buried in free text) so Astra can
    // identify incident-specific legal/factual dependencies. Supplied allegations, NOT verified evidence.
    public PersonalInjuryProfileSnapshot? PersonalInjuryProfile { get; init; }
}

// Immutable structured projection of the database-backed Personal Injury Profile fields, threaded onto
// the Decision Contract so the proposal stage receives the same PI facts at every stage. These are
// SUPPLIED ALLEGATIONS/CONTEXT, not independently verified evidence; they identify propositions that
// still need investigation. Uses existing DB-backed fields; no new profile structure is invented.
public sealed record PersonalInjuryProfileSnapshot(
    string? IncidentTypeCode,
    DateOnly? IncidentDate,
    string? IncidentState,
    string? LiabilitySummary,
    string? InjurySummary,
    string? DamagesSummary)
{
    public string? IncidentCounty { get; init; }
    public string? IncidentCity { get; init; }
    public string? IncidentLocation { get; init; }
    public string? IncidentSummary { get; init; }
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
    bool IsEliminated)
{
    // Branch-first (DECISION_DISCOVERY_V2) advisory proposal-stage score in [0,1] Astra proposed and
    // POLOXI Core consumes for further reasoning. Advisory only; Core owns the authoritative
    // CompositeScore/verdict above. Null on the legacy candidate-first path or when omitted.
    public decimal? ProposedScore { get; init; }

    // R1 Outcome Proposal Hierarchy: the originating outcome-node codes (e.g. "O1", "O1.1") this
    // candidate was normalized from. DISCOVERY provenance ONLY — it links a competed candidate back to
    // the interpretation nodes that produced it and NEVER contributes evidentiary support to scoring.
    // Empty on the legacy candidate-first path or when no outcome hierarchy was proposed.
    public IReadOnlyCollection<string> OriginatingOutcomeNodeIds { get; init; } = [];
}

// ── R1 Outcome Proposal Hierarchy (DECISION_DISCOVERY §2). A first-class DISCOVERY structure of
//    materially-distinct outcome interpretations, kept separate from the Section 3 shared legal /
//    factual dependency (evaluation) hierarchy. Outcome nodes normalize into the global candidate pool
//    (traced via DecisionCandidateDto.OriginatingOutcomeNodeIds) but are NEVER independent evidence and
//    are never double-counted in authoritative scoring. Empty on the legacy candidate-first path. ──
public sealed record OutcomeProposalHierarchyDto(
    IReadOnlyCollection<OutcomeProposalNodeDto> Nodes);

public sealed record OutcomeProposalNodeDto(
    Guid DecisionOutcomeNodeId,
    string OutcomeNodeId,
    string? ParentOutcomeNodeId,
    int Level,
    string Title,
    string? Description,
    string? DistinguishingProposition);

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

// ── Branch-first (DECISION_DISCOVERY_V2) enrichment projections (§4,§5,§6). Descriptive only; POLOXI
//    Core still owns all scoring. Empty on the legacy candidate-first path. ──

// An explicit Candidate × Branch semantic role assertion (required / supporting / opposing /
// conditional / distinguishing / non_applicable) against the shared branch forest.
public sealed record DecisionCandidateBranchRelationDto(
    Guid DecisionCandidateBranchRelationId,
    Guid DecisionCandidateId,
    Guid DecisionBranchId,
    string RelationTypeCode,
    string? Rationale);

// A dependency-graph node projected from the enriched discovery: unresolved propositions (with the
// evidence / legal authority needed to resolve them) and supplied vs verified fact provenance.
public sealed record DecisionDependencyDto(
    Guid DecisionDependencyId,
    Guid? DecisionCandidateId,
    string NodeKind,
    string Statement,
    string ProvenanceCode,
    bool IsEssential,
    bool IsVerified,
    string? EvidenceNeeded,
    string? AuthorityNeeded,
    string? LinkedBranchCode,
    string? FailureCode);

// §1 First-class DecisionIntent projection: the specific decision Astra proposed and its scope.
// Descriptive proposal-stage content; POLOXI Core validates it before candidate discovery and
// hierarchy registration. Null on the legacy candidate-first path or when no intent was proposed.
public sealed record DecisionIntentDto(
    Guid DecisionIntentId,
    string? DecisionTarget,
    string? DecisionType,
    string? RequestedDisposition,
    string? CurrentOutcome,
    string? DecisionScope,
    string? TimeHorizon,
    string? ProceduralStage,
    string? UserConstraints,
    string? MaterialAmbiguity);

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
    int RankDelta)
{
    public Guid? TargetCandidateId { get; init; }
    public string? TargetCandidateCode { get; init; }
    public string PolarityCode { get; init; } = "UNRESOLVED";
}

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

    // ── Branch-first (DECISION_DISCOVERY_V2) enrichment (§4,§5,§6). Descriptive read-only projections;
    //    POLOXI Core still owns all scoring. Empty on the legacy candidate-first discovery path. ──
    public IReadOnlyCollection<DecisionCandidateBranchRelationDto> CandidateBranchRelations { get; init; } = [];
    public IReadOnlyCollection<DecisionDependencyDto> Dependencies { get; init; } = [];

    // §1 First-class DecisionIntent (the specific decision + its scope) proposed by Astra. Null on the
    // legacy candidate-first path or when no intent was proposed. Descriptive; Core still validates it.
    public DecisionIntentDto? DecisionIntent { get; init; }

    // R1 Outcome Proposal Hierarchy: the first-class DISCOVERY structure of materially-distinct outcome
    // interpretations proposed for LEGAL_DECISION / EVALUATE. Separate from the shared dependency
    // (evaluation) hierarchy; normalized candidates trace back via OriginatingOutcomeNodeIds. Null on
    // the legacy path, for non-EVALUATE modes, or when no outcome hierarchy was proposed.
    public OutcomeProposalHierarchyDto? OutcomeProposalHierarchy { get; init; }

    // ── POLOXI Legal B3 (Hallucination Solver) shadow/what-if snapshot.
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

// Broad six-factor Matter Type lookup row (migration 0458, Legal_MatterType).
// Code is the stable machine identifier (PERSONAL_INJURY); Name is the display label.
public sealed record MatterTypeDto(Guid MatterTypeId, string Code, string Name, string? Description, int SortOrder);

// Canonical MatterType -> Subtype catalog row (migration 0460, Legal_MatterSubtype). A broad matter
// type owns its subtype set directly; domain packs merely LINK to these rows. Code is the stable
// subtype machine identifier (MOTOR_VEHICLE_ACCIDENT); Name is the display label.
public sealed record MatterSubtypeDto(Guid MatterSubtypeId, Guid MatterTypeId, string MatterTypeCode, string Code, string Name, string? Description, int SortOrder);

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
    public string? SubjectMatterJurisdiction { get; init; }
    public string? PersonalTerritorialJurisdiction { get; init; }
    public string? ProceduralLaw { get; init; }
    public DateOnly? AuthorityCutoffDate { get; init; }

    // ── Structured Posture dimension (legacy Posture + these). ──
    public string? MovingParty { get; init; }
    public string? RespondingParty { get; init; }
    public string? MotionTarget { get; init; }
    public string? RequestedDisposition { get; init; }

    // User-supplied free-text narrative persisted BEFORE any LLM prompt executes for this matter.
    // SUPPLIED context, never verified evidence.
    public string? AdditionalNarrative { get; init; }

    // Per-matter Blended Score scope-gate override (decimal 0-1). When set, it is used as the
    // MatterScopeOutThreshold for THIS matter's runs; null falls back to the tenant/platform default.
    public decimal? MatterScopeOutThreshold { get; init; }

    // Migration 0461 (canonical): authoritative binding to the canonical subtype catalog
    // (Legal_MatterSubtype). Null = no deterministic matter-type filtering for this matter.
    public Guid? MatterSubtypeId { get; init; }
    // Resolved subtype Code of the bound canonical row (read-model convenience; null when unbound).
    public string? DomainPackMatterTypeCode { get; init; }
    // Display Name of the bound canonical row (for UI and LLM prompt context; null when unbound).
    public string? DomainPackMatterTypeName { get; init; }

    // Migration 0458: broad Matter Type lookup binding (Legal_MatterType). MatterTypeCode holds the
    // machine code; MatterTypeName is the display label projected from the lookup for UI/LLM use.
    public Guid? MatterTypeId { get; init; }
    public string? MatterTypeName { get; init; }

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

    // Migration 0461: canonical subtype code. Resolved server-side to
    // Legal_DecisionMatter.MatterSubtypeId within the canonical Legal_MatterSubtype catalog.
    // Null or unknown code leaves the FK NULL = no deterministic matter-type filtering (fail-open).
    [StringLength(120)] public string? DomainPackMatterTypeCode { get; init; }

    // ── Structured Jurisdiction dimension. ──
    [StringLength(120)] public string? CourtSystem { get; init; }
    [StringLength(120)] public string? State { get; init; }
    [StringLength(120)] public string? CourtLevel { get; init; }
    [StringLength(120)] public string? County { get; init; }
    [StringLength(120)] public string? GoverningLaw { get; init; }
    [StringLength(300)] public string? SubjectMatterJurisdiction { get; init; }
    [StringLength(300)] public string? PersonalTerritorialJurisdiction { get; init; }
    [StringLength(300)] public string? ProceduralLaw { get; init; }
    public DateOnly? AuthorityCutoffDate { get; init; }

    // ── Structured Posture dimension. ──
    [StringLength(200)] public string? MovingParty { get; init; }
    [StringLength(200)] public string? RespondingParty { get; init; }
    [StringLength(300)] public string? MotionTarget { get; init; }
    [StringLength(300)] public string? RequestedDisposition { get; init; }

    [StringLength(20000)] public string? AdditionalNarrative { get; init; }
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

    // Migration 0455: authoritative Domain Pack matter-type code (see DecisionMatterCreateRequest).
    [StringLength(120)] public string? DomainPackMatterTypeCode { get; init; }

    // ── Structured Jurisdiction dimension. ──
    [StringLength(120)] public string? CourtSystem { get; init; }
    [StringLength(120)] public string? State { get; init; }
    [StringLength(120)] public string? CourtLevel { get; init; }
    [StringLength(120)] public string? County { get; init; }
    [StringLength(120)] public string? GoverningLaw { get; init; }
    [StringLength(300)] public string? SubjectMatterJurisdiction { get; init; }
    [StringLength(300)] public string? PersonalTerritorialJurisdiction { get; init; }
    [StringLength(300)] public string? ProceduralLaw { get; init; }
    public DateOnly? AuthorityCutoffDate { get; init; }

    // ── Structured Posture dimension. ──
    [StringLength(200)] public string? MovingParty { get; init; }
    [StringLength(200)] public string? RespondingParty { get; init; }
    [StringLength(300)] public string? MotionTarget { get; init; }
    [StringLength(300)] public string? RequestedDisposition { get; init; }

    [StringLength(20000)] public string? AdditionalNarrative { get; init; }
}

// Focused request that persists ONLY the user's Additional Narrative on a matter.
// cockpits to save the submitted prompt text before the LLM pipeline runs, without replaying every
// other matter field.
public sealed record DecisionMatterAdditionalNarrativeRequest(
    [StringLength(20000)] string? AdditionalNarrative);

// Focused request that persists ONLY the per-matter Blended Score scope-gate threshold. Used by the
// Decision2 dial to remember the attorney's last value PER MATTER without replaying other fields.
public sealed record DecisionMatterScopeThresholdRequest(
    [Range(0, 1)] decimal? MatterScopeOutThreshold);

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
    // Migration 0458: broad Matter Type code (Legal_MatterType.Code) this pack serves; drives the
    // Matter type -> Subtype UI cascade. Null = pack not mapped to a broad type.
    public string? MatterTypeCode { get; init; }
    public IReadOnlyCollection<DecisionDomainConceptDto> Concepts { get; init; } = [];
    public IReadOnlyCollection<DecisionDomainConceptRelationDto> ConceptRelations { get; init; } = [];

    // Canonical Decision Outcome candidates (migration 0396). These are the first-class competing
    // final resolutions/statuses for the practice area (e.g. PI C1–C5). They are injected into the
    // candidate universe so the Decision Outcome cards are never starved. The determining FACTORS
    // (liability, causation, comparative fault, damages, …) remain shared evaluation-hierarchy nodes
    // and are NOT part of this set.
    public IReadOnlyCollection<DecisionDomainPackOutcomeCandidateDto> OutcomeCandidates { get; init; } = [];

    // Conditional deterministic relevance rules (migration 0433). A rule EXCLUDEs or PRIORITIZEs a
    // Concept/EvidenceType only when the named matter signal is present; pack membership alone never
    // excludes. Advisory ordering/scope configuration only; POLOXI Core owns scoring.
    public IReadOnlyCollection<DecisionDomainPackRelevanceRuleDto> RelevanceRules { get; init; } = [];

    // Incident families (migration 0445). A matter resolves to zero or more families by matching
    // MatterSignalPatterns against its matter-type/subtype/claim-type/profile incident type; Evidence Types
    // tagged with IncidentFamilyCodes are deterministically excluded when the matter resolves to at least one
    // family and none of them is tagged. Untagged Evidence Types apply to every family.
    public IReadOnlyCollection<DecisionDomainPackIncidentFamilyDto> IncidentFamilies { get; init; } = [];

    // Decision contracts (migration 0451). Each contract is an INDEPENDENT decision landscape with its own
    // competing candidate set and its own relative-support denominator (e.g. PROCEDURAL_DISPOSITION vs
    // MATTER_RESOLUTION). Outcome candidates carry DecisionContractCode membership; candidates without a
    // contract (legacy C1-C9) belong to the default/global landscape. Advisory configuration only.
    public IReadOnlyCollection<DecisionDomainPackDecisionContractDto> DecisionContracts { get; init; } = [];

    // Directional candidate x dimension vectors (migration 0451). A row declares how a dimension's strength
    // influences an outcome: SUPPORTS raises it, OPPOSES lowers it, NEUTRAL is tension/irrelevant, UNKNOWN is
    // explicitly unmodeled (never treated as negative). Mirror outcomes carry opposite directions on shared
    // element axes so the same proposition cannot inflate contradictory dispositions. Advisory only.
    public IReadOnlyCollection<DecisionDomainPackOutcomeCandidateDimensionDto> OutcomeCandidateDimensions { get; init; } = [];

    // Essential/corroborating evidence requirements per outcome (migration 0451). Separates scored coverage
    // from verified-evidence coverage and decision readiness; a missing requirement is a readiness GAP, never
    // negative evidence. MinVerificationProfileCode names the verification profile that satisfies it.
    public IReadOnlyCollection<DecisionDomainPackOutcomeCandidateEvidenceRequirementDto> OutcomeCandidateEvidenceRequirements { get; init; } = [];
}

// Migration 0445: MatterSignalPatterns is a comma-separated list of SQL-LIKE style patterns ('%' wildcard).
public sealed record DecisionDomainPackIncidentFamilyDto(string FamilyCode, string Name, string? Description, string MatterSignalPatterns, int SortOrder);

// Migration 0433: TargetKindCode CONCEPT|EVIDENCE_TYPE; SignalKindCode MATTER_TYPE|CLAIM|DEFENSE|
// JURISDICTION|PROFILE_FIELD|KEYWORD|DOCUMENT_PRESENT; EffectCode EXCLUDE|PRIORITIZE.
public sealed record DecisionDomainPackRelevanceRuleDto(string TargetKindCode, string TargetCode, string SignalKindCode, string SignalValue, string EffectCode, int Priority, string? Reason);

// PoloxiImportanceScore (0.0–1.0) is an advisory DB-backed importance prior (migration 0407) expressing
// how decisive the node is to the decision. POLOXI Core owns authoritative scoring; this is configuration only.
public sealed record DecisionDomainPackDimensionDto(string DimensionCode, string Name, string? Description, decimal? PoloxiImportanceScore = null);

[method: JsonConstructor]
public sealed record DecisionDomainPackEvidenceTypeDto(string EvidenceTypeCode, string Name, string? DimensionCode, string? Description, decimal? PoloxiImportanceScore = null)
{
    // Migration 0433 (nullable/additive): required-coverage flag, applicable matter types (comma list,
    // empty = all) and relevance keywords used ONLY for deterministic ordering and gap reporting.
    public bool IsRequiredCoverage { get; init; }
    public string? MatterTypeCodes { get; init; }
    public string? RelevanceKeywords { get; init; }
    // Migration 0445 (nullable/additive): comma list of incident family codes; null/empty = all families.
    public string? IncidentFamilyCodes { get; init; }
    // Migration 0455 (normalized): matter-type codes resolved from the
    // Legal_DecisionDomainPackEvidenceTypeMatterType junction. Empty = applies to every matter type.
    // Supersedes the deprecated comma MatterTypeCodes string as the source of truth.
    public IReadOnlyCollection<string> MatterTypeCodeTags { get; init; } = Array.Empty<string>();

    // Dapper materialization constructor: matches the full column set selected by
    // LegalDecisionRepository.BuildDomainPackAsync (Dapper requires a constructor whose signature
    // covers every selected column when no parameterless constructor exists).
    public DecisionDomainPackEvidenceTypeDto(string EvidenceTypeCode, string Name, string? DimensionCode, string? Description, decimal? PoloxiImportanceScore, bool IsRequiredCoverage, string? MatterTypeCodes, string? RelevanceKeywords, string? IncidentFamilyCodes)
        : this(EvidenceTypeCode, Name, DimensionCode, Description, PoloxiImportanceScore)
    {
        this.IsRequiredCoverage = IsRequiredCoverage;
        this.MatterTypeCodes = MatterTypeCodes;
        this.RelevanceKeywords = RelevanceKeywords;
        this.IncidentFamilyCodes = IncidentFamilyCodes;
    }
}

public sealed record DecisionDomainPackVerificationProfileDto(string ProfileCode, string Name, string? EvidenceTypeCode, string? Description);

public sealed record DecisionDomainPackMatterTypeDto(string SubtypeCode, string Name, string? Description);

// ── Personal Injury Jurisdiction Rule (migration 0464) ──────────────────────────────────────────
// A commonly-cited, DB-backed reference row of state/DC personal-injury rules. ADVISORY ONLY and
// NOT legal advice (accrual, tolling, exceptions, and caps vary and change over time). Global
// (TenantId NULL) seed rows are immutable; tenant rows override. POLOXI Core reasoning is unchanged.
public sealed record DecisionJurisdictionRuleDto(
    Guid DecisionJurisdictionRuleId,
    string PracticeAreaCode,
    string JurisdictionCode,
    string JurisdictionName,
    decimal? PersonalInjuryLimitationYears,
    decimal? MedicalMalpracticeLimitationYears,
    decimal? WrongfulDeathLimitationYears,
    string? DiscoveryRuleNote,
    string? MinorsTollingNote,
    string NegligenceRegimeCode,
    string? NegligenceRegimeNote,
    string? JointSeveralLiabilityNote,
    string? DramShopNote,
    string? GovernmentClaimNoticeNote,
    string? NonEconomicDamagesCapNote,
    string? PunitiveDamagesStandardNote,
    string? MedicalMalpracticeNote,
    string? SourceNote,
    DateOnly? EffectiveDate,
    bool IsAdvisoryOnly,
    int SortOrder,
    bool IsActive,
    // True when this row is a tenant override (TenantId not null); false for the global seed row.
    bool IsTenantOverride);

// Canonical Decision Outcome candidate (migration 0396). RoleCode is PATHWAY (prospective resolution)
// or ASSERTED_HISTORICAL (a possibly-completed historical status that must be verified first and must
// never be auto-promoted to an established resolution). RequiresVerification signals the verify-first gate.
public sealed record DecisionDomainPackOutcomeCandidateDto(
    string OutcomeCode,
    string Name,
    string? Description,
    string RoleCode,
    bool RequiresVerification,
    string? MatterTypeCode,
    int SortOrder,
    // Factual-predicate gate (migration 0398). When RequiresFactualPredicate is true the outcome is
    // eligible to enter the candidate universe only when the matter context satisfies the predicate
    // (at least one FactualPredicateKeywords entry is present in the matter facts). This keeps the
    // eligibility rule data-driven per row; no outcome name is hardcoded in application code.
    bool RequiresFactualPredicate = false,
    string? FactualPredicateKeywords = null,
    // Disposition-archetype gate (migration 0450). Declares how this outcome is aggregated over the
    // decision-factor (legal element) axes during Candidate x Branch competition:
    // PLAINTIFF_MERITS (conjunctive), DEFENSE_MERITS (disjunctive over failures), SETTLEMENT (tension),
    // NEUTRAL (membership-weighted average). Null => application falls back to keyword classification.
    string? ArchetypeCode = null,
    // Decision contract membership (migration 0451). Associates this candidate with an independent decision
    // landscape (PROCEDURAL_DISPOSITION | MATTER_RESOLUTION | ...). Null = legacy/default landscape (C1-C9).
    // ContractKind mirrors the contract's kind so future packs can group candidates generically.
    string? DecisionContractCode = null,
    string? ContractKind = null);

// Decision contract (migration 0451): an independent decision landscape with its own competing candidate set
// and its own relative-support denominator. Advisory configuration only; POLOXI Core owns competition.
public sealed record DecisionDomainPackDecisionContractDto(
    string ContractCode,
    string ContractKind,
    string Name,
    string? Description,
    string? DecisionQuestion,
    int SortOrder);

// Directional candidate x dimension mapping (migration 0451). DirectionCode is SUPPORTS | OPPOSES | NEUTRAL |
// UNKNOWN; Weight (0..1) is relative influence; IsEssentialElement flags a gating legal element for readiness.
public sealed record DecisionDomainPackOutcomeCandidateDimensionDto(
    string OutcomeCode,
    string DimensionCode,
    string DirectionCode,
    decimal Weight,
    bool IsEssentialElement,
    string? Rationale,
    int SortOrder);

// Essential/corroborating evidence requirement per outcome (migration 0451). IsEssential distinguishes a
// gating requirement from a corroborating one; MinVerificationProfileCode names the profile that satisfies it.
public sealed record DecisionDomainPackOutcomeCandidateEvidenceRequirementDto(
    string OutcomeCode,
    string EvidenceTypeCode,
    bool IsEssential,
    string? MinVerificationProfileCode,
    string? Rationale,
    int SortOrder);

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

// ── Domain Pack entity/event taxonomy, terminology, and evidence→signal map DTOs (migration 0373).
// These are the DOMAIN SEMANTICS the Decision Channels and the document semantic interpreter resolve
// through. Advisory configuration only — POLOXI Core owns all scoring and competition.
public sealed record DecisionDomainEntityTypeDto(
    string EntityTypeCode,
    string Name,
    string? DimensionCode,
    string? Description,
    int SortOrder);

public sealed record DecisionDomainEventTypeDto(
    string EventTypeCode,
    string Name,
    string? DimensionCode,
    string? Description,
    int SortOrder);

public sealed record DecisionDomainTermDto(
    string TermText,
    string CanonicalCode,
    string TermKindCode,
    int Weight);

public sealed record DecisionDomainSignalMapDto(
    string EvidenceTypeCode,
    string TargetSignalCode,
    string RelationCode,
    string? DimensionCode,
    int SortOrder);

// ── Extracted-document domain entities/events (persistence + read DTOs) ──
public sealed record DocumentDomainEntityPersistence(
    Guid DocumentDomainEntityId,
    Guid DecisionMatterId,
    Guid LegalDocumentId,
    Guid LegalDocumentVersionId,
    Guid? LegalDocumentPassageId,
    string? DomainPackCode,
    string EntityTypeCode,
    string? DimensionCode,
    string EntityText,
    string? NormalizedValue,
    decimal? Confidence,
    string? ProposedByModel,
    string? PromptRunId,
    Guid TenantId,
    Guid? CreatedByUserId);

public sealed record DocumentDomainEventPersistence(
    Guid DocumentDomainEventId,
    Guid DecisionMatterId,
    Guid LegalDocumentId,
    Guid LegalDocumentVersionId,
    Guid? LegalDocumentPassageId,
    string? DomainPackCode,
    string EventTypeCode,
    string? DimensionCode,
    string Summary,
    DateTime? EventDateUtc,
    decimal? Confidence,
    string? ProposedByModel,
    string? PromptRunId,
    Guid TenantId,
    Guid? CreatedByUserId);

public sealed record DocumentDomainEntityDto(
    Guid DocumentDomainEntityId,
    Guid LegalDocumentId,
    Guid LegalDocumentVersionId,
    Guid? LegalDocumentPassageId,
    string? DomainPackCode,
    string EntityTypeCode,
    string? DimensionCode,
    string EntityText,
    string? NormalizedValue,
    decimal? Confidence,
    DateTime CreatedDateUtc);

public sealed record DocumentDomainEventDto(
    Guid DocumentDomainEventId,
    Guid LegalDocumentId,
    Guid LegalDocumentVersionId,
    Guid? LegalDocumentPassageId,
    string? DomainPackCode,
    string EventTypeCode,
    string? DimensionCode,
    string Summary,
    DateTime? EventDateUtc,
    decimal? Confidence,
    DateTime CreatedDateUtc);

// ── ResolvedDomainPack: the single cached, tenant-scoped view the IDomainPackResolver returns.
// It is the formal abstraction the channels resolve through: it exposes concepts, synonym terminology
// (for conservative domain-aware node matching), the evidence-type→TargetSignal/Relation map, and the
// entity/event vocabularies. It never carries a numeric score or signal delta.
public sealed record ResolvedDomainPack(
    string PackCode,
    string PracticeAreaCode,
    IReadOnlyCollection<DecisionDomainConceptDto> Concepts,
    IReadOnlyCollection<DecisionDomainEntityTypeDto> EntityTypes,
    IReadOnlyCollection<DecisionDomainEventTypeDto> EventTypes,
    IReadOnlyCollection<DecisionDomainTermDto> Terms,
    IReadOnlyCollection<DecisionDomainSignalMapDto> SignalMap)
{
    // Canonical Evidence Type vocabulary (Legal_DecisionDomainPackEvidenceType) supplied to the semantic
    // interpreter so extracted evidence binds to the same codes Wide2 scope/presence gating keys on.
    public IReadOnlyCollection<DecisionDomainPackEvidenceTypeDto> EvidenceTypes { get; init; } = [];

    // Synonym surface-form → canonical code, used by the matcher to expand node/assertion text.
    public IReadOnlyDictionary<string, string> TermLookup { get; } =
        Terms.GroupBy(t => t.TermText, StringComparer.OrdinalIgnoreCase)
             .ToDictionary(g => g.Key, g => g.First().CanonicalCode, StringComparer.OrdinalIgnoreCase);

    // Resolve the POLOXI TargetSignal/Relation an evidence type informs; null when the pack has no entry.
    public DecisionDomainSignalMapDto? ResolveSignal(string? evidenceTypeCode)
        => string.IsNullOrWhiteSpace(evidenceTypeCode)
            ? null
            : SignalMap.FirstOrDefault(m => string.Equals(m.EvidenceTypeCode, evidenceTypeCode, StringComparison.OrdinalIgnoreCase));
}

// Well-known Domain Pack codes.
public static class DecisionDomainPackCodes
{
    public const string PersonalInjury = "PERSONAL_INJURY";
}

// Request to transition a matter's lifecycle status (OPEN <-> CLOSED).
public sealed record DecisionMatterStatusUpdateRequest(
    [Required, StringLength(60)] string StatusCode);

// Request to soft-delete multiple matters in a single tenant-scoped operation.
public sealed record DecisionMatterBulkDeleteRequest(
    [Required, MinLength(1)] IReadOnlyCollection<Guid> MatterIds);

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
    public IReadOnlyCollection<string> Counties { get; init; } = [];
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

// ── Candidate Full Analysis read model ──────────────────────────────────────────────────────────
// ── Decision Presentation Gate ─────────────────────────────────────────────────────────────────────────────────────
// The authoritative, UI-agnostic presentation state of a decision. It is DERIVED (never guessed) from
// already-persisted signals — competition execution, contract completeness, readiness verdict, and the
// scoring-blocked status — so every surface (header, metrics, brief) projects the SAME epistemic truth.
// Nothing here changes the underlying verdicts; it only governs how honestly they may be presented.
public enum DecisionPresentationState
{
    // No decision session / candidates at all.
    NoDecision,

    // Candidate structure established, but authoritative competition has not produced a winner.
    CompetitionBlocked,

    // Competition ran and a candidate is leading, but readiness/contract are not fully satisfied.
    Provisional,

    // Competition ran AND readiness is satisfied: an authoritative decision may be presented.
    DecisionReady,
}

// A pure projection carrying the derived presentation state plus the individual authoritative gates that
// produced it, so the UI can both pick the correct headline AND explain exactly why it is (not) a decision.
public sealed record DecisionPresentationStatus(
    DecisionPresentationState State,
    bool CompetitionExecuted,
    bool ContractComplete,
    bool ReadinessPassed,
    int ContractCompletenessPct,
    int InterpretiveSupportPct,
    int NormalizedEntropyPct,
    string Headline,
    string? BlockingReason,
    IReadOnlyList<string> Blockers)
{
    // True only when the UI is permitted to use authoritative decision language
    // ("Current Leader", "Decision Score", "Decision established").
    public bool CanShowCurrentLeader => CompetitionExecuted && State == DecisionPresentationState.DecisionReady;

    // True when competition did not execute: interpretive numbers must be relabeled and badged "NOT COMPETED".
    public bool IsInterpretiveOnly => !CompetitionExecuted;
}

// ── Candidate Full Analysis read model ─────────────────────────────────────────────────────────────────────────────
// Authoritative, DB-backed composition powering the dedicated Candidate Full Analysis workspace
// (/legal/decision/candidate-analysis). It is assembled server-side from already-persisted sources —
// the matter, the latest decision session, the matter evidence graph, matter change events, and
// document stats — then projected onto ONE candidate (by its 1-based rank index). Sections for which
// no persisted per-candidate source exists yet are returned empty so the UI renders honest
// empty-states rather than fabricated values. Nothing here is hardcoded or mocked.
public sealed record CandidateFullAnalysisDto(
    Guid MatterId,
    string MatterTitle,
    string? Jurisdiction,
    string? Posture,
    Guid? DecisionSessionId,
    int CandidateIndex,
    string CandidateLabel,
    string CandidateTitle,
    string? CandidateDescription,
    bool IsLeader,
    int DecisionScorePct,
    int RankPosition,
    int RankTotal,
    int MarginVsNextPts,
    string UncertaintyLabel,
    string ReadinessLabel,
    DateTime? LastRecalibratedUtc,
    IReadOnlyList<CandidateDimensionDto> Dimensions,
    IReadOnlyList<CandidateDriverDto> StrongestDrivers,
    IReadOnlyList<CandidateConstraintDto> StrongestConstraints,
    CandidateHierarchySummaryDto Hierarchy,
    IReadOnlyList<CandidateHierarchyNodeDto> HierarchyNodes,
    IReadOnlyList<CandidatePropositionDto> Propositions,
    IReadOnlyList<CandidateEvidenceCategoryDto> EvidenceBreakdown,
    IReadOnlyList<CandidateMaterialEvidenceDto> MaterialEvidence,
    CandidateAuthoritySummaryDto Authority,
    IReadOnlyList<CandidateFlipPointDto> FlipPoints,
    IReadOnlyList<CandidateAdvItemDto> NextBestInformation,
    CandidateDocumentIntelligenceDto DocumentIntelligence,
    IReadOnlyList<CandidateUncertaintyBandDto> UncertaintyMap,
    IReadOnlyList<CandidateChangeHistoryDto> ChangeHistory,
    CandidateReadinessDto Readiness,
    string? Narrative)
{
    // Derived presentation gate (never persisted directly). Governs whether the UI may use authoritative
    // decision language or must present interpretation-prior values relabeled with a "NOT COMPETED" badge.
    // Defaults to NoDecision so an un-populated DTO can never overstate certainty.
    public DecisionPresentationStatus Presentation { get; init; } = new(
        DecisionPresentationState.NoDecision,
        CompetitionExecuted: false,
        ContractComplete: false,
        ReadinessPassed: false,
        ContractCompletenessPct: 0,
        InterpretiveSupportPct: 0,
        NormalizedEntropyPct: 0,
        Headline: "No decision",
        BlockingReason: null,
        Blockers: []);
}

// A single multidimensional state bar (LEGAL / FACT / EVIDENCE / AUTHORITY / VERIFICATION).
public sealed record CandidateDimensionDto(string Label, int ValuePct, string Tone);

// A driver that pushes the candidate up, with its evidence-grounding counts.
public sealed record CandidateDriverDto(
    string Label,
    int StrengthPct,
    string Description,
    int PropositionCount,
    int EvidenceBindingCount);

// A constraint / risk working against the candidate.
public sealed record CandidateConstraintDto(string Label, string SeverityLabel, string Tone);

// Roll-up counters for the Candidate Hierarchy (CHR) panel.
public sealed record CandidateHierarchySummaryDto(
    int NodeCount,
    int ResolvedCount,
    int TotalForResolution,
    string CoverageLabel,
    string DepthLabel);

// One node in the candidate decision hierarchy tree (CHR). ParentNodeKey is null at the root.
public sealed record CandidateHierarchyNodeDto(
    string NodeKey,
    string? ParentNodeKey,
    int Depth,
    string Label,
    int? StrengthPct,
    string StateCode,
    string StateTone,
    Guid? PropositionId);

// A proposition shown in the Proposition Inspector, with its grounding edges.
public sealed record CandidatePropositionDto(
    Guid PropositionId,
    string Code,
    string Text,
    string StatusCode,
    string StatusTone,
    string AprLabel,
    string? FactorPath,
    IReadOnlyList<CandidatePropositionEvidenceDto> Evidence);

// An evidence edge grounding a proposition (SUPPORTS / CONTRADICTS / QUALIFIES / INSUFFICIENT).
public sealed record CandidatePropositionEvidenceDto(
    Guid EvidenceItemId,
    string RelationshipTypeCode,
    string RelationshipTone,
    string DocumentFileName,
    int? PageNumber,
    string? Summary,
    int? VerificationPct);

// Evidence-analysis matrix row (per factor/dimension).
public sealed record CandidateEvidenceCategoryDto(
    string Label,
    int Supports,
    int Contradicts,
    int Qualifies,
    int Insufficient);

// A single most-decision-material evidence item (HRR + AER trace).
public sealed record CandidateMaterialEvidenceDto(
    Guid EvidenceItemId,
    string DocumentFileName,
    int? PageNumber,
    string? SourceAssertion,
    string? HrrPath,
    string AerRelationshipCode,
    string AerTone,
    int? VerificationPct,
    Guid? LegalDocumentVersionId = null);

// Legal-authority roll-up and the top controlling authority (kept distinct from ordinary evidence).
public sealed record CandidateAuthoritySummaryDto(
    int TotalCount,
    int VerifiedCount,
    int PendingCount,
    string? TopAuthorityLabel,
    string? TopAuthorityCitation,
    string? RelevantProposition,
    bool CitationVerified,
    bool HoldingVerified,
    bool AuthorityVerified,
    string? PropositionFitLabel,
    string? EffectLabel);

// A flip point that could change the candidate's standing.
public sealed record CandidateFlipPointDto(
    string Label,
    string CurrentStateLabel,
    string FlipPotentialLabel,
    string Tone,
    string? Rationale,
    string? AffectedCandidates);

// An ADV / next-best-information recommendation.
public sealed record CandidateAdvItemDto(
    int Rank,
    string Title,
    decimal Adv,
    decimal InformationValue,
    decimal DecisionRelevance,
    decimal FlipPotential,
    string EstimatedCostLabel,
    string? WhyItMatters,
    string? AffectedHierarchyPath);

// Document-intelligence wiring roll-up for the candidate.
public sealed record CandidateDocumentIntelligenceDto(
    int MatterDocumentCount,
    int RelevantDocumentCount,
    int SourceAssertionCount,
    int PropositionBindingCount,
    int VerifiedEvidenceCount,
    int ContradictionCount,
    string? LatestDocumentFileName,
    int? LatestDocumentPage,
    string? LatestAssertionCode,
    string? LatestPropositionCode,
    string? LatestAerCode,
    string? LatestDecisionEffect);

// One uncertainty band in the uncertainty map.
public sealed record CandidateUncertaintyBandDto(string Label, int LevelPct, string LevelLabel, string Tone);

// A decision-change-history point for the candidate (DCI).
public sealed record CandidateChangeHistoryDto(
    DateTime WhenUtc,
    string Label,
    int? ScorePct,
    int? DeltaPts,
    string Tone);

// Candidate-specific readiness breakdown and blockers.
public sealed record CandidateReadinessDto(
    string StateLabel,
    int OverallPct,
    IReadOnlyList<CandidateReadinessMetricDto> Metrics,
    IReadOnlyList<string> Blockers);

public sealed record CandidateReadinessMetricDto(string Label, string Value, int? Pct, string Tone);

// ── Per-matter Evidence Type scope (migration 0441: POLOXI.Legal_MatterEvidenceScope) ───────────
// One row per Decision Matter x Domain Pack L2 target (EVIDENCE_TYPE | CONCEPT). Holds the deterministic
// + LLM relevance decision for THIS matter and attorney overrides. Wide2 L3 generation reads it: only
// statuses whose ParticipatesInL3=1 are sent to atomic-proposition generation. USER decisions are
// authoritative and never overwritten by the system. Targets are always catalog-validated.
public static class MatterEvidenceScopeStatusCodes
{
    public const string InScope = "IN_SCOPE";
    public const string OutOfScope = "OUT_OF_SCOPE";
    public const string PendingReview = "PENDING_REVIEW";
    // Migration 0445: the L2 review never returned a valid score after bounded retries and no deterministic
    // signal admitted the target. Dormant like PENDING_REVIEW; never auto-admitted to L3.
    public const string ReviewFailed = "REVIEW_FAILED";
}

public static class MatterEvidenceScopeDeciderCodes
{
    public const string System = "SYSTEM";
    public const string Llm = "LLM";
    public const string Rule = "RULE";
    public const string User = "USER";
}

public sealed record MatterEvidenceScopeDto(
    Guid MatterEvidenceScopeId,
    Guid DecisionMatterId,
    string PackCode,
    string TargetKindCode,
    string TargetCode,
    string DimensionCode,
    string DisplayName,
    string ScopeStatusCode,
    string DecidedByCode,
    decimal? DeterministicScore,
    decimal? LlmScore,
    decimal? BlendedScore,
    string? TierCode,
    string? RelevanceReasons,
    string? LlmReason,
    bool IsRequiredCoverage,
    bool IsPrioritized,
    bool DocumentPresent,
    int MappedPassageCount,
    string? UserNote,
    Guid? LastEvaluatedExecutionId,
    DateTime? LastEvaluatedDateUtc,
    int SortOrder,
    DateTime CreatedDateUtc,
    DateTime? ModifiedDateUtc,
    // 0448: independent routing attributes (Legal_MatterEvidenceRoutingCode). Null on rows not yet re-evaluated.
    // Constructor parameters (not init-only extras) so Dapper's constructor mapping covers every selected column.
    string? EvidenceAvailabilityCode = null,
    string? VerificationStatusCode = null,
    string? ExecutionPriorityCode = null,
    int DirectPassageCount = 0,
    int VerifiedAssertionCount = 0,
    string? RoutingReason = null);

// 0448: DB-backed routing vocabularies (POLOXI.Legal_MatterEvidenceRoutingCode). Constants mirror seed codes.
public static class MatterEvidenceRoutingCodeKinds
{
    public const string EvidenceAvailability = "EVIDENCE_AVAILABILITY";
    public const string VerificationStatus = "VERIFICATION_STATUS";
    public const string ExecutionPriority = "EXECUTION_PRIORITY";
    public const string ClassificationProposalStatus = "CLASSIFICATION_PROPOSAL_STATUS";
}

public static class MatterEvidenceAvailabilityCodes
{
    public const string Present = "PRESENT";
    public const string Referenced = "REFERENCED";
    public const string Absent = "ABSENT";
    public const string NotApplicable = "NOT_APPLICABLE";
}

public static class MatterEvidenceVerificationCodes
{
    public const string Verified = "VERIFIED";
    public const string Unverified = "UNVERIFIED";
    public const string Contradicted = "CONTRADICTED";
    public const string Unknown = "UNKNOWN";
}

public static class MatterEvidenceExecutionPriorityCodes
{
    public const string Immediate = "IMMEDIATE";
    public const string Normal = "NORMAL";
    public const string Deferred = "DEFERRED";
    public const string Dormant = "DORMANT";
}

public sealed record MatterEvidenceRoutingCodeDto(string CodeKind, string Code, string Name, string? Description, bool RunsL3, int SortOrder);

// 0448: confidence-qualified classification proposal (POLOXI.Legal_DecisionMatterClassificationProposal).
// The authoritative Legal_DecisionMatter / PI profile fields are never mutated by the system.
public static class MatterClassificationProposalStatusCodes
{
    public const string Proposed = "PROPOSED";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
}

public static class MatterClassificationFieldCodes
{
    public const string Subtype = "SUBTYPE";
    public const string IncidentTypeCode = "INCIDENT_TYPE_CODE";
    public const string IncidentFamily = "INCIDENT_FAMILY";
    public const string DecisionContext = "DECISION_CONTEXT";
}

public sealed record MatterClassificationProposalDto(
    Guid DecisionMatterClassificationProposalId,
    Guid DecisionMatterId,
    string FieldCode,
    string? CurrentValue,
    string? ProposedValue,
    decimal Confidence,
    string? StructuredSignals,
    string? NarrativeSignals,
    string Rationale,
    string StatusCode,
    Guid? DecidedByUserId,
    DateTime? DecidedDateUtc,
    string? DecisionNote,
    Guid? LastProposedExecutionId,
    DateTime CreatedDateUtc,
    DateTime? ModifiedDateUtc);

// Pipeline write: upserts the single open PROPOSED row per matter/field. Decided rows are history and untouched.
public sealed record MatterClassificationProposalEvaluation(
    string FieldCode,
    string? CurrentValue,
    string? ProposedValue,
    decimal Confidence,
    string? StructuredSignals,
    string? NarrativeSignals,
    string Rationale,
    Guid? LastProposedExecutionId);

public sealed record MatterClassificationProposalDecisionRequest(
    [property: Required, StringLength(40)] string StatusCode,
    [property: StringLength(1000)] string? DecisionNote);

public sealed record MatterEvidenceScopeStatusDto(string StatusCode, string Name, string? Description, bool ParticipatesInL3, int SortOrder);

public sealed record MatterEvidenceScopeDeciderDto(string DeciderCode, string Name, string? Description, bool IsAuthoritative, int SortOrder);

// Catalog target available for a matter's pack (used by the UI "add" dropdown; only these may be inserted).
public sealed record MatterEvidenceScopeTargetOptionDto(string TargetKindCode, string TargetCode, string DimensionCode, string DisplayName, string? Description, bool IsRequiredCoverage);

public sealed record MatterEvidenceScopeOptionsDto
{
    public string? PackCode { get; init; }
    public IReadOnlyCollection<MatterEvidenceScopeStatusDto> Statuses { get; init; } = [];
    public IReadOnlyCollection<MatterEvidenceScopeDeciderDto> Deciders { get; init; } = [];
    public IReadOnlyCollection<MatterEvidenceScopeTargetOptionDto> Targets { get; init; } = [];
}

// Attorney create/edit (UI). Always recorded as DecidedByCode=USER by the service.
public sealed record MatterEvidenceScopeUpsertRequest(
    [property: Required, StringLength(30)] string TargetKindCode,
    [property: Required, StringLength(240)] string TargetCode,
    [property: Required, StringLength(40)] string ScopeStatusCode,
    [property: StringLength(1000)] string? UserNote);

public sealed record MatterEvidenceScopeBulkStatusRequest(
    [property: Required, MinLength(1)] IReadOnlyCollection<Guid> MatterEvidenceScopeIds,
    [property: Required, StringLength(40)] string ScopeStatusCode,
    [property: StringLength(1000)] string? UserNote);

// Pipeline (Wide2) write: system/LLM evaluation for one target. Never overwrites a USER row.
public sealed record MatterEvidenceScopeEvaluation(
    string PackCode,
    string TargetKindCode,
    string TargetCode,
    string DimensionCode,
    string DisplayName,
    string ScopeStatusCode,
    string DecidedByCode,
    decimal? DeterministicScore,
    decimal? LlmScore,
    decimal? BlendedScore,
    string? TierCode,
    string? RelevanceReasons,
    string? LlmReason,
    bool IsRequiredCoverage,
    bool IsPrioritized,
    bool DocumentPresent,
    int MappedPassageCount,
    Guid? LastEvaluatedExecutionId,
    int SortOrder)
{
    // 0448: routing attributes computed independently of ScopeStatusCode (see MatterEvidenceRouting).
    public string? EvidenceAvailabilityCode { get; init; }
    public string? VerificationStatusCode { get; init; }
    public string? ExecutionPriorityCode { get; init; }
    public int DirectPassageCount { get; init; }
    public int VerifiedAssertionCount { get; init; }
    public string? RoutingReason { get; init; }
}

// ── Per-matter Atomic Propositions
// One row per Decision Matter x parent L2 target x LLM-generated atomic proposition (L3+). Captures every
// model-returned value, the worker/batch that produced it and call provenance. USER decisions are
// authoritative; the pipeline refreshes model values but never overwrites a USER status/note.
public static class MatterAtomicPropositionStatusCodes
{
    public const string InScope = "IN_SCOPE";
    public const string OutOfScope = "OUT_OF_SCOPE";
    public const string PendingReview = "PENDING_REVIEW";
}

public static class MatterAtomicPropositionDeciderCodes
{
    public const string Llm = "LLM";
    public const string User = "USER";
}

public sealed record MatterAtomicPropositionDto(
    Guid MatterAtomicPropositionId,
    Guid DecisionMatterId,
    string PackCode,
    string ParentTargetKindCode,
    string ParentTargetCode,
    string ParentDisplayName,
    string DimensionCode,
    string? ParentPropositionCode,
    string PropositionCode,
    int LevelNumber,
    string DisplayName,
    string Statement,
    string? SearchText,
    string? CapabilityCode,
    decimal Confidence,
    string? SemanticTypeCode,
    string? BranchRoleCode,
    bool ContinueNarrowing,
    string? StopReason,
    string? DependsOnCodes,
    string? EquivalentToCodes,
    int AnchorCount,
    string? AnchorsJson,
    string WorkerName,
    int BatchNumber,
    int AttemptNumber,
    string? BatchTierCode,
    string? ModelCode,
    string? ProviderCode,
    int? InputTokenCount,
    int? OutputTokenCount,
    int? DurationMs,
    string? ProviderRequestId,
    string ScopeStatusCode,
    string DecidedByCode,
    string? UserNote,
    int GenerationCount,
    Guid? LastWideBranchId,
    Guid? LastEvaluatedExecutionId,
    DateTime? LastEvaluatedDateUtc,
    int SortOrder,
    DateTime CreatedDateUtc,
    DateTime? ModifiedDateUtc);

public sealed record MatterAtomicPropositionStatusDto(string StatusCode, string Name, string? Description, bool ParticipatesInNarrowing, int SortOrder);

public sealed record MatterAtomicPropositionDeciderDto(string DeciderCode, string Name, string? Description, bool IsAuthoritative, int SortOrder);

public sealed record MatterAtomicPropositionOptionsDto
{
    public string? PackCode { get; init; }
    public IReadOnlyCollection<MatterAtomicPropositionStatusDto> Statuses { get; init; } = [];
    public IReadOnlyCollection<MatterAtomicPropositionDeciderDto> Deciders { get; init; } = [];
    // Parent L2 targets available for this matter's pack (catalog-bound; only these may be used as parents).
    public IReadOnlyCollection<MatterEvidenceScopeTargetOptionDto> Parents { get; init; } = [];
}

// Attorney create/edit (UI). Always recorded as DecidedByCode=USER by the service.
public sealed record MatterAtomicPropositionUpsertRequest(
    [property: Required, StringLength(30)] string ParentTargetKindCode,
    [property: Required, StringLength(240)] string ParentTargetCode,
    [property: Required, StringLength(240)] string PropositionCode,
    [property: Required, StringLength(300)] string DisplayName,
    [property: Required, StringLength(2000)] string Statement,
    [property: Range(0, 1)] decimal Confidence,
    [property: StringLength(30)] string? SemanticTypeCode,
    [property: StringLength(30)] string? BranchRoleCode,
    [property: Required, StringLength(40)] string ScopeStatusCode,
    [property: StringLength(1000)] string? UserNote);

public sealed record MatterAtomicPropositionBulkStatusRequest(
    [property: Required, MinLength(1)] IReadOnlyCollection<Guid> MatterAtomicPropositionIds,
    [property: Required, StringLength(40)] string ScopeStatusCode,
    [property: StringLength(1000)] string? UserNote);

// Pipeline (Wide2) write: one generated proposition. Never overwrites a USER row's status/note.
public sealed record MatterAtomicPropositionEvaluation(
    string PackCode,
    string ParentTargetKindCode,
    string ParentTargetCode,
    string ParentDisplayName,
    string DimensionCode,
    string? ParentPropositionCode,
    string PropositionCode,
    int LevelNumber,
    string DisplayName,
    string Statement,
    string? SearchText,
    string? CapabilityCode,
    decimal Confidence,
    string? SemanticTypeCode,
    string? BranchRoleCode,
    bool ContinueNarrowing,
    string? StopReason,
    string? DependsOnCodes,
    string? EquivalentToCodes,
    int AnchorCount,
    string? AnchorsJson,
    string WorkerName,
    int BatchNumber,
    int AttemptNumber,
    string? BatchTierCode,
    string? ModelCode,
    string? ProviderCode,
    int? InputTokenCount,
    int? OutputTokenCount,
    int? DurationMs,
    string? ProviderRequestId,
    Guid WideBranchId,
    Guid WideExecutionId,
    int SortOrder);
