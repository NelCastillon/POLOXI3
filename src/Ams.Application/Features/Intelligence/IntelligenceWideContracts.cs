using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Intelligence;

// Contracts for the isolated "Intelligent Search Wide" dynamic progressive disambiguation pipeline.
// Pipeline: Ambiguous Intent -> Dynamic LLM Hierarchy -> Progressive Disambiguation -> Enterprise Grounding
//           -> Candidate Elimination -> Confidence -> Verified Answer / Governed Action.

public sealed record WideSearchRequest(Guid TenantId,Guid UserId,[Required,StringLength(4000,MinimumLength=2)]string Query,[Range(1,100)]int MaximumResults=25,[Required,StringLength(120)]string CorrelationId="")
{
    public IReadOnlyCollection<string> GrantedPermissions{get;init;}=[];
    // 'POLOXI Engine' filter: true runs the full dynamic disambiguation + enterprise grounding pipeline;
    // false returns a pure LLM answer without hierarchy, grounding, or elimination.
    public bool UsePoloxiEngine{get;init;}=true;
    // V2.8 Clarification continuation: when the previous execution ended USER_CLARIFICATION_REQUIRED,
    // the follow-up request carries the user's clarification answer plus the target it answers.
    // The pipeline treats the answer as an added hard constraint and reweights — it never restarts blind.
    [StringLength(500)]public string? ClarificationAnswer{get;init;}
    [StringLength(300)]public string? ClarificationTarget{get;init;}
    // V2.8.5 Clarification Calibration: the continuation carries which ask/answer round this is
    // (0 = original question) and the intent entropy measured BEFORE the user's answer, so the
    // service can compute ClarificationGain = prior − current and enforce the round cap and gain
    // floor deterministically without server-side session state.
    [Range(0,10)]public int ClarificationRound{get;init;}
    [Range(0,1)]public decimal? PriorIntentEntropy{get;init;}
    // V3.2.3 AnswerKind carry-forward: the clarification answer is a parameter fill for the ORIGINAL
    // task, not a new question — the continuation inherits the original run's AnswerKind instead of
    // re-classifying the answer-polluted text (which reads like SINGLE_ANSWER/CONTENT_ENUMERATION).
    [StringLength(30)]public string? OriginalAnswerKind{get;init;}
    // V3.4 server-side continuation state: when set, the epistemic chain (original query, round,
    // prior intent entropy, answer kind, clarification target) is derived from the persisted parent
    // execution row - the client-carried fields above become legacy fallbacks only. This is the
    // continuation token for API productization: tamper-proof, tenant-scoped, replayable.
    public Guid? ParentWideExecutionId{get;init;}
    // Model selection: null/empty = Auto (feature-policy primary/fallback routing); otherwise an
    // active CHAT AI.ModelDeployment.ModelCode the pipeline routes every LLM call through.
    [StringLength(100)]public string? ModelCode{get;init;}
}

// Database-backed model option for the wide-search Model dropdown (active CHAT deployments).
public sealed record WideModelOptionDto(string ModelCode,string DeploymentName,string? ModelFamily);

// V3.4: continuation state loaded server-side from POLOXI.WideExecution (tenant-scoped).
// Null when the parent id does not exist for the tenant - the service falls back to client fields.
public sealed record WideContinuationState(Guid WideExecutionId,string QueryText,int ClarificationRound,decimal? IntentEntropy,string? AnswerKindCode,string? ClarificationTarget);

public sealed record WideBranchDto(Guid WideBranchId,Guid? ParentWideBranchId,int LevelNumber,string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,string GroundingStatusCode,int EvidenceCount,decimal Confidence,bool ContinueNarrowing,string? StopReason,bool IsEliminated,string? EliminationReason,int SortOrder)
{
    // V2.1 branch lifecycle state: ACTIVE, SECONDARY, DORMANT, or PRUNED (constraint violation only).
    public string BranchStateCode{get;init;}=WideBranchStates.Active;
    // V2.3 semantic type: ALTERNATIVE (mutually exclusive competing interpretation, entropy-eligible)
    // or DIMENSION (jointly valid criterion; excluded from winner-take-all entropy).
    public string SemanticTypeCode{get;init;}=WideBranchSemanticTypes.Alternative;
    // Three-score model: what the LLM initially thought, what evidence supports, and what POLOXI concludes.
    public decimal InterpretationPrior{get;init;}
    public decimal EvidenceSupport{get;init;}
    public decimal PoloxiConfidence{get;init;}
}

// V2.1 branch lifecycle states. PRUNED is reserved for hard-constraint violations, explicit
// contradictions, or structurally invalid branches; lacking enterprise evidence or a low
// interpretation prior demotes a branch to SECONDARY/DORMANT instead of eliminating it.
// V3.0 adds RESOLVED: an evidence-settled branch removed from further INVESTIGATION attention
// (it stays in the answer path with its final scores). Reversible: a material evidence-support
// change reopens it — "reversible uncertainty, irreversible invalidation".
public static class WideBranchStates
{
    public const string Active="ACTIVE";
    public const string Secondary="SECONDARY";
    public const string Dormant="DORMANT";
    public const string Pruned="PRUNED";
    public const string Resolved="RESOLVED";
}

// ── V3.0 Evidence-Guided Adaptive Narrowing ────────────────────────────────────
// POLOXI's default behavior is NARROW: each useful iteration should shrink the reasoning space
// (branches), the candidate space, and unresolved uncertainty. Expansion is permitted ONLY when
// newly grounded evidence demonstrates the current space may be incomplete (discovery admission
// gate + per-round budget), after which narrowing resumes. All decisions are deterministic — the
// LLM proposes; evidence validates; the POLOXI Narrowing Policy decides.

// Per-round directional trend of the reasoning/candidate spaces.
public static class WideNarrowingTrends
{
    public const string Narrowing="NARROWING";   // spaces shrank or uncertainty fell
    public const string Stable="STABLE";         // no material change
    public const string Expansion="EXPANSION";   // evidence-justified admissions grew a space
    public const string Reopened="REOPENED";     // a resolved branch was reopened by changed evidence
    public const string Converged="CONVERGED";   // uncertainty below trigger — no further rounds needed
}

// V3.0 candidate narrowing states. ELIMINATED is soft (evidence currently uncompetitive — can
// return); INVALIDATED is hard (constraint violation — terminal). A candidate is NEVER deferred
// or eliminated solely because evidence is missing: missing evidence ⇒ WATCH ⇒ investigate.
public static class WideCandidateStates
{
    public const string Active="ACTIVE";
    public const string Watch="WATCH";
    public const string Deferred="DEFERRED";
    public const string Eliminated="ELIMINATED";
    public const string Invalidated="INVALIDATED";
    public const string Admitted="ADMITTED";
    public const string NewlyDiscovered="NEWLY_DISCOVERED";
    public const string DiscoveredNotAdmitted="DISCOVERED_NOT_ADMITTED";
}

// One provenance-preserving narrowing state transition (branch or candidate) with its reason.
public sealed record WideNarrowingTransitionDto(string SubjectTypeCode,string SubjectName,string PreviousStateCode,string NewStateCode,string Reason);

// One narrowing evaluation: before/after space sizes, uncertainty, measured gain, trend, and the
// individual audited transitions. Rendered by the UI and persisted for audit (never deleted).
public sealed record WideNarrowingIterationDto(int RoundNumber,string TrendCode,int ActiveBranchCountBefore,int ActiveBranchCountAfter,int CandidateCountBefore,int CandidateCountAfter,decimal NormalizedEntropyBefore,decimal? NormalizedEntropyAfter,decimal? ActualInformationGain,IReadOnlyCollection<WideNarrowingTransitionDto> Transitions)
{
    // Candidates admitted this round through the discovery gate (joined the NEXT round's entropy basis).
    public int AdmittedCandidateCount{get;init;}
    // Names discovered but NOT admitted (insufficient discovery evidence or budget) — disclosed, never hidden.
    public int DiscoveredNotAdmittedCount{get;init;}
    // Branches marked RESOLVED (settled) and branches reopened by changed evidence this round.
    public int ResolvedBranchCount{get;init;}
    public int ReopenedBranchCount{get;init;}
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

// V3.12 explicit branch semantic ROLE, assigned by the hierarchy LLM at proposal time (the same
// LLM-proposes/POLOXI-enforces pattern as SemanticTypeCode). Deterministic scoring math acts on
// the role; keyword heuristics remain only as a fallback when the role is absent/unknown.
//   HARD_CONSTRAINT → failure can invalidate a candidate (never scored as a preference)
//   GUARDRAIL       → weak performance penalizes the composite (veto-style, non-compensatory)
//   PREFERENCE      → higher score improves the candidate (ordinary compensatory criterion)
//   CONTEXT         → does not directly score candidates (process/reasoning/meta branches)
public static class WideBranchRoles
{
    public const string HardConstraint="HARD_CONSTRAINT";
    public const string Guardrail="GUARDRAIL";
    public const string Preference="PREFERENCE";
    public const string Context="CONTEXT";
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

// Async start+poll transport for long-running wide searches: the API starts the pipeline on a
// background task and the client polls for completion, so no HTTP request outlives the pipeline.
// Transport-only — POLOXI itself runs unchanged.
public sealed record WideSearchOperationStartResponse(Guid OperationId);

public sealed record WideSearchOperationStatusResponse(Guid OperationId,string StatusCode,WideSearchResponse? Response,string? ErrorMessage);

// V3.12 P0 stage telemetry: per-LLM-stage wall time and token counts, collected in-memory during
// the run and disclosed on the response so latency regressions are visible without querying
// AI.Execution by hand. Diagnostic only — never feeds any scoring or routing decision.
public sealed record WideStageTimingDto(string StageCode,long DurationMilliseconds,int InputTokenCount,int OutputTokenCount,string? ModelCode);

public sealed record WideSearchResponse(Guid WideExecutionId,string Query,string StatusCode,string TerminationReasonCode,int DepthReached,int LlmCallCount,decimal FinalConfidence,string AnswerVerificationCode,string? FinalAnswer,IReadOnlyCollection<WideBranchDto> Branches,IReadOnlyCollection<PoloxiEvidenceDto> Evidence,IReadOnlyCollection<WideActionSuggestionDto> SuggestedActions,long DurationMilliseconds)
{
    // V3.12 P0: per-stage LLM latency/token disclosure (diagnostic only).
    public IReadOnlyCollection<WideStageTimingDto> StageTimings{get;init;}=[];
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
    // V3.2: the governing AnswerKind classification (ENTITY_RANKING / CONTENT_ENUMERATION / SINGLE_ANSWER)
    // and whether kind-aware budget routing actually tuned this execution's workflow.
    public string? AnswerKindCode{get;init;}
    public bool AnswerKindRoutingApplied{get;init;}
    // Actual provider/model that composed the final answer (resolved by the router: explicit override or Auto route).
    public string? ProviderCodeUsed{get;init;}
    public string? ModelCodeUsed{get;init;}
    // Raw first LLM result: the ranked list the selected model returns for the PLAIN query in a single
    // shot — exactly as if the user prompted the model's own chat interface. Captured in parallel with
    // the POLOXI pipeline for comparison only; never evidence-weighted, filtered, or competed.
    public IReadOnlyCollection<WideInterpretiveResultItemDto> LlmRawItems{get;init;}=[];
    // V2.1: cross-branch candidate competition results (composite ranking honoring hard constraints).
    public IReadOnlyCollection<WideCandidateDto> Candidates{get;init;}=[];
    // V3.15 Grouped ambiguity execution: L1 interpretation groups with their own branch/result/evidence
    // projection. This preserves the full POLOXI run while preventing unrelated meanings from being
    // visually merged into one result universe.
    public IReadOnlyCollection<WideAmbiguityGroupDto> AmbiguityGroups{get;init;}=[];
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
    // V2.6 Candidate Stability: deterministic convergence signals measured across information rounds.
    // WinnerStability = share of measurement points where the same candidate led the deterministic
    // signal ranking; TopKStability = average overlap of the top-K set between consecutive rounds.
    // 1.0 when fewer than two measurement points exist (neutral, never penalizing).
    public decimal? WinnerStability{get;init;}
    public decimal? TopKStability{get;init;}
    // V2.6 Decision Confidence: how confident POLOXI is in the FINAL RANKING — blends decision evidence
    // coverage, top-candidate separation, winner stability, and answer confidence. Replaces
    // hierarchy-coverage-dominated confidence as the user-facing confidence for wide searches.
    public decimal? DecisionConfidence{get;init;}
    // V2.8 Clarification Gate: when POLOXI cannot responsibly resolve the ambiguity (compound gate:
    // low decision confidence AND unstable winner AND thin candidate margin AND a high-value
    // unresolved information target), it returns USER_CLARIFICATION_REQUIRED with a deterministic
    // clarification question instead of pretending certainty. Options are rendered from the top
    // competing candidates — no extra LLM call.
    public string? ClarificationQuestion{get;init;}
    public string? ClarificationTarget{get;init;}
    public IReadOnlyCollection<string> ClarificationOptions{get;init;}=[];
    // V2.8.4 Clarification Intelligence: recognition-based structured choices — the user recognizes
    // a DESCRIPTION ("business banking / fintech for startups") instead of recalling a legal name.
    // Includes an OTHER escape hatch. Rendered as clickable options; natural-language answers remain
    // fully supported through ClarificationAnswer.
    public IReadOnlyCollection<WideClarificationOptionDto> ClarificationOptionItems{get;init;}=[];
    // V2.8.4 intent telemetry: Shannon entropy (normalized 0..1) over the top candidates' composite
    // scores — the measurable "which one does the USER mean?" uncertainty. Clarification Gain =
    // IntentEntropy(before question) − IntentEntropy(after answer), diffable across the two
    // executions of a clarification round.
    public decimal? IntentEntropy{get;init;}
    // V2.8.4: the winning ClarificationValue (separation × answerability) behind the selected target.
    public decimal? BestClarificationValue{get;init;}
    // V2.8.5 Clarification Gain: PriorIntentEntropy − IntentEntropy — the measured intent-side
    // uncertainty reduction produced by the user's clarification answer. Null on first executions.
    public decimal? ClarificationGain{get;init;}
    // V2.8.5: which ask/answer round produced this execution (0 = original question).
    public int ClarificationRound{get;init;}
    // V2.9 Answer Composer: presentation contract derived DETERMINISTICALLY from the Candidate ×
    // Branch outcome — the UI renders this instead of rediscovering the reasoning. Null when no
    // candidate competition ran (LLM-only mode or zero candidates).
    public WideAnswerContext? AnswerContext{get;init;}
    // V3.0 Evidence-Guided Adaptive Narrowing: per-round deterministic narrowing audit — how the
    // reasoning space, candidate space, and unresolved uncertainty changed, with every state
    // transition and its evidence-based reason. Empty when narrowing is disabled or no rounds ran.
    public IReadOnlyCollection<WideNarrowingIterationDto> NarrowingIterations{get;init;}=[];
    // V3.0: the final directional statement of the run (NARROWING/STABLE/EXPANSION/REOPENED/CONVERGED).
    public string? FinalNarrowingTrend{get;init;}
    // Phase 2a Challenge-the-Winner (WATCH MODE): audit-only adversarial assessment recorded when the
    // top two candidates finished close. NEVER changes the winner, ranking, confidence, or answer.
    // Null when the round is disabled (default), the margin is wide, or the assessment failed soft.
    public WideChallengeOutcomeDto? ChallengeOutcome{get;init;}
}

public sealed record WideAmbiguityGroupDto(Guid RootWideBranchId,string GroupCode,string DisplayName,string Interpretation,decimal Confidence,string? SafetyRiskCode,string? AnswerKindCode,string? CandidateKindCode)
{
    public IReadOnlyCollection<WideBranchDto> Branches{get;init;}=[];
    public IReadOnlyCollection<WideInterpretiveResultDto> InterpretiveResults{get;init;}=[];
    public IReadOnlyCollection<WideCandidateDto> Candidates{get;init;}=[];
    public IReadOnlyCollection<PoloxiEvidenceDto> Evidence{get;init;}=[];
    public IReadOnlyCollection<WideExternalKnowledgeSnippet> ExternalKnowledge{get;init;}=[];
    public string? Summary{get;init;}
}

// Phase 2a Challenge-the-Winner (WATCH MODE): the adversarial assessment outcome. VerdictCode is one
// of WideChallengeVerdicts; Margin is the leader-vs-runner-up composite margin that triggered the
// round. Audit-only: persisted on the execution and returned for display, never acted on.
public sealed record WideChallengeOutcomeDto(string ChallengedCandidate,string ChallengerCandidate,decimal Margin,string VerdictCode,string Rationale,string? SuggestedWinner);

public static class WideChallengeVerdicts
{
    public const string Upheld="UPHELD";                       // challenge found no credible case against the leader
    public const string Weakened="WEAKENED";                   // credible concerns found but not enough to overturn
    public const string OverturnSuggested="OVERTURN_SUGGESTED";// challenge argues the runner-up should win (watch-only; never applied)
}

// V2.9 response modes: the Uncertainty Router controls PRESENTATION, not only reasoning.
public static class WideResponseModes
{
    public const string Answer="ANSWER";                              // decisive winner → direct answer UX
    public const string AnswerWithRefinement="ANSWER_WITH_REFINEMENT";// close ranking → ranking + optional preference UX
    public const string ClarificationRequired="CLARIFICATION_REQUIRED";// intent gap → candidate-choice UX
    public const string LimitedEvidence="LIMITED_EVIDENCE";           // weak grounding → answer + evidence warning UX
}

// V2.9 Answer Composer contract: the structured POLOXI outcome the presentation layer communicates.
// Everything here is computed deterministically from candidates, branch scores, and telemetry —
// the presentation layer never reranks, invents evidence, or resolves uncertainty POLOXI did not.
public sealed record WideAnswerContext(string ResponseMode,string ConfidenceLabel,string ConfidenceNarrative)
{
    // V3.14 renderer contract: the UI/API can choose the correct presentation without re-inferring intent.
    public string? AnswerKindCode{get;init;}
    public string? CandidateKindCode{get;init;}
    public string? OutputShape{get;init;}
    public string? TargetObject{get;init;}
    public IReadOnlyCollection<string> PresentationGuidance{get;init;}=[];
    // The winning candidate's name (deterministic final ranking #1). Null in clarification mode.
    public string? WinnerDisplayName{get;init;}
    // Winner's strongest decision dimensions with scores, best first (why it won).
    public IReadOnlyCollection<WideDimensionScoreDto> WinnerStrengths{get;init;}=[];
    // Winner's weakest decision dimensions with scores (honest trade-offs).
    public IReadOnlyCollection<WideDimensionScoreDto> WinnerWeaknesses{get;init;}=[];
    // Per-candidate ranking-card summaries: best-for dimension and main trade-off dimension.
    public IReadOnlyCollection<WideCandidateSummaryDto> CandidateSummaries{get;init;}=[];
    // Deterministic winner-vs-runner-up contrasts ("why Raleigh beat Charlotte").
    public IReadOnlyCollection<WideCandidateContrastDto> CandidateContrasts{get;init;}=[];
    // Dimensions whose weighting could change the ranking (highest cross-candidate separation) —
    // rendered as "This ranking could change if..." personalization chips.
    public IReadOnlyCollection<string> ChangeableDimensions{get;init;}=[];
    // V2.9.2 Output Contract Validation: deterministic post-competition check that the delivered
    // ranking satisfies the query contract (requested count, entity list size). Never hidden —
    // a shortfall is disclosed instead of silently under-delivering.
    public WideOutputContractResultDto? OutputContract{get;init;}
    // V2.9.2 Single Ranking-Changing Uncertainty: the ONE unresolved decision dimension most
    // likely to change #1, with the candidate most likely to replace it. Zero-LLM, computed from
    // the Candidate × Branch matrix; null when the winner's lead is decisive.
    public WideRankingChangeDriverDto? RankingChangeDriver{get;init;}
}

// V2.9.2: deterministic output-contract compliance result. RequestedCount is the explicit count
// from the query contract ("top 10"); DeliveredCount is the number of non-violating ranked
// candidates actually produced. IsSatisfied is false on any shortfall so the UI can disclose it.
public sealed record WideOutputContractResultDto(int RequestedCount,int DeliveredCount,bool IsSatisfied)
{
    // True when a relaxed-discovery recovery competition was attempted to close the shortfall.
    public bool RecoveryAttempted{get;init;}
}

// V2.9.2: the single highest-impact unresolved dimension. DimensionName is the decision dimension
// where the runner-up most out-scores (or is least behind) the winner relative to the winner's
// composite lead; ChallengerDisplayName is the candidate most likely to become #1 if that
// dimension resolves against the winner. MarginPoints is the winner-vs-challenger composite gap.
public sealed record WideRankingChangeDriverDto(string DimensionName,string ChallengerDisplayName,decimal MarginPoints)
{
    // Winner's and challenger's evidence scores on the driving dimension (UI context).
    public decimal? WinnerScore{get;init;}
    public decimal? ChallengerScore{get;init;}
}

// V2.9: one decision dimension with the candidate's evidence score on it.
public sealed record WideDimensionScoreDto(string DimensionName,decimal Score);

// V2.9: ranking-card summary — the candidate's best dimension and its weakest (main trade-off).
public sealed record WideCandidateSummaryDto(string DisplayName,decimal CompositeScore,string? BestForDimension,string? TradeOffDimension)
{
    // Evidence scores behind the best-for / trade-off dimensions so the UI can show the actual
    // data value ("Quality of life 92%"), not just the dimension name.
    public decimal? BestForScore{get;init;}
    public decimal? TradeOffScore{get;init;}
    // V2.9.1 human-facing signals derived from retrieved evidence (never invented): a short
    // buyer-facing "best for" phrase, praise themes, and complaint/watch-out themes. The raw
    // dimension names/scores above remain the deterministic fallback and tooltip layer — the
    // lowest branch score is NOT automatically a negative, so themes replace "Trade-off" text
    // whenever grounded insight exists.
    public string? BestFor{get;init;}
    public IReadOnlyCollection<string> PraisedFor{get;init;}=[];
    public IReadOnlyCollection<string> WatchOutFor{get;init;}=[];
    // V2.9.4 support tier (STRONG/MODERATE/LIMITED) so the ranking card discloses which results
    // are strongly supported versus provisional — completeness with transparency.
    public string SupportTierCode{get;init;}="STRONG";
}

// V2.9: deterministic contrast between the winner and a close alternative — the dimensions where
// the winner led and where the alternative led, from the same Candidate × Branch matrix.
public sealed record WideCandidateContrastDto(string AlternativeDisplayName,decimal AlternativeScore,IReadOnlyCollection<string> WinnerLeadsOn,IReadOnlyCollection<string> AlternativeLeadsOn);

// V2.8.4 Clarification Intelligence: one recognition-based clarification choice. Label is
// description-first (candidate's evidence-backed detail) because users searching a bare name
// often do not recognize the legal name — recognition beats recall.
// V3.2.1: Label is what the user SEES (evidence-backed description); Value is what is SUBMITTED as the
// clarification answer (the concise candidate name, so the continuation constraint stays clean). The
// OTHER escape hatch uses Key=OTHER and a null Value: the continuation re-runs without a fake constraint.
public sealed record WideClarificationOptionDto(string Key,string Label,string? Value=null);

// V2.1 Query Contract: separates hard constraints from ambiguous concepts so POLOXI only branches ambiguity.
public sealed record WideQueryContract(string? EntityType,string? GeographicConstraint,int? RequestedCount,string? RankingConcept,IReadOnlyCollection<string> HardConstraints,IReadOnlyCollection<string> AmbiguousConcepts,IReadOnlyCollection<string> OutputRequirements)
{
    // V3.1 answer-kind routing: broad output mode. Null degrades to the pre-V3.1 heuristics.
    public string? AnswerKind{get;init;}
    // V3.13 candidate contract: what kind of item is allowed to compete. This prevents entity-ranking
    // validation from accepting artifacts when the user asked for a fix, diagnosis, plan, or procedure.
    public string? CandidateKind{get;init;}
    // V3.14 contract-first layer fields. These define the playing field before MECE decomposition runs.
    public string? Intent{get;init;}
    public string? TargetObject{get;init;}
    public IReadOnlyCollection<string> RequiredTerms{get;init;}=[];
    public IReadOnlyCollection<string> ExcludedTerms{get;init;}=[];
    public IReadOnlyCollection<string> AmbiguousTerms{get;init;}=[];
    public string? SafetyRiskCode{get;init;}
    public string? OutputShape{get;init;}
    // V3.13 ambiguity-first contract: when the main object/action has materially different domain senses
    // and the query does not resolve them, POLOXI asks before ranking or enumerating.
    public bool RequiresClarification{get;init;}
    public string? ClarificationQuestion{get;init;}
    public string? ClarificationTarget{get;init;}
    public IReadOnlyCollection<string> ClarificationOptions{get;init;}=[];
    public bool IsSafetySensitive{get;init;}
}

// V2.1 candidate competition: a candidate with its composite score and per-branch evidence scores.
public sealed record WideCandidateDto(Guid WideCandidateId,int RankNumber,string DisplayName,string? Detail,decimal CompositeScore,IReadOnlyCollection<WideCandidateBranchScoreDto> BranchScores)
{
    // Share of surviving interpretation dimensions this candidate has evidence scores for.
    // Low coverage means the candidate may look strong only because data is missing.
    public decimal EvidenceCoverage{get;init;}
    // True when the candidate failed a hard query constraint and was ruled out (kept visible, never hidden).
    public bool IsConstraintViolation{get;init;}
    // V2.6 Candidate Quality: coverage-scaled dimension performance — "how good is this candidate?".
    // Never reduced by evidence weakness; weak evidence lowers EvidenceConfidence instead.
    public decimal QualityScore{get;init;}
    // V2.6 Evidence Confidence: "how well can we support that quality claim?" — deterministic, driven
    // by independent-source diversity and mention support. Affects confidence, never quality.
    public decimal EvidenceConfidence{get;init;}
    // V2.9.3 admission provenance: NORMAL = passed the standard cross-dimensional support gate;
    // RECOVERY = qualified only through the recovery-pass support calculation (interpretive
    // dimensions + independent evidence hosts, both distinct). requiredSupport itself is NEVER
    // lowered — recovery only recognizes additional independent support the normal discovery
    // path did not fully credit.
    public string AdmissionModeCode{get;init;}="NORMAL";
    // V2.9.4 tiered admission: the requested result count is honored whenever enough plausible,
    // evidence-backed candidates exist — weaker-but-valid candidates are ADMITTED with a lower
    // support tier instead of being silently dropped. The invariant is transparency, not exclusion:
    // never hide weaker evidence just to satisfy Top N, and never invent unsupported candidates.
    //   STRONG   = passes the standard cross-dimensional support gate (>=2 interpretation dimensions
    //              or equivalent host-attested support).
    //   MODERATE = combined distinct interpretive dimensions + distinct independent evidence hosts
    //              meets the required support (e.g., 1 dimension + >=1 independent host, or >=2 hosts).
    //   LIMITED  = at least one credible support signal (one dimension or one host) and passes
    //              all hard constraints.
    // Zero-support names are still excluded — tiers disclose weakness, they never admit inventions.
    public string SupportTierCode{get;init;}="STRONG";
    // Distinct interpretive dimensions naming this candidate (repeats within one branch count once).
    public int InterpretiveSupportCount{get;init;}
    // Distinct independent evidence hosts attesting this candidate (repeat articles from one host count once).
    public int EvidenceHostSupportCount{get;init;}
    // Total support credited at admission time (interpretive + hosts in recovery; capped host/interpretive rule in normal).
    public int TotalSupportCount{get;init;}
}

public sealed record WideCandidateBranchScoreDto(string BranchDisplayName,decimal EvidenceScore)
{
    // V3.5 hierarchical roll-up: when the dimension had scored child branches, EvidenceScore is the
    // blended roll-up and DirectScore preserves the model's flat parent-level judgment for disclosure.
    public decimal? DirectScore{get;init;}
    // Child (next-level) branch scores that fed the roll-up, with each child's PoloxiConfidence weight.
    public IReadOnlyCollection<WideCandidateChildScoreDto> ChildScores{get;init;}=[];
}

public sealed record WideCandidateChildScoreDto(string BranchDisplayName,decimal EvidenceScore,decimal Confidence);

public sealed record WideExternalReferenceDto(string Title,string Url,string Source,string Summary,string BranchDisplayName);

public sealed record WideInterpretiveResultDto(string BranchDisplayName,string Interpretation,decimal Confidence,IReadOnlyCollection<WideInterpretiveResultItemDto> Items)
{
    // STABLE: durable knowledge. TIME_SENSITIVE: prices, rates, rankings, availability - figures may be outdated.
    public string DataVolatility{get;init;}="STABLE";
    // True when the result set was composed from live external retrieval (Stage 2.5), not LLM recall.
    public bool IsExternallyGrounded{get;init;}
    // Branch lifecycle state of the source branch (ACTIVE/SECONDARY/DORMANT) so the UI can render
    // not-prioritized interpretations as secondary-importance results instead of hiding them.
    public string BranchStateCode{get;init;}=WideBranchStates.Active;
    // Hierarchy level of the source branch (0 when the branch could not be resolved).
    public int LevelNumber{get;init;}
}

public sealed record WideInterpretiveResultItemDto(int RankNumber,string Name,string Detail);

public sealed record WideActionSuggestionDto(string DisplayName,string NavigationRoute,string Rationale);

// Wide pipeline configuration loaded from Core.ConfigurationSetting (DB is the source of truth).
public sealed record WideConfiguration(decimal TargetConfidence,decimal MinimumBranchConfidence,int MaximumBranchesPerLevel,int AbsoluteDepthCeiling,int MaximumTotalLlmCalls)
{
    // V2.1 thresholds and weights (DB-seeded; see migration 0146).
    public decimal SecondaryBranchThreshold{get;init;}=.35m;
    public decimal DormantBranchThreshold{get;init;}=.20m;
    public decimal PriorWeight{get;init;}=.45m;
    public decimal EvidenceWeight{get;init;}=.55m;
    // V3.4 evidence-support calibration (DB-seeded; see migration 0163). Defaults preserve the
    // original hardcoded curve: enterprise = min(ceiling, base + increment*(count-1));
    // external = maxScore * min(1, base + increment*matchedCount).
    public decimal EnterpriseSupportBase{get;init;}=.50m;
    public decimal EnterpriseSupportIncrement{get;init;}=.20m;
    public decimal EnterpriseSupportCeiling{get;init;}=.90m;
    public decimal ExternalSupportBase{get;init;}=.60m;
    public decimal ExternalSupportIncrement{get;init;}=.10m;
    public int MaximumCandidates{get;init;}=10;
    public bool EnableQueryContract{get;init;}=true;
    // Bounded parallelism (DB-seeded; see migration 0147). 1 disables parallel execution.
    public int GroundingConcurrency{get;init;}=4;
    public int ExternalRetrievalConcurrency{get;init;}=3;
    // V2.2 information-directed exploration (DB-seeded; see migration 0149). Fail-soft: any
    // estimator/entropy failure skips the information round and continues V2.1 narrowing.
    public bool EnableInformationValue{get;init;}=true;
    public decimal InformationValueTriggerEntropy{get;init;}=.45m;
    // V2.8 Clarification Gate thresholds (DB-seeded; see migration 0152). ALL conditions must hold
    // for POLOXI to ask instead of answer — a single low metric never triggers a question.
    public bool EnableClarificationGate{get;init;}=true;
    // V3.2.1: raised from .60 so low-stability rankings with stalled retrieval ask instead of committing.
    public decimal ClarificationConfidenceThreshold{get;init;}=.65m;
    public decimal ClarificationWinnerStabilityThreshold{get;init;}=.50m;
    public decimal ClarificationMarginThreshold{get;init;}=.10m;
    // V2.8.5 Clarification Calibration (DB-seeded; see migration 0153): clarification must converge.
    // The round cap stops endless questioning; the gain floor stops follow-up questions when the
    // previous answer measurably failed to reduce intent uncertainty.
    public int MaximumClarificationRounds{get;init;}=2;
    public decimal MinimumClarificationGain{get;init;}=.10m;
    public int MaximumInformationRounds{get;init;}=3;
    public int MaximumInformationTargetsPerRound{get;init;}=2;
    public decimal MinimumInformationValue{get;init;}=.55m;
    public decimal MinimumActualInformationGain{get;init;}=.05m;
    public int InformationNoProgressRounds{get;init;}=2;
    public decimal InformationValueLlmWeight{get;init;}=.60m;
    public decimal InformationValueEvidenceGapWeight{get;init;}=.15m;
    public decimal InformationValueBranchWeight{get;init;}=.15m;
    public decimal InformationValueCandidateNeedWeight{get;init;}=.10m;
    // Phase 1 (VNext): information-round criterion weights (DB-seeded; see migration 0164). Defaults
    // preserve the original hardcoded raw-score formula exactly.
    public decimal CriterionUncertaintyWeight{get;init;}=.20m;
    public decimal CriterionRankingImpactWeight{get;init;}=.25m;
    public decimal CriterionDiscriminationWeight{get;init;}=.25m;
    public decimal CriterionEvidenceAvailabilityWeight{get;init;}=.15m;
    public decimal CriterionNoveltyWeight{get;init;}=.10m;
    public decimal CriterionRedundancyPenalty{get;init;}=.05m;
    // Phase 2a Challenge-the-Winner (DB-seeded; see migration 0165). Default OFF: current behavior
    // is bit-identical until explicitly enabled. Watch mode only — outcome is audit data.
    public bool EnableChallengeRound{get;init;}=false;
    public decimal ChallengeMarginThreshold{get;init;}=.10m;
    // V3.11 Guardrail-Constrained Weighted Utility: ordinary preference scores remain compensatory,
    // but guardrail criteria apply a deterministic veto-inspired penalty when performance is below
    // an acceptable floor. The LLM may describe criteria; POLOXI owns these thresholds and math.
    public bool EnableGuardrailPenalty{get;init;}=true;
    public decimal GuardrailVetoThreshold{get;init;}=.20m;
    public decimal GuardrailAcceptableThreshold{get;init;}=.65m;
    public decimal GuardrailPenaltyExponent{get;init;}=.50m;
    // V3.12 Marginal-Value Hierarchy Stopping: continue expanding only while a new level is still
    // adding measurable value. Deterministic deltas over data POLOXI already computes — when BOTH
    // the evidence-coverage delta AND the aggregate-confidence delta of the latest level fall below
    // these floors (at depth ≥ MarginalValueMinimumDepth), expansion stops with MARGINAL_VALUE_STOP
    // instead of paying another expensive hierarchy LLM call for decorative branches.
    public bool EnableMarginalValueStopping{get;init;}=true;
    public int MarginalValueMinimumDepth{get;init;}=3;
    public decimal MarginalCoverageDeltaFloor{get;init;}=.05m;
    public decimal MarginalConfidenceDeltaFloor{get;init;}=.03m;
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
    // V3.0 Evidence-Guided Adaptive Narrowing (DB-seeded; see migration 0155). Fail-soft: any
    // narrowing failure skips the evaluation and continues the V2.x pipeline unchanged.
    public bool EnableAdaptiveNarrowing{get;init;}=true;
    // Branch resolution eligibility: a branch may be RESOLVED only when its evidence support meets
    // the coverage floor AND its share of the round's investigation value is below the IV floor.
    public decimal NarrowingBranchCoverageFloor{get;init;}=.60m;
    public decimal NarrowingInformationValueFloor{get;init;}=.35m;
    // Reopen trigger: a RESOLVED branch reopens when its evidence support moves by at least this much.
    public decimal NarrowingReopenSupportDelta{get;init;}=.15m;
    // Candidate deferral eligibility: sufficient signal coverage AND at least this relative score gap
    // behind the leader. Candidates below the coverage floor go to WATCH (investigate, never eliminate).
    public decimal NarrowingCandidateCoverageFloor{get;init;}=.50m;
    public decimal NarrowingCandidateScoreGap{get;init;}=.40m;
    // Discovery admission gate: a newly discovered name is admitted only when attested by at least
    // this many distinct evidence hosts/mentions; per-round admission budget caps expansion cost.
    public int NarrowingDiscoveryMinimumSupport{get;init;}=2;
    public int MaximumCandidateAdmissionsPerRound{get;init;}=5;
    // V3.2 Answer-Kind-Aware Workflow Routing (DB-seeded; see migration 0157). The Stage 0
    // AnswerKind classification tunes (never forks) the pipeline: depth ceilings and information
    // round caps per kind. A depth ceiling of 0 means "use the full default"; unknown/null kinds
    // always run the full pipeline (fail-safe toward thoroughness, never toward speed).
    public bool EnableAnswerKindRouting{get;init;}=true;
    public int ContentEnumerationDepthCeiling{get;init;}=2;
    public int ContentEnumerationMaxInformationRounds{get;init;}=1;
    public int SingleAnswerDepthCeiling{get;init;}=2;
    public int SingleAnswerMaxInformationRounds{get;init;}
    // V3.3 POLOXI.AnswerKind lookup table (DB-seeded; see migration 0159). When rows exist they are
    // the source of truth for answer-kind recognition, per-kind budgets, and whether the deterministic
    // Candidate Competition applies; the per-kind config keys above remain only as compiled fallbacks
    // when the table is empty. Fail-safe toward the full pipeline.
    public IReadOnlyCollection<WideAnswerKindDefinition>AnswerKinds{get;init;}=[];
    // V3.3: the ReweightCandidatesByClarificationAnswer boost factor (score * (1 + boost * overlap)),
    // moved from a compiled .35m constant to a DB-seeded dial.
    public decimal ClarificationReweightBoost{get;init;}=.35m;
}

// V3.3 answer-kind definition row from POLOXI.AnswerKind. DepthCeiling 0 and MaxInformationRounds
// null both mean "use the full defaults"; budgets only ever shrink, never expand.
public sealed record WideAnswerKindDefinition(string AnswerKindCode,int DepthCeiling,int? MaxInformationRounds,bool RunsCandidateCompetition);

// Stage 2.5 external grounding configuration loaded from Core.ConfigurationSetting (DB is the source of truth).
// A blank ApiKey or Enabled=false disables live retrieval; the pipeline degrades to interpretive-only answers.
public sealed record WideExternalGroundingConfiguration(bool Enabled,string ProviderCode,string ApiKey,int MaximumQueriesPerExecution,int MaximumSnippetsPerQuery,int CacheHours,int TimeoutSeconds);

// A fresh real-world snippet retrieved at answer time (live provider call or POLOXI.ExternalKnowledge cache hit).
public sealed record WideExternalKnowledgeSnippet(string Query,string Title,string Url,string Snippet,decimal Score,DateTime RetrievedDateUtc);

// LLM structured outputs (strict JSON schema payloads).
public sealed record WideProposedBranch(string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,decimal Confidence,bool ContinueNarrowing,string? StopReason,string? ParentBranchCode)
{
    // V2.3: ALTERNATIVE or DIMENSION; anything else defaults to ALTERNATIVE for backward compatibility.
    public string? SemanticType{get;init;}
    // V3.12: HARD_CONSTRAINT | GUARDRAIL | PREFERENCE | CONTEXT; anything else defaults to
    // PREFERENCE so pre-role behavior (keyword fallbacks) is preserved.
    public string? BranchRole{get;init;}
}

public sealed record WideIntentProposal(string ConceptCode,string DisplayName,decimal AmbiguityScore,IReadOnlyCollection<WideProposedBranch> Branches);

public sealed record WideLevelProposal(IReadOnlyCollection<WideProposedBranch> Branches);

public sealed record WideAnswerProposal(string Answer,string VerificationCode,decimal Confidence,IReadOnlyCollection<WideAnswerAction> SuggestedActions,IReadOnlyCollection<int> RelevantEvidenceNumbers)
{
    public IReadOnlyCollection<WideExternalReference> ExternalReferences{get;init;}=[];
    public IReadOnlyCollection<WideInterpretiveResult> InterpretiveResults{get;init;}=[];
    // V2.9.1: evidence-grounded per-candidate insight themes (best-for phrase, praise themes,
    // complaint themes). The LLM may only echo themes present in the supplied evidence/snippets.
    public IReadOnlyCollection<WideCandidateInsight> CandidateInsights{get;init;}=[];
}

// V2.9.1: one candidate's grounded human-facing insight themes.
public sealed record WideCandidateInsight(string CandidateName,string? BestFor,IReadOnlyCollection<string> PraisedFor,IReadOnlyCollection<string> WatchOutFor);

public sealed record WideExternalReference(string Title,string Url,string Source,string Summary,string BranchDisplayName);

public sealed record WideInterpretiveResult(string BranchDisplayName,string Interpretation,IReadOnlyCollection<WideInterpretiveResultItem> Items)
{
    public string DataVolatility{get;init;}="STABLE";
}

public sealed record WideInterpretiveResultItem(int RankNumber,string Name,string Detail);

public sealed record WideAnswerAction(string DisplayName,string NavigationRoute,string Rationale);

// V2.1 LLM structured outputs.
public sealed record WideQueryContractProposal(string? EntityType,string? GeographicConstraint,int? RequestedCount,string? RankingConcept,IReadOnlyCollection<string> HardConstraints,IReadOnlyCollection<string> AmbiguousConcepts,IReadOnlyCollection<string> OutputRequirements)
{
    public string? AnswerKind{get;init;}
    public string? CandidateKind{get;init;}
    public string? Intent{get;init;}
    public string? TargetObject{get;init;}
    public IReadOnlyCollection<string> RequiredTerms{get;init;}=[];
    public IReadOnlyCollection<string> ExcludedTerms{get;init;}=[];
    public IReadOnlyCollection<string> AmbiguousTerms{get;init;}=[];
    public string? SafetyRiskCode{get;init;}
    public string? OutputShape{get;init;}
    public bool RequiresClarification{get;init;}
    public string? ClarificationQuestion{get;init;}
    public string? ClarificationTarget{get;init;}
    public IReadOnlyCollection<string> ClarificationOptions{get;init;}=[];
    public bool IsSafetySensitive{get;init;}
}

public sealed record WideCandidateScoringProposal(IReadOnlyCollection<WideCandidateScore> Candidates);

public sealed record WideCandidateScore(string Name,string? Detail,bool ViolatesConstraint,string? ConstraintViolationReason,IReadOnlyCollection<WideCandidateBranchEvidence> BranchScores)
{
    // V3.10 SB - Structural Boolean riding the existing scoring call: is this a concrete entity of
    // the kind the query asks for (a specific city/company/product), not a criterion, category, or
    // description? Untrusted LLM signal - it demotes admission tier but never hard-excludes alone.
    // Defaults true so older/partial responses keep pre-V3.10 behavior.
    public bool IsEntityOfRequestedKind{get;init;}=true;
}

public sealed record WideCandidateBranchEvidence(string BranchDisplayName,decimal EvidenceScore);

// Persistence records.
public sealed record WideExecutionStart(Guid TenantId,Guid UserId,string QueryText,string CorrelationId)
{
    // V3.4: links a clarification continuation to the execution that asked the question.
    public Guid? ParentWideExecutionId{get;init;}
}

public sealed record WideBranchRecord(Guid WideBranchId,Guid WideExecutionId,Guid? ParentWideBranchId,Guid TenantId,int LevelNumber,string BranchCode,string DisplayName,string Interpretation,string? CapabilityCode,string? SearchText,string GroundingStatusCode,int EvidenceCount,decimal Confidence,bool ContinueNarrowing,string? StopReason,bool IsEliminated,string? EliminationReason,int SortOrder)
{
    public string BranchStateCode{get;init;}=WideBranchStates.Active;
    public string SemanticTypeCode{get;init;}=WideBranchSemanticTypes.Alternative;
    // V3.12 explicit semantic role assigned at proposal time; PREFERENCE is the safe default that
    // preserves pre-role behavior (ordinary compensatory scoring + keyword fallbacks).
    public string BranchRoleCode{get;init;}=WideBranchRoles.Preference;
    public decimal InterpretationPrior{get;init;}
    public decimal EvidenceSupport{get;init;}
    public decimal PoloxiConfidence{get;init;}
}

public sealed record WideCandidateRecord(Guid WideCandidateId,Guid WideExecutionId,Guid TenantId,string DisplayName,string? Detail,decimal CompositeScore,int RankNumber,bool IsConstraintViolation,string? ConstraintViolationReason,IReadOnlyCollection<WideCandidateBranchScoreRecord> BranchScores);

public sealed record WideCandidateBranchScoreRecord(Guid WideCandidateBranchScoreId,Guid WideCandidateId,Guid WideBranchId,Guid TenantId,string BranchDisplayName,decimal EvidenceScore);

// Batch persistence rows (one round trip per level/phase instead of per branch).
public sealed record WideBranchOutcomeUpdate(Guid WideBranchId,string GroundingStatusCode,int EvidenceCount,bool IsEliminated,string? EliminationReason);

public sealed record WideBranchScoreUpdate(Guid WideBranchId,string BranchStateCode,decimal InterpretationPrior,decimal EvidenceSupport,decimal PoloxiConfidence);

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
    // if this branch is investigated. POLOXI later scores direction/magnitude accuracy against reality.
    public IReadOnlyCollection<WideRankingChangePrediction> PredictedRankingChanges{get;init;}=[];
}

public sealed record WideRankingChangePrediction(string Candidate,string Direction,string Magnitude);

// Persistence records (POLOXI.WideInformationRound / Target / Prediction; see migration 0149).
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

// V3.0 persistence record (POLOXI.WideNarrowingIteration; see migration 0155). Transitions are
// stored as JSON — every narrowing decision keeps its provenance (subject, states, reason).
public sealed record WideNarrowingIterationRecord(Guid WideNarrowingIterationId,Guid WideExecutionId,Guid TenantId,int RoundNumber,string TrendCode,int ActiveBranchCountBefore,int ActiveBranchCountAfter,int CandidateCountBefore,int CandidateCountAfter,decimal NormalizedEntropyBefore,decimal? NormalizedEntropyAfter,decimal? ActualInformationGain,int ResolvedBranchCount,int ReopenedBranchCount,int AdmittedCandidateCount,int DiscoveredNotAdmittedCount,string TransitionsJson);

// Execution-level entropy summary persisted at completion (POLOXI.WideExecution V2.2 columns).
public sealed record WideExecutionEntropyUpdate(Guid WideExecutionId,decimal? InitialEntropy,decimal? FinalEntropy,decimal? InitialNormalizedEntropy,decimal? FinalNormalizedEntropy,decimal? TotalActualInformationGain,int InformationRoundCount,int InformationTargetCount,int InformationRetrievalCount)
{
    public string? EntropyBasisCode{get;init;}
    // V2.8 clarification state (POLOXI.WideExecution columns; see migration 0152). Persisted so a
    // follow-up user answer continues the same reasoning context instead of restarting blind.
    public decimal? DecisionConfidence{get;init;}
    public string? ClarificationTarget{get;init;}
    public string? ClarificationQuestion{get;init;}
    // V2.8.5 Clarification Calibration (POLOXI.WideExecution columns; see migration 0153). Persisted
    // per execution so calibration queries can measure which clarification targets actually work.
    public decimal? IntentEntropy{get;init;}
    public decimal? PriorIntentEntropy{get;init;}
    public decimal? ClarificationGain{get;init;}
    public int ClarificationRound{get;init;}
}
