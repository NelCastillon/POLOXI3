namespace Legal.Application.Features.Intelligence.Decision;

using System.ComponentModel.DataAnnotations;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// DB-backed configuration + persistence-snapshot contracts for the self-contained decision module.
// These come exclusively from POLOXI.Legal_Decision* tables — no values are hardcoded in code (§3).
// ─────────────────────────────────────────────────────────────────────────────────────────────

// A single AI route row (POLOXI.Legal_DecisionModelRoute) used by the self-contained provider.
public sealed record DecisionModelRouteDto(
    string FeatureCode,
    string ProviderTypeCode,
    string ModelCode,
    string DeploymentName,
    string EndpointReference,
    string? CredentialReference,
    string ApiVersion,
    int TimeoutSeconds,
    int MaxOutputTokens,
    decimal Temperature,
    int Priority);

// ── Execution modes (DEV Logic / PROD Logic) ────────────────────────────────────────────────────
// Two shared-pipeline execution profiles. Modes are execution SETTINGS (default model, replay
// permission, environment restriction), NOT separate reasoning engines. The same POLOXI Core, Legal
// Domain Pack, Light Evidence Graph and Typed Legal Dependency Graph V2 run in both modes; only the
// model/replay/provider execution settings differ. Governance and verification gates are identical.
public enum DecisionExecutionMode
{
    // Fast development/debugging. Default gpt-4.1-mini, replay allowed, never runs in Production.
    Dev,
    // Full-quality governed output. Default gpt-6-astra, replay disabled, Production-authorized.
    Prod
}

// A row of POLOXI.Legal_DecisionExecutionMode: the DB-backed, admin-editable configuration that
// resolves an execution mode to its default model deployment, provider/endpoint overrides and
// safeguards. Nullable override fields mean "use the existing Auto/route-resolved value" (so blank =
// unchanged pre-existing behavior, which is what keeps PROD Logic identical to the current pipeline).
public sealed record DecisionExecutionModeDto(
    string ExecutionModeCode,
    string DisplayName,
    string? Description,
    string? DefaultModelCode,
    bool AllowReplay,
    bool IsProductionAllowed,
    int SortOrder,
    bool IsActive,
    string? ProviderTypeCode = null,
    string? EndpointReference = null,
    string? ApiVersion = null,
    decimal? Temperature = null,
    int? MaxOutputTokens = null,
    int? TimeoutSeconds = null);

// Admin update for a single execution mode (Configuration Mode page). ExecutionModeCode identifies the
// row; nullable overrides clear to "use existing routed value" when omitted. DisplayName/Description
// are editable labels; the execution safeguards (AllowReplay, IsProductionAllowed) are editable flags.
public sealed record SaveDecisionExecutionModeRequest(
    [property: Required, StringLength(20)] string ExecutionModeCode,
    [property: Required, StringLength(80)] string DisplayName,
    [property: StringLength(400)] string? Description,
    [property: StringLength(100)] string? DefaultModelCode,
    bool AllowReplay,
    bool IsProductionAllowed,
    [property: StringLength(50)] string? ProviderTypeCode,
    [property: StringLength(400)] string? EndpointReference,
    [property: StringLength(40)] string? ApiVersion,
    [property: Range(0, 2)] decimal? Temperature,
    [property: Range(1, 1000000)] int? MaxOutputTokens,
    [property: Range(1, 900)] int? TimeoutSeconds);

// A prompt family row (POLOXI.Legal_DecisionPrompt).
public sealed record DecisionPromptDefinition(
    string PromptCode,
    string StageCode,
    string SystemPrompt,
    string UserPromptTemplate,
    string? OutputSchemaJson);

public sealed record DecisionPromptConfigurationDto(
    string PromptCode,
    string StageCode,
    string SystemPrompt,
    string UserPromptTemplate,
    string? OutputSchemaJson,
    bool IsActive,
    DateTime CreatedDateUtc,
    DateTime? ModifiedDateUtc);

public sealed record SaveDecisionPromptConfigurationRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(120)]
    string PromptCode,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(60)]
    string StageCode,
    [property: System.ComponentModel.DataAnnotations.Required]
    string SystemPrompt,
    [property: System.ComponentModel.DataAnnotations.Required]
    string UserPromptTemplate,
    string? OutputSchemaJson,
    bool IsActive);

// Admin-editable update for a POLOXI.Legal_DecisionModelRoute row. FeatureCode identifies the route
// (kept read-only in the UI); the remaining fields mirror the editable table columns. Governance and
// verification gates are unaffected — this only adjusts model/endpoint routing settings.
public sealed record SaveDecisionModelRouteRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(120)]
    string FeatureCode,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(60)]
    string ProviderTypeCode,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(100)]
    string ModelCode,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(100)]
    string DeploymentName,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(400)]
    string EndpointReference,
    [property: System.ComponentModel.DataAnnotations.StringLength(400)]
    string? CredentialReference,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(40)]
    string ApiVersion,
    [property: System.ComponentModel.DataAnnotations.Range(1, 3600)]
    int TimeoutSeconds,
    [property: System.ComponentModel.DataAnnotations.Range(1, 100000000)]
    int MaxOutputTokens,
    [property: System.ComponentModel.DataAnnotations.Range(0, 2)]
    decimal Temperature,
    [property: System.ComponentModel.DataAnnotations.Range(0, 100000)]
    int Priority,
    bool IsActive);

// Create a brand-new POLOXI.Legal_DecisionPrompt row (distinct from the update-only save above).
public sealed record CreateDecisionPromptConfigurationRequest(
    [property: Required, StringLength(120)] string PromptCode,
    [property: Required, StringLength(60)] string StageCode,
    [property: Required] string SystemPrompt,
    [property: Required] string UserPromptTemplate,
    string? OutputSchemaJson,
    bool IsActive);

// ── Domain Pack child CRUD requests (advisory domain configuration) ──────────────────────────
// Each request targets a specific pack (DecisionDomainPackId) and upserts by business code.
public sealed record SaveDomainPackDimensionRequest(
    [property: Required, StringLength(60)] string DimensionCode,
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(1000)] string? Description,
    int SortOrder,
    bool IsActive);

public sealed record SaveDomainPackEvidenceTypeRequest(
    [property: Required, StringLength(60)] string EvidenceTypeCode,
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(60)] string? DimensionCode,
    [property: StringLength(1000)] string? Description,
    int SortOrder,
    bool IsActive);

public sealed record SaveDomainPackVerificationProfileRequest(
    [property: Required, StringLength(60)] string ProfileCode,
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(60)] string? EvidenceTypeCode,
    [property: StringLength(1000)] string? Description,
    int SortOrder,
    bool IsActive);

public sealed record SaveDomainPackMatterTypeRequest(
    [property: Required, StringLength(120)] string MatterTypeCode,
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(1000)] string? Description,
    int SortOrder,
    bool IsActive);

public sealed record SaveDomainPackConceptRequest(
    [property: Required, StringLength(80)] string ConceptCode,
    [property: Required, StringLength(60)] string DimensionCode,
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(1200)] string? Description,
    [property: Required, StringLength(40)] string ConceptKindCode,
    [property: Required, StringLength(40)] string SourceClassCode,
    [property: StringLength(60)] string? VerificationProfileCode,
    [property: StringLength(120)] string? JurisdictionCode,
    [property: StringLength(120)] string? MatterTypeCode,
    bool IsRequiredCoverage,
    bool IsFallbackEligible,
    int SortOrder,
    bool IsActive);

public sealed record SaveDomainPackConceptRelationRequest(
    [property: Required, StringLength(80)] string SourceConceptCode,
    [property: Required, StringLength(80)] string TargetConceptCode,
    [property: Required, StringLength(40)] string RelationTypeCode,
    [property: StringLength(80)] string? ConstraintCode,
    [property: StringLength(1200)] string? Description,
    [property: StringLength(120)] string? JurisdictionCode,
    [property: StringLength(120)] string? MatterTypeCode,
    bool IsHardConstraint,
    int SortOrder,
    bool IsActive);

// A single editable row of POLOXI.Legal_DecisionSetting (the runtime decision-tuning table).
// Surfaced in the Legal Configuration UI so operators can toggle/tune settings such as the
// clarification preflight gate (Decision.Preflight.*) without editing the database by hand.
public sealed record DecisionSettingDto(
    string SettingKey,
    string SettingValue,
    string DataTypeCode,
    string? Description,
    DateTime CreatedDateUtc,
    DateTime? ModifiedDateUtc);

public sealed record SaveDecisionSettingRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(200)]
    string SettingKey,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(1000)]
    string SettingValue);

// The Core control weights/thresholds loaded from POLOXI.Legal_DecisionSetting (§11,§12,§34).
public sealed record DecisionCoreSettings(
    double WeightUncertainty,
    double WeightRankingImpact,
    double WeightDiscrimination,
    double WeightEvidenceAvailability,
    double WeightNovelty,
    double WeightRedundancyPenalty,
    double ThresholdDecisionRelevance,
    double ThresholdFlipPotential,
    double ThresholdReopen,
    double ThresholdResearchExhaustionAdv,
    double ThresholdTransformationRelevance,
    double ThresholdDeepeningFlip,
    int MaxDepth,
    int MaxLlmCalls,
    int MaxCandidates,
    // Proposal Integrity Gate recovery switch. Default false = SHADOW MODE: the gate computes a
    // disposition and diagnostics for correlation analysis but never rejects or repairs a run. When
    // true, a FAIL disposition triggers exactly ONE targeted, defect-diagnosed recovery attempt before
    // the proposal is allowed to compete (or is declared UNRESOLVED). Kept off until shadow telemetry
    // shows gate failures actually predict poor downstream results.
    bool EnableProposalRecovery = false)
{
    public DecisionVerificationSettings Verification { get; init; } = new();

    // Additive early Decision-Contract completeness preflight (attorney clarification BEFORE the
    // expensive POLOXI discovery/graph/verification pipeline). Off by default; it never replaces the
    // dynamic clarification logic that only becomes visible after candidate competition.
    public DecisionPreflightSettings Preflight { get; init; } = new();
}

// Configurable gate that lets POLOXI ask a targeted clarification up front when an ESSENTIAL
// decision-contract input is missing, so the run does not pay for the full pipeline just to end in
// CLARIFICATION_REQUIRED. Deliberately conservative: it must NOT fire on "zero uploaded documents"
// alone (a valid hypothetical analysis is still allowed when the attorney supplies sufficient facts).
public sealed record DecisionPreflightSettings
{
    // Master switch. Default false = disabled (preserves current behavior exactly).
    public bool Enabled { get; init; }

    // When true, a missing explicit procedural relief/instruction (Posture + MotionTarget both empty)
    // is treated as an essential-input gap that warrants an up-front clarification.
    public bool RequireProceduralInstruction { get; init; } = true;

    // When true, the gate only fires if the matter/query also supplies no usable facts (so a purely
    // hypothetical, fact-bearing query is never blocked). When false, missing procedural instruction
    // alone is enough. Default true keeps the gate narrow.
    public bool RequireFactsWhenProcedureMissing { get; init; } = true;

    // Minimum effective-query length (characters) below which the query is considered fact-thin for
    // the "facts supplied" test. Configurable so tenants can tune sensitivity.
    public int MinimumFactsQueryLength { get; init; } = 40;
}

public sealed record DecisionVerificationSettings
{
    public bool Enabled { get; init; } = true;
    public bool ShadowMode { get; init; } = true;
    public bool MechanicalVerificationEnabled { get; init; } = true;
    public bool SemanticVerificationEnabled { get; init; } = true;
    public int MaxInputTokensPerEvidence { get; init; } = 2500;
    public int MaxOutputTokensPerEvidence { get; init; } = 700;
    public bool AllowSchemaRepair { get; init; } = true;
    public int MaxSchemaRepairAttempts { get; init; } = 1;
    public bool PoloxiDeepeningEnabled { get; init; }
    public bool PoloxiDecisionMaterialOnly { get; init; } = true;
    public int PoloxiMaxRounds { get; init; } = 1;
    public bool EnforceVerifiedEvidenceOnly { get; init; }
    public bool CacheEnabled { get; init; } = true;
}

// POLOXI Legal V2 dependency-graph control settings loaded from POLOXI.Legal_DecisionSetting.
public sealed record DecisionV2Settings(
    bool UseDependencyGraphDefault,
    double MaterialityThreshold,
    double ReadinessMinAuthorityVerified,
    double ReadinessLosingSideMargin,
    int PropagationMaxDepth,
    int ReadinessMaxHighImpactFrontier,
    // B3 Hallucination Solver AUTHORITY mode. The solver always recompetes and surfaces a
    // non-destructive shadow "what-if" snapshot so both rankings are visible. This flag only decides
    // which one is authoritative: false = Advisory (original decision stays authoritative; recomputed
    // shown as what-if) — the default; true = Enforced (recomputed ranking replaces the returned
    // decision). Advisory reproduces the frozen ASPEN_B2 returned baseline while still exposing B3.
    bool ApplyVerifiedSignalsToRanking = false,
    // Branch-first discovery (v2). false = the legacy candidate-first DECISION_DISCOVERY path (LLM
    // proposes candidates that own their branches and supplies its own composite). true = the
    // DECISION_DISCOVERY_V2 path: the LLM proposes a SHARED L1→L3 branch tree + one GLOBAL candidate
    // universe + a candidate×branch matrix, and POLOXI Core derives each candidate composite and the
    // flip points from that matrix. Default false keeps v1 byte-identical for rollback.
    bool BranchFirstDiscoveryEnabled = false);

// Full authoritative snapshot persisted at the end of a session (§35,§39).
public sealed record DecisionSessionPersistence(
    Guid DecisionSessionId,
    Guid TenantId,
    Guid? ActorUserId,
    string QueryText,
    string? ContextCode,
    string? ModelCode,
    bool UsePoloxiEngine,
    string StatusCode,
    string? TerminalStateCode,
    string TerminationReason,
    Guid? WinnerCandidateId,
    decimal ContractCompleteness,
    decimal CandidateEntropy,
    decimal DecisionMargin,
    int DepthReached,
    int LlmCallCount,
    long DurationMs,
    string? FinalAnswer,
    string? ClarificationQuestion,
    string? ClarificationTarget,
    string? CorrelationId,
    IReadOnlyCollection<DecisionCandidatePersistence> Candidates,
    IReadOnlyCollection<DecisionBranchPersistence> Branches,
    IReadOnlyCollection<DecisionEvidencePersistence> Evidence,
    IReadOnlyCollection<DecisionFlipPointPersistence> FlipPoints,
    IReadOnlyCollection<DecisionEventPersistence> Events)
{
    // Cockpit additions (0210): matter linkage + persisted Next Best Action.
    public Guid? MatterId { get; init; }
    public string? NextBestActionText { get; init; }
    public string? NextBestActionImpactCode { get; init; }
    public string? NextBestActionRationale { get; init; }
    public string? CounterfactualAssumption { get; init; }

    // Research outcome additions (0262): explicit, durable retrieval state (§13) so a rehydrated
    // decision can distinguish RETRIEVAL_FAILED / SEARCH_NO_RESULTS / RETRIEVED / NOT_NEEDED.
    public string? ResearchStatusCode { get; init; }
    public string? ResearchFailureDetail { get; init; }
    public Guid? ParentDecisionSessionId { get; init; }
    public string? MatterJurisdiction { get; init; }
    public string? GoverningLaw { get; init; }
    public string? CourtOrForum { get; init; }
    public DateOnly? AuthorityCutoffDate { get; init; }
    public LegalAuthorityScope? AuthorityScope { get; init; }

    // Execution mode (DEV / PROD) frozen into the immutable session snapshot at creation. A continued
    // (clarification) session inherits the parent's ModeCode so the run cannot silently switch modes.
    public string? ModeCode { get; init; }

    // DECISION_DISCOVERY_V2 enrichment (§4,§5,§6): Candidate × Branch relationship edges and the typed
    // dependency nodes (unresolved propositions + fact provenance). Empty for the legacy v1 path.
    public IReadOnlyCollection<DecisionCandidateBranchRelationPersistence> CandidateBranchRelations { get; init; } = [];
    public IReadOnlyCollection<DecisionDependencyPersistence> Dependencies { get; init; } = [];

    // DECISION_DISCOVERY_V2 (§1): the first-class structured DecisionIntent proposed by Astra (the
    // specific decision + its scope). Null for the legacy v1 path or when no intent was proposed.
    public DecisionIntentPersistence? DecisionIntent { get; init; }
}

public sealed record DecisionClarificationPersistence(
    Guid DecisionClarificationId,
    Guid DecisionSessionId,
    Guid ParentDecisionSessionId,
    string? Question,
    string? Target,
    string Answer,
    Guid? DecisionBranchId,
    Guid? DecisionCandidateId,
    Guid? DecisionGraphNodeId,
    Guid? DecisionGraphEdgeId,
    string ScopeCode,
    Guid TenantId,
    Guid? ActorUserId,
    DateTime CreatedDateUtc);

public sealed record DecisionEvidenceVerificationFactorPersistence(
    Guid DecisionEvidenceVerificationFactorId,
    string FactorCode,
    string StateCode,
    string ReasonCode,
    string? Reason,
    string? VerifiedValue,
    string? SourceRef,
    string? SupportingPassage,
    string VerificationMethod,
    string? SupportedComponentsJson,
    string? UnsupportedComponentsJson,
    DateTime EvaluatedDateUtc)
{
    public string? PassageRef { get; init; }
    public string? VerifierId { get; init; }
    public string? VerifierVersion { get; init; }
}

public sealed record DecisionEvidenceVerificationPersistence(
    Guid DecisionEvidenceVerificationId,
    Guid DecisionEvidenceId,
    Guid DecisionSessionId,
    Guid? DecisionBranchId,
    string SourceTypeCode,
    string ProfileCode,
    string DispositionCode,
    bool IsVerified,
    bool IsDecisionAuthorized,
    string? BlockingReasonsJson,
    Guid? MatterId,
    Guid TenantId,
    Guid? ActorUserId,
    DateTime EvaluatedDateUtc,
    IReadOnlyCollection<DecisionEvidenceVerificationFactorPersistence> Factors)
{
    public Guid? SourceSnapshotId { get; init; }
    public string? SourceContentHash { get; init; }
    public string? PassageHash { get; init; }
    public string? SourceProvider { get; init; }
    public string? SourceVersion { get; init; }
    public string? SourceRef { get; init; }
    public string? PassageRef { get; init; }
    public string? ExtractionVersion { get; init; }
    public int ProfileVersion { get; init; } = 1;
    public int VerificationVersion { get; init; }
    public int MechanicalVerificationCount { get; init; }
    public int RetrievedCount { get; init; }
    public int PreScreenRejectedCount { get; init; }
    public int SemanticVerificationCount { get; init; }
    public int PoloxiDeepeningCount { get; init; }
    public int CacheHitCount { get; init; }
    public int InputTokenCount { get; init; }
    public int OutputTokenCount { get; init; }
    public long LatencyMilliseconds { get; init; }
}

public sealed record DecisionCandidatePersistence(
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
    // Branch-first (V2) provenance: the LLM-proposed stable candidateId, carried so Candidate × Branch
    // relations resolve to this persisted candidate. Null for the legacy candidate-first path.
    public string? SemanticCandidateId { get; init; }

    // Branch-first (V2) advisory score in [0,1] proposed by Astra and consumed by POLOXI Core for
    // further reasoning. Advisory only — Core still owns the authoritative CompositeScore/verdict.
    // Null on the legacy candidate-first path or when the proposal omitted a score.
    public decimal? ProposedScore { get; init; }
}

public sealed record DecisionBranchPersistence(
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
    decimal Cost,
    bool IsOnFrontier,
    string? StopReason,
    int SortOrder)
{
    public DecisionBranchPersistence()
        : this(Guid.Empty, null, 0, string.Empty, string.Empty, null, string.Empty,
            0m, 0m, 0m, 0m, 0m, 0m, false, null, 0)
    {
    }

    public bool IsExecutable =>
        !string.Equals(GenerationOriginCode,
            DecisionBranchGenerationOrigins.DomainFallback, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(BranchStateCode,
            DecisionBranchStates.Dormant, StringComparison.OrdinalIgnoreCase);

    public string GenerationOriginCode { get; init; } = DecisionBranchGenerationOrigins.DynamicLlm;
    public Guid? DecisionDomainConceptId { get; init; }
    public string? DomainConceptCode { get; init; }
    public decimal? GuardrailMatchScore { get; init; }
    public string? GuardrailActionCode { get; init; }
    public int? GuardrailVersion { get; init; }

    // Branch-first (DECISION_DISCOVERY_V2) provenance: the stable LLM-proposed branchId (e.g. B1.1),
    // carried so Candidate × Branch relations and unresolved propositions can resolve to this branch.
    public string? SemanticBranchId { get; init; }
}

// ── DECISION_DISCOVERY_V2 enrichment records (§4,§5,§6). These are PROPOSAL-stage semantic assertions
//    only; POLOXI Core owns all scoring, evidence admission, competition, and readiness. ──

// §4 Candidate × Branch relationship: the SEMANTIC role a candidate plays against a SHARED branch.
public sealed record DecisionCandidateBranchRelationPersistence(
    Guid DecisionCandidateBranchRelationId,
    Guid DecisionCandidateId,
    Guid DecisionBranchId,
    string RelationTypeCode,
    string? Rationale);

// §1 First-class DecisionIntent: the specific decision and its scope Astra proposed. Descriptive
// proposal-stage content; POLOXI Core still validates it before candidate discovery and hierarchy
// registration. Persisted one-per-session in POLOXI.Legal_DecisionIntent.
public sealed record DecisionIntentPersistence(
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

// §5/§6 Unresolved proposition + fact provenance, persisted as typed Legal_DecisionDependency nodes.
public sealed record DecisionDependencyPersistence(
    Guid DecisionDependencyId,
    Guid? DecisionCandidateId,
    string NodeKind,
    string Statement,
    decimal Support,
    bool IsEssential,
    string? FailureCode,
    string? ProvenanceCode,
    string? EvidenceNeeded,
    string? AuthorityNeeded,
    bool IsVerified,
    string? LinkedBranchCode);

public sealed record DecisionEvidencePersistence(
    Guid DecisionEvidenceId,
    Guid? DecisionBranchId,
    string? SourceRef,
    string? SourceTitle,
    string? Snippet,
    decimal IdentityFactor,
    decimal CitationFactor,
    decimal HoldingFactor,
    decimal WeightFactor,
    decimal PropositionFit,
    decimal VerificationValue,
    string VerificationStatus)
{
    // Verification provenance (0263, §14): traceable claim ↔ source ↔ passage ↔ verification chain.
    public string? SupportedObjective { get; init; }
    public string? SupportingPassage { get; init; }
    public string? LifecycleState { get; init; }
}

// Evidence attachment authority is separate from both structural graph verification and the source's
// verification lifecycle. Every retrieval attempt starts non-authoritative; only verified proposition
// support may finalize as SUPPORTED_BY and influence the decision.
public static class DecisionEvidenceAttachmentStates
{
    public const string ProposedSupportFor = "PROPOSED_SUPPORT_FOR";
    public const string SupportedBy = "SUPPORTED_BY";
    public const string PartiallySupportedBy = "PARTIALLY_SUPPORTED_BY";
    public const string ContradictedBy = "CONTRADICTED_BY";
    public const string Unsupported = "UNSUPPORTED";
}

public sealed record DecisionEvidenceAttachmentPersistence(
    Guid DecisionEvidenceAttachmentId,
    Guid DecisionSessionId,
    Guid TenantId,
    Guid? ActorUserId,
    Guid? MatterId,
    Guid DecisionResearchNeedId,
    Guid DecisionBranchId,
    Guid DecisionEvidenceId,
    string PropositionToResolve,
    string SupportStateCode,
    Guid? AffectedGraphEdgeId,
    bool IsAuthoritative,
    string? AssessmentReason)
{
    public Guid? DecisionEvidenceVerificationId { get; init; }
    public Guid? SourceSnapshotId { get; init; }
    public string? PassageRef { get; init; }
    public Guid AtomicPropositionId { get; init; }
    public Guid? LegalSearchPlanId { get; init; }
    public Guid? NormalizedAuthorityId { get; init; }
    public string? NormalizedAuthorityIdentity { get; init; }
    public decimal? PropositionSelectionScore { get; init; }
    public int? PropositionSelectionRank { get; init; }
}

public sealed record DecisionFlipPointPersistence(
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

public sealed record DecisionEventPersistence(
    Guid DecisionEventId,
    Guid? ParentDecisionEventId,
    int SequenceNumber,
    string EventType,
    string? StageCode,
    string? PayloadJson,
    string? ProvenanceJson);

public sealed record DecisionOutputClaimProvenancePersistence(
    Guid OutputClaimProvenanceId,
    Guid DecisionSessionId,
    Guid ClaimId,
    Guid? SourceBranchId,
    Guid? SourceCandidateId,
    Guid? DecisionEvidenceId,
    Guid? DecisionEvidenceAttachmentId,
    Guid? DecisionEvidenceVerificationId,
    Guid? SourceSnapshotId,
    string? PassageRef,
    string ClaimText,
    bool IsMaterial,
    string MappingStateCode,
    Guid? SourcePropositionId,
    string? MappingReasonCode,
    string DispositionCode,
    Guid TenantId,
    Guid? ActorUserId);

// ── POLOXI Legal V2 dependency-graph persistence snapshots ──────────────────────────────────────

// A typed graph node persisted into its kind-specific table (Kind selects the table).
public sealed record DecisionGraphNodePersistence(
    Guid NodeId,
    string NodeKind,
    string NodeCode,
    string DisplayName,
    string? Statement,
    decimal Support,
    bool IsEssential,
    bool IsSatisfied,
    string VerificationStatus,
    int SortOrder)
{
    // Optional kind-specific attributes (persisted only when relevant to the node's table).
    public string? AuthorityRef { get; init; }
    public string? BurdenedParty { get; init; }
    public string? StandardOfProof { get; init; }
    public Guid? CandidateId { get; init; }
    public bool IsDispositive { get; init; }

    // V2.1 lineage (0220): the authoritative POLOXI object this node derives from. Enables the
    // closed loop to map a graph change to the exact branch/candidate WITHOUT string matching.
    public Guid? SourceBranchId { get; init; }
    public Guid? SourceCandidateId { get; init; }
    public Guid? SourceEvidenceId { get; init; }
    public string? SourceAuthorityId { get; init; }
    public Guid? MatterId { get; init; }
    public int RowVersionNo { get; init; } = 1;
}

public sealed record DecisionGraphEdgePersistence(
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
    string? PropagatedStateCode)
{
    // V2.1 lineage + propagation policy (0220).
    public Guid? SourceBranchId { get; init; }
    public Guid? SourceCandidateId { get; init; }
    public Guid? SourceEvidenceId { get; init; }
    public string? SourceAuthorityId { get; init; }
    public Guid? MatterId { get; init; }
    public bool AlternativePathAllowed { get; init; }
    public string PropagationPolicy { get; init; } = "DEPENDENCY";
    public int RowVersionNo { get; init; } = 1;
}

public sealed record DecisionLosingSideTestPersistence(
    Guid DecisionLosingSideTestId,
    Guid? WinnerCandidateId,
    Guid? ChallengerCandidateId,
    string? StrongestCaseSummary,
    decimal ChallengerStrength,
    decimal WinnerStrength,
    bool WinnerSurvived);

// Full V2 graph snapshot for a session (persisted together, retrieved together).
public sealed record DecisionGraphPersistence(
    Guid DecisionSessionId,
    Guid TenantId,
    Guid? ActorUserId,
    bool ReadinessSatisfied,
    string? ReadinessBlockersJson,
    IReadOnlyCollection<DecisionGraphNodePersistence> Nodes,
    IReadOnlyCollection<DecisionGraphEdgePersistence> Edges,
    DecisionLosingSideTestPersistence? LosingSideTest);
