using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Persistence;

// Persistence for the isolated Wide dynamic disambiguation pipeline.
public interface IIntelligenceWideRepository
{
    Task<WideConfiguration> GetWideConfigurationAsync(Guid tenantId,CancellationToken cancellationToken=default);
    // Active CHAT model deployments selectable on the wide-search page (tenant-scoped or platform-wide).
    Task<IReadOnlyCollection<WideModelOptionDto>> GetWideModelsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<Guid> StartWideExecutionAsync(WideExecutionStart start,CancellationToken cancellationToken=default);
    Task SaveWideBranchesAsync(IReadOnlyCollection<WideBranchRecord> branches,Guid userId,CancellationToken cancellationToken=default);
    Task UpdateWideBranchOutcomeAsync(Guid tenantId,Guid wideBranchId,string groundingStatusCode,int evidenceCount,bool isEliminated,string? eliminationReason,CancellationToken cancellationToken=default);
    // Batch variant: persists a whole level's grounding outcomes in one round trip.
    Task UpdateWideBranchOutcomesAsync(Guid tenantId,IReadOnlyCollection<WideBranchOutcomeUpdate> outcomes,CancellationToken cancellationToken=default);
    // V2.1: persists branch lifecycle state and the three-score model (prior, evidence support, POLOXI confidence).
    Task UpdateWideBranchScoresAsync(Guid tenantId,Guid wideBranchId,string branchStateCode,decimal interpretationPrior,decimal evidenceSupport,decimal poloxiConfidence,CancellationToken cancellationToken=default);
    // Batch variant: persists all branch scores/states in one round trip.
    Task UpdateWideBranchScoresAsync(Guid tenantId,IReadOnlyCollection<WideBranchScoreUpdate> scores,CancellationToken cancellationToken=default);
    // V2.1: persists the candidate universe and candidate-by-branch evidence matrix (never deleted).
    Task SaveWideCandidatesAsync(IReadOnlyCollection<WideCandidateRecord> candidates,Guid userId,CancellationToken cancellationToken=default);
    // V2.1: persists the extracted query contract and evidence coverage metrics on the execution.
    Task UpdateWideExecutionContractAsync(Guid tenantId,Guid userId,Guid wideExecutionId,string? queryContractJson,decimal evidenceCoverage,int externalEvidenceCount,int enterpriseEvidenceCount,int candidateCount,CancellationToken cancellationToken=default);
    // V3.2: persists the governing Stage 0 AnswerKind classification (ENTITY_RANKING / CONTENT_ENUMERATION / SINGLE_ANSWER).
    Task UpdateWideExecutionAnswerKindAsync(Guid tenantId,Guid userId,Guid wideExecutionId,string answerKindCode,CancellationToken cancellationToken=default);
    Task CompleteWideExecutionAsync(Guid tenantId,Guid userId,Guid wideExecutionId,string statusCode,string terminationReasonCode,int depthReached,int llmCallCount,decimal finalConfidence,string answerVerificationCode,string? finalAnswer,long durationMilliseconds,CancellationToken cancellationToken=default);
    Task<WideExternalGroundingConfiguration> GetExternalGroundingConfigurationAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> GetCachedExternalKnowledgeAsync(Guid tenantId,string normalizedQuery,DateTime notBeforeUtc,CancellationToken cancellationToken=default);
    Task SaveExternalKnowledgeAsync(Guid tenantId,Guid userId,string normalizedQuery,IReadOnlyCollection<WideExternalKnowledgeSnippet> snippets,Guid? wideExecutionId=null,CancellationToken cancellationToken=default);
    // V3.4: loads the tenant-scoped continuation state persisted on the parent execution row so the
    // epistemic chain never depends on client-carried fields. Null when not found for the tenant.
    Task<WideContinuationState?> GetWideContinuationStateAsync(Guid tenantId,Guid wideExecutionId,CancellationToken cancellationToken=default);
    // V3.4: returns every snippet retrieved by a specific execution for evidence reuse in continuations.
    Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> GetExecutionExternalKnowledgeAsync(Guid tenantId,Guid wideExecutionId,CancellationToken cancellationToken=default);
    // V2.2: persists one information-directed exploration round (entropy before; completed later).
    Task SaveInformationRoundAsync(WideInformationRoundRecord round,Guid userId,CancellationToken cancellationToken=default);
    // V2.2: writes the measured after-entropy, actual information gain, and raw delta for a round.
    // V2.5: also records the after max entropy (bits) and measured population size for auditability.
    Task CompleteInformationRoundAsync(Guid tenantId,Guid userId,Guid wideInformationRoundId,decimal entropyAfter,decimal normalizedEntropyAfter,decimal actualInformationGain,decimal rawEntropyDelta,int selectedTargetCount,decimal maxEntropyAfter,int populationCountAfter,CancellationToken cancellationToken=default);
    // V2.2: persists all evaluated targets for a round in one batch (selected and unselected, never deleted).
    Task SaveInformationTargetsAsync(IReadOnlyCollection<WideInformationTargetRecord> targets,Guid userId,CancellationToken cancellationToken=default);
    // V2.2: persists falsifiable per-candidate ranking predictions made by the estimator.
    Task SaveInformationPredictionsAsync(IReadOnlyCollection<WideInformationPredictionRecord> predictions,Guid userId,CancellationToken cancellationToken=default);
    // V2.2: scores predictions against reality after reranking (direction/magnitude accuracy).
    Task UpdateInformationPredictionOutcomesAsync(Guid tenantId,IReadOnlyCollection<WideInformationPredictionRecord> outcomes,CancellationToken cancellationToken=default);
    // V2.2: persists execution-level entropy summary and information-round counters.
    Task UpdateWideExecutionEntropyAsync(Guid tenantId,Guid userId,WideExecutionEntropyUpdate update,CancellationToken cancellationToken=default);
    // V3.0: persists one adaptive-narrowing evaluation with its full transition provenance (never deleted).
    Task SaveNarrowingIterationAsync(WideNarrowingIterationRecord iteration,Guid userId,CancellationToken cancellationToken=default);
}
