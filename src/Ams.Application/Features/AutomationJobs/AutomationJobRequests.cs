using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.AutomationJobs;

public sealed record CreateJobDefinitionRequest(
    Guid TenantId,
    [property: Required, StringLength(100)] string JobCode,
    [property: Required, StringLength(240)] string JobName,
    [property: StringLength(1000)] string? Description,
    [property: Required, StringLength(80)] string JobTypeCode,
    [property: Required, StringLength(80)] string CategoryCode,
    [property: StringLength(160)] string? OwnerTeam,
    [property: Required, StringLength(50)] string ConcurrencyPolicy,
    [property: Range(0, 25)] int MaxRetryCount,
    [property: Range(30, 86400)] int TimeoutSeconds,
    [property: StringLength(500)] string? Tags,
    string ConfigurationJson,
    string DynamicFieldSchemaJson,
    string DefaultParameterJson,
    bool IsActive);

public sealed record UpdateJobDefinitionRequest(
    [property: Required, StringLength(240)] string JobName,
    [property: StringLength(1000)] string? Description,
    [property: Required, StringLength(80)] string JobTypeCode,
    [property: Required, StringLength(80)] string CategoryCode,
    [property: Required, StringLength(50)] string StatusCode,
    [property: StringLength(160)] string? OwnerTeam,
    [property: Required, StringLength(50)] string ConcurrencyPolicy,
    [property: Range(0, 25)] int MaxRetryCount,
    [property: Range(30, 86400)] int TimeoutSeconds,
    [property: StringLength(500)] string? Tags,
    string ConfigurationJson,
    string DynamicFieldSchemaJson,
    string DefaultParameterJson,
    bool IsActive);

public sealed record UpsertJobStepRequest(
    Guid TenantId,
    Guid JobDefinitionId,
    [property: Range(1, 500)] int StepOrder,
    [property: Required, StringLength(100)] string StepCode,
    [property: Required, StringLength(240)] string StepName,
    [property: Required, StringLength(240)] string StepExecutorType,
    [property: StringLength(1000)] string? Description,
    string InputMappingJson,
    string OutputMappingJson,
    string RetryPolicyJson,
    string DynamicFieldSchemaJson,
    string InputParameterJson,
    string OutputContractJson,
    [property: StringLength(500)] string? DependsOnStepCodes,
    [property: Range(30, 86400)] int TimeoutSeconds,
    bool ContinueOnError,
    bool IsEnabled);

public sealed record UpsertJobScheduleRequest(
    Guid TenantId,
    Guid JobDefinitionId,
    [property: Required, StringLength(240)] string ScheduleName,
    [property: Required, StringLength(120)] string CronExpression,
    [property: Required, StringLength(120)] string TimeZoneId,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    [property: Required, StringLength(80)] string MisfirePolicy,
    DateTime? NextRunDateUtc,
    bool IsEnabled);

public sealed record TriggerJobRunRequest(
    Guid TenantId,
    Guid? TriggeredByUserId,
    string ExecutionContextJson);

public sealed record SetJobScheduleEnabledRequest(bool IsEnabled);

public sealed record SetJobDefinitionStatusRequest(
    [property: Required, StringLength(50)] string StatusCode);

public sealed record IdResult(Guid Id);
