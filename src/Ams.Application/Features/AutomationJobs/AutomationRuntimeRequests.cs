using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.AutomationJobs;

public sealed record CreateScheduledJobRunRequest(
    Guid TenantId,
    Guid JobDefinitionId,
    Guid JobScheduleId,
    [property: Required, StringLength(120)] string CorrelationId,
    [property: Required, StringLength(4000)] string ExecutionContextJson);

public sealed record StartJobRunRequest(
    DateTime StartedDateUtc);

public sealed record CompleteJobRunRequest(
    [property: Required, StringLength(50)] string StatusCode,
    int SuccessfulSteps,
    int FailedSteps,
    [property: StringLength(2000)] string? ErrorMessage,
    DateTime CompletedDateUtc);

public sealed record UpdateJobScheduleRunStateRequest(
    DateTime LastRunDateUtc,
    DateTime? NextRunDateUtc);

public sealed record CreateJobStepRunRequest(
    Guid TenantId,
    Guid JobRunId,
    Guid JobStepId,
    [property: Range(1, 500)] int StepOrder,
    [property: Required, StringLength(240)] string StepExecutorType,
    [property: Required] string InputJson);

public sealed record CompleteJobStepRunRequest(
    [property: Required, StringLength(50)] string StatusCode,
    [property: Required] string OutputJson,
    [property: StringLength(2000)] string? ErrorMessage,
    DateTime CompletedDateUtc);

public sealed record CreateFileExecutionLogRequest(
    Guid TenantId,
    Guid? FileSaveId,
    Guid? JobRunId,
    Guid? JobStepRunId,
    [property: Required, StringLength(40)] string LogLevel,
    [property: Required, StringLength(100)] string EventType,
    [property: Required, StringLength(2000)] string Message,
    [property: StringLength(240)] string? ExceptionType,
    string? ExceptionDetail,
    string? PayloadJson);

public sealed record CreateFileRunLogRequest(
    Guid TenantId,
    Guid JobRunId,
    Guid? FileSaveId,
    [property: Required, StringLength(100)] string Stage,
    [property: Required, StringLength(50)] string StatusCode,
    [property: Range(0, int.MaxValue)] int RecordsReceived,
    [property: Range(0, int.MaxValue)] int RecordsProcessed,
    [property: Range(0, int.MaxValue)] int RecordsFailed,
    DateTime? StartedDateUtc,
    DateTime? CompletedDateUtc,
    [property: StringLength(2000)] string? ErrorMessage,
    [property: Required] string MetricsJson);
