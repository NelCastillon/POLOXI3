using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.DocumentIntake;

public sealed record DocumentIntakeRuntimeSettings(
    int WorkerBatchSize,
    int WorkerPollIntervalSeconds,
    int LeaseDurationSeconds,
    bool MalwareEnabled,
    bool MalwareFailClosed,
    string MalwareProviderCode,
    int MalwarePendingTimeoutMinutes,
    int PayloadRetentionDays,
    int PayloadPurgeBatchSize,
    int PayloadRetentionWorkerIntervalMinutes,
    bool PayloadAccessAuditEnabled,
    bool TelemetryEnabled,
    int TelemetrySnapshotIntervalMinutes,
    int DeadLetterReplayMaxAttempts,
    bool RequirePassedPromptEvaluation);

public sealed record DocumentIntakeDeadLetterDto(Guid WorkItemId,Guid IntakeSessionId,string SessionNumber,Guid? DocumentId,string WorkTypeCode,string StatusCode,int AttemptCount,int MaxAttempts,string? LastErrorCode,string? LastErrorMessage,DateTime AvailableDateUtc,byte[] RowVersion);
public sealed record ReplayDocumentIntakeWorkCommand(Guid TenantId,Guid WorkItemId,[Required,StringLength(2000)]string Reason,[Required,StringLength(120)]string CorrelationId,Guid ActorUserId,byte[] RowVersion);
public sealed record DocumentIntakeMalwareStatusDto(Guid TenantId,Guid DocumentId,string FileName,string StoragePath,string StatusCode,string ProviderCode,string? ThreatName,string? ProviderResult,DateTime ScanRequestedDateUtc,DateTime? ScanCompletedDateUtc,byte[] RowVersion);
public sealed record DocumentIntakePayloadDto(Guid PayloadGovernanceId,Guid TenantId,Guid IntakeSessionId,string StorageReference,string PayloadTypeCode,bool ContainsPii,DateTime RetainUntilDateUtc,int LegalHoldCount,string StatusCode,DateTime CreatedDateUtc,byte[] RowVersion);
public sealed record PlaceDocumentIntakeLegalHoldCommand(Guid TenantId,Guid IntakeSessionId,[Required,StringLength(100)]string HoldCode,[Required,StringLength(2000)]string Reason,Guid ActorUserId);
public sealed record ReleaseDocumentIntakeLegalHoldCommand(Guid TenantId,Guid LegalHoldId,[Required,StringLength(2000)]string Reason,Guid ActorUserId,byte[] RowVersion);
public sealed record DocumentIntakePromptSuiteDto(Guid SuiteId,Guid? TenantId,string PromptCode,string SuiteName,string? Description,decimal MinimumPassRate,decimal MinimumAverageScore,int ActiveCaseCount,bool IsActive,byte[] RowVersion);
public sealed record DocumentIntakePromptEvaluationRunDto(Guid RunId,Guid? TenantId,Guid PromptDefinitionId,Guid SuiteId,string PromptCode,string PromptVersion,string StatusCode,int TotalCaseCount,int PassedCaseCount,decimal? PassRate,decimal? AverageScore,DateTime CreatedDateUtc,DateTime? CompletedDateUtc,byte[] RowVersion);
public sealed record QueuePromptEvaluationCommand(Guid TenantId,Guid PromptDefinitionId,Guid SuiteId,[Required,StringLength(120)]string CorrelationId,Guid ActorUserId);
public sealed record DocumentIntakePromptEvaluationWorkDto(Guid RunId,Guid? TenantId,Guid PromptDefinitionId,Guid SuiteId,string PromptCode,string PromptVersion,string SystemPrompt,string OutputSchemaJson,string CorrelationId,IReadOnlyCollection<DocumentIntakePromptEvaluationCaseDto> Cases);
public sealed record DocumentIntakePromptEvaluationCaseDto(Guid CaseId,string CaseName,string InputPayloadReference,string ExpectedOutputJson,string EvaluationRulesJson);
public sealed record DocumentIntakePromptEvaluationCaseResult(Guid CaseId,string StatusCode,decimal Score,string? ActualOutputReference,string? DifferenceJson,string? ErrorCode,string? ErrorMessage,long DurationMilliseconds);
public sealed record ApproveDocumentIntakePromptCommand(Guid TenantId,Guid PromptDefinitionId,Guid EvaluationRunId,[Required,StringLength(2000)]string Reason,Guid ActorUserId,byte[] PromptRowVersion);
public sealed record DocumentIntakeTelemetryDto(DateTime WindowStartUtc,DateTime WindowEndUtc,int QueueDepth,long OldestQueuedAgeSeconds,int ProcessingCount,int RetryCount,int DeadLetterCount,int CompletedCount,int FailedCount,long? P50DurationMilliseconds,long? P95DurationMilliseconds,int ProviderThrottleCount,long InputTokenCount,long OutputTokenCount);
public sealed record DocumentIntakeAlertDto(Guid AlertId,Guid? TenantId,string SloCode,string DisplayName,string SeverityCode,string StatusCode,decimal MetricValue,decimal ThresholdValue,string Summary,DateTime FirstObservedDateUtc,DateTime LastObservedDateUtc,byte[] RowVersion);
public sealed record DocumentIntakeReadinessDto(string ComponentCode,string StatusCode,string Message,long DurationMilliseconds,DateTime CheckedDateUtc);
