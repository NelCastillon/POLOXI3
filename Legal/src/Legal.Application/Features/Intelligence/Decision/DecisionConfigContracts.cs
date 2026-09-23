namespace Legal.Application.Features.Intelligence.Decision;

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
    bool ApplyVerifiedSignalsToRanking = false);

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
    bool IsEliminated);

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
}

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
