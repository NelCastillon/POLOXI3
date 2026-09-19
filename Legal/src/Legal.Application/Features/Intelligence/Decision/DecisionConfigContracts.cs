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
    int MaxCandidates);

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
    int SortOrder);

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
    string VerificationStatus);

public sealed record DecisionFlipPointPersistence(
    Guid DecisionFlipPointId,
    Guid? DecisionBranchId,
    string Description,
    decimal ChangeCost,
    bool WinnerChanges,
    int RankDelta);

public sealed record DecisionEventPersistence(
    Guid DecisionEventId,
    Guid? ParentDecisionEventId,
    int SequenceNumber,
    string EventType,
    string? StageCode,
    string? PayloadJson,
    string? ProvenanceJson);

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
