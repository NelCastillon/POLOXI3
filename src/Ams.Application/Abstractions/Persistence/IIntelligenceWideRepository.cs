using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Persistence;

// Persistence for the isolated Wide dynamic disambiguation pipeline.
public interface IIntelligenceWideRepository
{
    Task<WideConfiguration> GetWideConfigurationAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<Guid> StartWideExecutionAsync(WideExecutionStart start,CancellationToken cancellationToken=default);
    Task SaveWideBranchesAsync(IReadOnlyCollection<WideBranchRecord> branches,Guid userId,CancellationToken cancellationToken=default);
    Task UpdateWideBranchOutcomeAsync(Guid tenantId,Guid wideBranchId,string groundingStatusCode,int evidenceCount,bool isEliminated,string? eliminationReason,CancellationToken cancellationToken=default);
    Task CompleteWideExecutionAsync(Guid tenantId,Guid userId,Guid wideExecutionId,string statusCode,string terminationReasonCode,int depthReached,int llmCallCount,decimal finalConfidence,string answerVerificationCode,string? finalAnswer,long durationMilliseconds,CancellationToken cancellationToken=default);
}
