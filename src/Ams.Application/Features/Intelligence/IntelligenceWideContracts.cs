using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Intelligence;

// Contracts for the isolated "Intelligent Search Wide" dynamic progressive disambiguation pipeline.
// Pipeline: Ambiguous Intent -> Dynamic LLM Hierarchy -> Progressive Disambiguation -> Enterprise Grounding
//           -> Candidate Elimination -> Confidence -> Verified Answer / Governed Action.

public sealed record WideSearchRequest(Guid TenantId,Guid UserId,[Required,StringLength(1000,MinimumLength=2)]string Query,[Range(1,100)]int MaximumResults=25,[Required,StringLength(120)]string CorrelationId="")
{
    public IReadOnlyCollection<string> GrantedPermissions{get;init;}=[];
    // 'EPH Engine' filter: true runs the full dynamic disambiguation + enterprise grounding pipeline;
    // false returns a pure LLM answer without hierarchy, grounding, or elimination.
    public bool UseEphEngine{get;init;}=true;
}

public sealed record WideBranchDto(Guid WideBranchId,Guid? ParentWideBranchId,int LevelNumber,string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,string GroundingStatusCode,int EvidenceCount,decimal Confidence,bool ContinueNarrowing,string? StopReason,bool IsEliminated,string? EliminationReason,int SortOrder)
{
    // V2.1 branch lifecycle state: ACTIVE, SECONDARY, DORMANT, or PRUNED (constraint violation only).
    public string BranchStateCode{get;init;}=WideBranchStates.Active;
    // V2.3 semantic type: ALTERNATIVE (mutually exclusive competing interpretation, entropy-eligible)
    // or DIMENSION (jointly valid criterion; excluded from winner-take-all entropy).
    public string SemanticTypeCode{get;init;}=WideBranchSemanticTypes.Alternative;
    // Three-score model: what the LLM initially thought, what evidence supports, and what EPH concludes.
    public decimal InterpretationPrior{get;init;}
    public decimal EvidenceSupport{get;init;}
    public decimal EphConfidence{get;init;}
}

// V2.1 branch lifecycle states. PRUNED is reserved for hard-constraint violations, explicit
// contradictions, or structurally invalid branches; lacking enterprise evidence or a low
// interpretation prior demotes a branch to SECONDARY/DORMANT instead of eliminating it.
public static class WideBranchStates
{
    public const string Active="ACTIVE";
    public const string Secondary="SECONDARY";
    public const string Dormant="DORMANT";
    public const string Pruned="PRUNED";
}

// V2.3 semantic branch types. ALTERNATIVE branches compete (only one primary interpretation is
// expected to win) and are the correct domain for Shannon entropy. DIMENSION branches are
// complementary criteria that can all be simultaneously true; they feed the Candidate x Dimension
// matrix and are scored on importance/evidence coverage, never winner-take-all entropy.
public static class WideBranchSemanticTypes
{
    public const string Alternative="ALTERNATIVE";
    public const string Dimension="DIMENSION";
}

// V2.3 entropy basis: which belief distribution uncertainty was measured over.
// BRANCH = competing ALTERNATIVE interpretation branches; CANDIDATE = the deterministic
// candidate-signal distribution (used when the hierarchy is dimension-dominated, so Information
// Gain targets "which candidate wins" instead of "which dimension wins").
public static class WideEntropyBases
{
    public const string Branch="BRANCH";
    public const string Candidate="CANDIDATE";
}

public sealed record WideSearchResponse(Guid WideExecutionId,string Query,string StatusCode,string TerminationReasonCode,int DepthReached,int LlmCallCount,decimal FinalConfidence,string AnswerVerificationCode,string? FinalAnswer,IReadOnlyCollection<WideBranchDto> Branches,IReadOnlyCollection<EphEvidenceDto> Evidence,IReadOnlyCollection<WideActionSuggestionDto> SuggestedActions,long DurationMilliseconds)
{
    // Real-world references produced by the LLM from the top interpretive narrowing paths.
    // Displayed in Authorized Evidence with links to the actual external sites; never enterprise-verified.
    public IReadOnlyCollection<WideExternalReferenceDto> ExternalReferences{get;init;}=[];
    // Actual LLM-answered result sets for the top interpretive narrowing paths (branch sub-header text fed
    // back to the LLM). Displayed in Authorized Evidence as complete result sets, ordered by branch scoring;
    // never enterprise-verified.
    public IReadOnlyCollection<WideInterpretiveResultDto> InterpretiveResults{get;init;}=[];
    // Fresh external snippets that grounded the answer (Stage 2.5); empty when live grounding is disabled.
    public IReadOnlyCollection<WideExternalKnowledgeSnippet> ExternalKnowledge{get;init;}=[];
    // V2.1: query contract extracted before hierarchy generation (constraints vs ambiguities vs output shape).
    public WideQueryContract? QueryContract{get;init;}
    // V2.1: cross-branch candidate competition results (composite ranking honoring hard constraints).
    public IReadOnlyCollection<WideCandidateDto> Candidates{get;init;}=[];
    // V2.1: share of surviving branches supported by at least one evidence item (external or enterprise).
    public decimal EvidenceCoverage{get;init;}
    // V2.5 Decision Evidence Coverage: share of the branches that actually drive the final
    // Candidate × Branch competition that are supported by evidence. Deep hierarchy leaves without
    // evidence should not drag down confidence in an answer whose DECIDING dimensions are grounded.
    public decimal DecisionEvidenceCoverage{get;init;}
    public int ExternalEvidenceCount{get;init;}
    public int EnterpriseEvidenceCount{get;init;}
    // V2.2: deterministic Shannon entropy over ACTIVE/SECONDARY branches (bits) measured at start and end.
    public decimal InitialEntropy{get;init;}
    public decimal FinalEntropy{get;init;}
    public decimal InitialNormalizedEntropy{get;init;}
    public decimal FinalNormalizedEntropy{get;init;}
    // V2.2: measured (never LLM-estimated) total uncertainty reduction across information rounds.
    public decimal TotalActualInformationGain{get;init;}
    // V2.3: which distribution execution-level entropy was measured over (BRANCH or CANDIDATE).
    public string EntropyBasisCode{get;init;}=WideEntropyBases.Branch;
    // V2.2: audit of every information-directed exploration round executed.
    public IReadOnlyCollection<WideInformationRoundDto> InformationRounds{get;init;}=[];
}

// V2.1 Query Contract: separates hard constraints from ambiguous concepts so EPH only branches ambiguity.
public sealed record WideQueryContract(string? EntityType,string? GeographicConstraint,int? RequestedCount,string? RankingConcept,IReadOnlyCollection<string> HardConstraints,IReadOnlyCollection<string> AmbiguousConcepts,IReadOnlyCollection<string> OutputRequirements);

// V2.1 candidate competition: a candidate with its composite score and per-branch evidence scores.
public sealed record WideCandidateDto(Guid WideCandidateId,int RankNumber,string DisplayName,string? Detail,decimal CompositeScore,IReadOnlyCollection<WideCandidateBranchScoreDto> BranchScores)
{
    // Share of surviving interpretation dimensions this candidate has evidence scores for.
    // Low coverage means the candidate may look strong only because data is missing.
    public decimal EvidenceCoverage{get;init;}
    // True when the candidate failed a hard query constraint and was ruled out (kept visible, never hidden).
    public bool IsConstraintViolation{get;init;}
}

public sealed record WideCandidateBranchScoreDto(string BranchDisplayName,decimal EvidenceScore);

public sealed record WideExternalReferenceDto(string Title,string Url,string Source,string Summary,string BranchDisplayName);

public sealed record WideInterpretiveResultDto(string BranchDisplayName,string Interpretation,decimal Confidence,IReadOnlyCollection<WideInterpretiveResultItemDto> Items)
{
    // STABLE: durable knowledge. TIME_SENSITIVE: prices, rates, rankings, availability - figures may be outdated.
    public string DataVolatility{get;init;}="STABLE";
    // True when the result set was composed from live external retrieval (Stage 2.5), not LLM recall.
    public bool IsExternallyGrounded{get;init;}
}

public sealed record WideInterpretiveResultItemDto(int RankNumber,string Name,string Detail);

public sealed record WideActionSuggestionDto(string DisplayName,string NavigationRoute,string Rationale);

// Wide pipeline configuration loaded from Core.ConfigurationSetting (DB is the source of truth).
public sealed record WideConfiguration(decimal TargetConfidence,decimal MinimumBranchConfidence,int MaximumBranchesPerLevel,int AbsoluteDepthCeiling,int MaximumTotalLlmCalls)
{
    // V2.1 thresholds and weights (DB-seeded; see migration 0146).
    public decimal SecondaryBranchThreshold{get;init;}=.35m;
    public decimal DormantBranchThreshold{get;init;}=.20m;
    public decimal PriorWeight{get;init;}=.30m;
    public decimal EvidenceWeight{get;init;}=.70m;
    public int MaximumCandidates{get;init;}=10;
    public bool EnableQueryContract{get;init;}=true;
    // Bounded parallelism (DB-seeded; see migration 0147). 1 disables parallel execution.
    public int GroundingConcurrency{get;init;}=4;
    public int ExternalRetrievalConcurrency{get;init;}=3;
    // V2.2 information-directed exploration (DB-seeded; see migration 0149). Fail-soft: any
    // estimator/entropy failure skips the information round and continues V2.1 narrowing.
    public bool EnableInformationValue{get;init;}=true;
    public decimal InformationValueTriggerEntropy{get;init;}=.45m;
    public int MaximumInformationRounds{get;init;}=3;
    public int MaximumInformationTargetsPerRound{get;init;}=2;
    public decimal MinimumInformationValue{get;init;}=.55m;
    public decimal MinimumActualInformationGain{get;init;}=.05m;
    public int InformationNoProgressRounds{get;init;}=2;
    public decimal InformationValueLlmWeight{get;init;}=.60m;
    public decimal InformationValueEvidenceGapWeight{get;init;}=.15m;
    public decimal InformationValueBranchWeight{get;init;}=.15m;
    public decimal InformationValueCandidateNeedWeight{get;init;}=.10m;
    // Deterministic values for LLM categorical judgments (VERY_LOW..VERY_HIGH).
    public decimal VeryLowInformationValue{get;init;}=.20m;
    public decimal LowInformationValue{get;init;}=.40m;
    public decimal MediumInformationValue{get;init;}=.60m;
    public decimal HighInformationValue{get;init;}=.80m;
    public decimal VeryHighInformationValue{get;init;}=1.00m;
    // V2.3 evidence-priority expansion guard and candidate admission (DB-seeded; see migration 0150).
    public int EvidencePriorityMinimumDepth{get;init;}=4;
    public decimal EvidencePriorityCoverageFloor{get;init;}=.35m;
    public int MinimumCandidateDimensionSupport{get;init;}=2;
}

// Stage 2.5 external grounding configuration loaded from Core.ConfigurationSetting (DB is the source of truth).
// A blank ApiKey or Enabled=false disables live retrieval; the pipeline degrades to interpretive-only answers.
public sealed record WideExternalGroundingConfiguration(bool Enabled,string ProviderCode,string ApiKey,int MaximumQueriesPerExecution,int MaximumSnippetsPerQuery,int CacheHours,int TimeoutSeconds);

// A fresh real-world snippet retrieved at answer time (live provider call or EPH.ExternalKnowledge cache hit).
public sealed record WideExternalKnowledgeSnippet(string Query,string Title,string Url,string Snippet,decimal Score,DateTime RetrievedDateUtc);

// LLM structured outputs (strict JSON schema payloads).
public sealed record WideProposedBranch(string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,decimal Confidence,bool ContinueNarrowing,string? StopReason,string? ParentBranchCode)
{
    // V2.3: ALTERNATIVE or DIMENSION; anything else defaults to ALTERNATIVE for backward compatibility.
    public string? SemanticType{get;init;}
}

public sealed record WideIntentProposal(string ConceptCode,string DisplayName,decimal AmbiguityScore,IReadOnlyCollection<WideProposedBranch> Branches);

public sealed record WideLevelProposal(IReadOnlyCollection<WideProposedBranch> Branches);

public sealed record WideAnswerProposal(string Answer,string VerificationCode,decimal Confidence,IReadOnlyCollection<WideAnswerAction> SuggestedActions,IReadOnlyCollection<int> RelevantEvidenceNumbers)
{
    public IReadOnlyCollection<WideExternalReference> ExternalReferences{get;init;}=[];
    public IReadOnlyCollection<WideInterpretiveResult> InterpretiveResults{get;init;}=[];
}

public sealed record WideExternalReference(string Title,string Url,string Source,string Summary,string BranchDisplayName);

public sealed record WideInterpretiveResult(string BranchDisplayName,string Interpretation,IReadOnlyCollection<WideInterpretiveResultItem> Items)
{
    public string DataVolatility{get;init;}="STABLE";
}

public sealed record WideInterpretiveResultItem(int RankNumber,string Name,string Detail);

public sealed record WideAnswerAction(string DisplayName,string NavigationRoute,string Rationale);

// V2.1 LLM structured outputs.
public sealed record WideQueryContractProposal(string? EntityType,string? GeographicConstraint,int? RequestedCount,string? RankingConcept,IReadOnlyCollection<string> HardConstraints,IReadOnlyCollection<string> AmbiguousConcepts,IReadOnlyCollection<string> OutputRequirements);

public sealed record WideCandidateScoringProposal(IReadOnlyCollection<WideCandidateScore> Candidates);

public sealed record WideCandidateScore(string Name,string? Detail,bool ViolatesConstraint,string? ConstraintViolationReason,IReadOnlyCollection<WideCandidateBranchEvidence> BranchScores);

public sealed record WideCandidateBranchEvidence(string BranchDisplayName,decimal EvidenceScore);

// Persistence records.
public sealed record WideExecutionStart(Guid TenantId,Guid UserId,string QueryText,string CorrelationId);

public sealed record WideBranchRecord(Guid WideBranchId,Guid WideExecutionId,Guid? ParentWideBranchId,Guid TenantId,int LevelNumber,string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,string GroundingStatusCode,int EvidenceCount,decimal Confidence,bool ContinueNarrowing,string? StopReason,bool IsEliminated,string? EliminationReason,int SortOrder)
{
    public string BranchStateCode{get;init;}=WideBranchStates.Active;
    public string SemanticTypeCode{get;init;}=WideBranchSemanticTypes.Alternative;
    public decimal InterpretationPrior{get;init;}
    public decimal EvidenceSupport{get;init;}
    public decimal EphConfidence{get;init;}
}

public sealed record WideCandidateRecord(Guid WideCandidateId,Guid WideExecutionId,Guid TenantId,string DisplayName,string? Detail,decimal CompositeScore,int RankNumber,bool IsConstraintViolation,string? ConstraintViolationReason,IReadOnlyCollection<WideCandidateBranchScoreRecord> BranchScores);

public sealed record WideCandidateBranchScoreRecord(Guid WideCandidateBranchScoreId,Guid WideCandidateId,Guid WideBranchId,Guid TenantId,string BranchDisplayName,decimal EvidenceScore);

// Batch persistence rows (one round trip per level/phase instead of per branch).
public sealed record WideBranchOutcomeUpdate(Guid WideBranchId,string GroundingStatusCode,int EvidenceCount,bool IsEliminated,string? EliminationReason);

public sealed record WideBranchScoreUpdate(Guid WideBranchId,string BranchStateCode,decimal InterpretationPrior,decimal EvidenceSupport,decimal EphConfidence);

// ── V2.2 Information-Directed Exploration ──────────────────────────────────────
// Terminology is deliberate: EstimatedInformationValue is the LLM-assisted PREDICTION of how
// valuable an investigation is likely to be; ActualInformationGain is the mathematically MEASURED
// entropy reduction after evidence arrives. They are never interchangeable.

// Allowed categorical judgment values returned by the batched Information Value estimator.
public static class WideInformationCategories
{
    public const string VeryLow="VERY_LOW";
    public const string Low="LOW";
    public const string Medium="MEDIUM";
    public const string High="HIGH";
    public const string VeryHigh="VERY_HIGH";
    public static readonly IReadOnlyCollection<string> All=[VeryLow,Low,Medium,High,VeryHigh];
}

// Deterministic Shannon entropy over the eligible (ACTIVE/SECONDARY) branch belief distribution.
public sealed record WideEntropyResult(decimal Entropy,decimal MaximumEntropy,decimal NormalizedEntropy,int EligibleBranchCount)
{
    // V2.3: which distribution this uncertainty was measured over (BRANCH or CANDIDATE).
    public string EntropyBasisCode{get;init;}=WideEntropyBases.Branch;
}

// Response DTO: one information round with its measured before/after uncertainty and targets.
public sealed record WideInformationRoundDto(int RoundNumber,decimal EntropyBefore,decimal NormalizedEntropyBefore,decimal? EntropyAfter,decimal? NormalizedEntropyAfter,decimal? ActualInformationGain,decimal? RawEntropyDelta,IReadOnlyCollection<WideInformationTargetDto> Targets)
{
    // V2.3: which distribution this round's uncertainty was measured over (BRANCH or CANDIDATE).
    public string EntropyBasisCode{get;init;}=WideEntropyBases.Branch;
    // V2.5 entropy audit: max entropy (log2 N, bits) and measured population size before/after,
    // so a "0 bits" round is distinguishable from rounding or a population change.
    public decimal? MaxEntropyBefore{get;init;}
    public decimal? MaxEntropyAfter{get;init;}
    public int? PopulationCountBefore{get;init;}
    public int? PopulationCountAfter{get;init;}
}

public sealed record WideInformationTargetDto(string BranchDisplayName,string UncertaintyCode,string RankingImpactCode,string CandidateDiscriminationCode,string EvidenceAvailabilityCode,string NoveltyCode,string RedundancyCode,decimal RawEstimatedInformationValue,decimal AdjustedInformationValue,bool WasSelected,int? SelectionRank,string? EvidenceTarget,string? Rationale);

// LLM structured output for the single batched Information Value estimation call.
public sealed record WideInformationValueProposal(IReadOnlyCollection<WideInformationTargetProposal> Targets);

public sealed record WideInformationTargetProposal(string BranchCode,string Uncertainty,string RankingImpact,string CandidateDiscrimination,string EvidenceAvailability,string Novelty,string Redundancy,string? EvidenceTarget,string Rationale)
{
    // Falsifiable predictions: which current candidates should move, and in what direction/magnitude,
    // if this branch is investigated. EPH later scores direction/magnitude accuracy against reality.
    public IReadOnlyCollection<WideRankingChangePrediction> PredictedRankingChanges{get;init;}=[];
}

public sealed record WideRankingChangePrediction(string Candidate,string Direction,string Magnitude);

// Persistence records (EPH.WideInformationRound / Target / Prediction; see migration 0149).
public sealed record WideInformationRoundRecord(Guid WideInformationRoundId,Guid WideExecutionId,Guid TenantId,int RoundNumber,decimal EntropyBefore,decimal NormalizedEntropyBefore,DateTime StartedDateUtc)
{
    public string EntropyBasisCode{get;init;}=WideEntropyBases.Branch;
    public decimal? EntropyAfter{get;init;}
    public decimal? NormalizedEntropyAfter{get;init;}
    public decimal? ActualInformationGain{get;init;}
    public decimal? RawEntropyDelta{get;init;}
    public int SelectedTargetCount{get;init;}
    public DateTime? CompletedDateUtc{get;init;}
    // V2.5 entropy audit: max entropy (log2 N, bits) and measured population size before/after.
    public decimal? MaxEntropyBefore{get;init;}
    public decimal? MaxEntropyAfter{get;init;}
    public int? PopulationCountBefore{get;init;}
    public int? PopulationCountAfter{get;init;}
}

public sealed record WideInformationTargetRecord(Guid WideInformationTargetId,Guid WideInformationRoundId,Guid WideBranchId,Guid TenantId,string UncertaintyCode,string RankingImpactCode,string CandidateDiscriminationCode,string EvidenceAvailabilityCode,string NoveltyCode,string RedundancyCode,decimal RawEstimatedInformationValue,decimal AdjustedInformationValue,bool WasSelected,int? SelectionRank,string? EvidenceTarget,string? Rationale)
{
    public decimal? CalibrationFactor{get;init;}
    public decimal? ExpectedRetrievalCost{get;init;}
    public decimal? InformationValuePerCost{get;init;}
    public int PredictedRankingImpactCount{get;init;}
    public int PredictedUpCount{get;init;}
    public int PredictedDownCount{get;init;}
    public decimal? DirectionAccuracy{get;init;}
    public decimal? MagnitudeAccuracy{get;init;}
}

public sealed record WideInformationPredictionRecord(Guid WideInformationPredictionId,Guid WideInformationTargetId,Guid TenantId,string CandidateName,string PredictedDirection,string PredictedMagnitude)
{
    public decimal? ScoreBefore{get;init;}
    public int? RankBefore{get;init;}
    public decimal? ScoreAfter{get;init;}
    public int? RankAfter{get;init;}
    public string? ActualDirection{get;init;}
    public string? ActualMagnitude{get;init;}
    public bool? DirectionCorrect{get;init;}
    public bool? MagnitudeCorrect{get;init;}
}

// Execution-level entropy summary persisted at completion (EPH.WideExecution V2.2 columns).
public sealed record WideExecutionEntropyUpdate(Guid WideExecutionId,decimal? InitialEntropy,decimal? FinalEntropy,decimal? InitialNormalizedEntropy,decimal? FinalNormalizedEntropy,decimal? TotalActualInformationGain,int InformationRoundCount,int InformationTargetCount,int InformationRetrievalCount)
{
    public string? EntropyBasisCode{get;init;}
}
