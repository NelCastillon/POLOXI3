namespace Ams.Application.Common.Dtos;

public sealed class AutomationSchedulerDashboardDto
{
    public int TotalJobs { get; set; }
    public int ActiveJobs { get; set; }
    public int EnabledSchedules { get; set; }
    public int RunningJobs { get; set; }
    public int FailedRuns24h { get; set; }
    public int FilesProcessed24h { get; set; }
    public double SuccessRate24h { get; set; }
    public IReadOnlyCollection<JobDefinitionDto> RecentJobs { get; set; } = Array.Empty<JobDefinitionDto>();
    public IReadOnlyCollection<JobRunDto> RecentRuns { get; set; } = Array.Empty<JobRunDto>();
}

public sealed class JobDefinitionDto
{
    public Guid JobDefinitionId { get; set; }
    public Guid TenantId { get; set; }
    public string JobCode { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JobTypeCode { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? OwnerTeam { get; set; }
    public string ConcurrencyPolicy { get; set; } = string.Empty;
    public int MaxRetryCount { get; set; }
    public int TimeoutSeconds { get; set; }
    public string? Tags { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public string DynamicFieldSchemaJson { get; set; } = "[]";
    public string DefaultParameterJson { get; set; } = "{}";
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public int StepCount { get; set; }
    public int ScheduleCount { get; set; }
    public DateTime? NextRunDateUtc { get; set; }
    public string? LastRunStatus { get; set; }
}

public sealed class JobStepDto
{
    public Guid JobStepId { get; set; }
    public Guid TenantId { get; set; }
    public Guid JobDefinitionId { get; set; }
    public int StepOrder { get; set; }
    public string StepCode { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string StepExecutorType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string InputMappingJson { get; set; } = "{}";
    public string OutputMappingJson { get; set; } = "{}";
    public string RetryPolicyJson { get; set; } = "{}";
    public string DynamicFieldSchemaJson { get; set; } = "[]";
    public string InputParameterJson { get; set; } = "{}";
    public string OutputContractJson { get; set; } = "{}";
    public string? DependsOnStepCodes { get; set; }
    public int TimeoutSeconds { get; set; }
    public bool ContinueOnError { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class JobScheduleDto
{
    public Guid JobScheduleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid JobDefinitionId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public string MisfirePolicy { get; set; } = string.Empty;
    public DateTime? NextRunDateUtc { get; set; }
    public DateTime? LastRunDateUtc { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class JobRunDto
{
    public Guid JobRunId { get; set; }
    public Guid TenantId { get; set; }
    public Guid JobDefinitionId { get; set; }
    public Guid? JobScheduleId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string JobCode { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? StartedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public int DurationMs { get; set; }
    public int? CurrentStepOrder { get; set; }
    public int TotalSteps { get; set; }
    public int SuccessfulSteps { get; set; }
    public int FailedSteps { get; set; }
    public int RetryAttempt { get; set; }
    public string? ErrorMessage { get; set; }
    public string ExecutionContextJson { get; set; } = "{}";
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class JobStepRunDto
{
    public Guid JobStepRunId { get; set; }
    public Guid TenantId { get; set; }
    public Guid JobRunId { get; set; }
    public Guid JobStepId { get; set; }
    public int StepOrder { get; set; }
    public string StepExecutorType { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? StartedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public int DurationMs { get; set; }
    public int RetryAttempt { get; set; }
    public string InputJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
}

public sealed class FileSaveDto
{
    public Guid FileSaveId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? JobRunId { get; set; }
    public Guid? JobStepRunId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ChecksumSha256 { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? BlobUri { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class FileExecutionLogDto
{
    public Guid FileExecutionLogId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? FileSaveId { get; set; }
    public Guid? JobRunId { get; set; }
    public Guid? JobStepRunId { get; set; }
    public string LogLevel { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? ExceptionDetail { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class FileRunLogDto
{
    public Guid FileRunLogId { get; set; }
    public Guid TenantId { get; set; }
    public Guid JobRunId { get; set; }
    public Guid? FileSaveId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public int RecordsReceived { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public DateTime? StartedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string MetricsJson { get; set; } = "{}";
    public DateTime CreatedDateUtc { get; set; }
}
