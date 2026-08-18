using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Persistence;

// Persistence for the isolated Wide dynamic disambiguation pipeline.
public interface IIntelligenceWideRepository
{
    Task<WideConfiguration> GetWideConfigurationAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<Guid> StartWideExecutionAsync(WideExecutionStart start,CancellationToken cancellationToken=default);
    Task SaveWideBranchesAsync(IReadOnlyCollection<WideBranchRecord> branches,Guid userId,CancellationToken cancellationToken=default);
    Task UpdateWideBranchOutcomeAsync(Guid tenantId,Guid wideBranchId,string groundingStatusCode,int evidenceCount,bool isEliminated,string? eliminationReason,CancellationToken cancellationToken=default);
    // V2.1: persists branch lifecycle state and the three-score model (prior, evidence support, EPH confidence).
    Task UpdateWideBranchScoresAsync(Guid tenantId,Guid wideBranchId,string branchStateCode,decimal interpretationPrior,decimal evidenceSupport,decimal ephConfidence,CancellationToken cancellationToken=default);
    // V2.1: persists the candidate universe and candidate-by-branch evidence matrix (never deleted).
    Task SaveWideCandidatesAsync(IReadOnlyCollection<WideCandidateRecord> candidates,Guid userId,CancellationToken cancellationToken=default);
    // V2.1: persists the extracted query contract and evidence coverage metrics on the execution.
    Task UpdateWideExecutionContractAsync(Guid tenantId,Guid userId,Guid wideExecutionId,string? queryContractJson,decimal evidenceCoverage,int externalEvidenceCount,int enterpriseEvidenceCount,int candidateCount,CancellationToken cancellationToken=default);
    Task CompleteWideExecutionAsync(Guid tenantId,Guid userId,Guid wideExecutionId,string statusCode,string terminationReasonCode,int depthReached,int llmCallCount,decimal finalConfidence,string answerVerificationCode,string? finalAnswer,long durationMilliseconds,CancellationToken cancellationToken=default);
    Task<WideExternalGroundingConfiguration> GetExternalGroundingConfigurationAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> GetCachedExternalKnowledgeAsync(Guid tenantId,string normalizedQuery,DateTime notBeforeUtc,CancellationToken cancellationToken=default);
    Task SaveExternalKnowledgeAsync(Guid tenantId,Guid userId,string normalizedQuery,IReadOnlyCollection<WideExternalKnowledgeSnippet> snippets,CancellationToken cancellationToken=default);
}
