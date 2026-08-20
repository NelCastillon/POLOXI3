using Ams.Application.Features.DocumentIntake;

namespace Ams.Application.Abstractions.Persistence;

public interface IDocumentIntakeOperationsRepository
{
    Task<DocumentIntakeRuntimeSettings> GetSettingsAsync(Guid? tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakeDeadLetterDto>> GetDeadLettersAsync(Guid tenantId,int pageSize=100,CancellationToken cancellationToken=default);
    Task ReplayDeadLetterAsync(ReplayDocumentIntakeWorkCommand command,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakeMalwareStatusDto>> GetPendingMalwareScansAsync(int batchSize,int errorRetryMinutes=15,CancellationToken cancellationToken=default);
    Task UpsertMalwareStatusAsync(Guid tenantId,Guid documentId,string storagePath,string statusCode,string providerCode,string? threatName,string? providerResult,CancellationToken cancellationToken=default);
    Task EnsureDocumentCleanAsync(Guid tenantId,Guid intakeSessionId,bool failClosed,CancellationToken cancellationToken=default);
    Task RegisterPayloadAsync(Guid tenantId,Guid intakeSessionId,string storageReference,string payloadType,bool containsPii,int retentionDays,string actorId,string? correlationId,CancellationToken cancellationToken=default);
    Task RecordPayloadAccessAsync(Guid tenantId,Guid intakeSessionId,string storageReference,string actionCode,string actorType,string actorId,string purpose,string outcomeCode,string? correlationId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakePayloadDto>> LeaseExpiredPayloadsAsync(int batchSize,CancellationToken cancellationToken=default);
    Task CompletePayloadPurgeAsync(Guid tenantId,Guid payloadGovernanceId,bool succeeded,string? error,CancellationToken cancellationToken=default);
    Task PlaceLegalHoldAsync(PlaceDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken=default);
    Task ReleaseLegalHoldAsync(ReleaseDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakePromptSuiteDto>> GetPromptSuitesAsync(Guid? tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakePromptEvaluationRunDto>> GetPromptEvaluationRunsAsync(Guid? tenantId,int pageSize=100,CancellationToken cancellationToken=default);
    Task<Guid> QueuePromptEvaluationAsync(QueuePromptEvaluationCommand command,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakePromptEvaluationWorkDto>> LeasePromptEvaluationsAsync(string leaseOwner,int batchSize,CancellationToken cancellationToken=default);
    Task CompletePromptEvaluationAsync(Guid runId,IReadOnlyCollection<DocumentIntakePromptEvaluationCaseResult> results,CancellationToken cancellationToken=default);
    Task ApprovePromptAsync(ApproveDocumentIntakePromptCommand command,bool requirePassedRun,CancellationToken cancellationToken=default);
    Task<DocumentIntakeTelemetryDto> CaptureTelemetrySnapshotAsync(CancellationToken cancellationToken=default);
    Task EvaluateSlosAsync(DocumentIntakeTelemetryDto snapshot,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakeAlertDto>> GetAlertsAsync(Guid? tenantId,bool openOnly=true,CancellationToken cancellationToken=default);
}
