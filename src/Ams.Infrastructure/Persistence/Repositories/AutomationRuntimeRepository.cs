using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.AutomationJobs;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AutomationRuntimeRepository : IAutomationRuntimeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AutomationRuntimeRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyCollection<JobScheduleDto>> GetDueSchedulesAsync(DateTime dueUtc, int take, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP (@Take) s.JobScheduleId, s.TenantId, s.JobDefinitionId, s.ScheduleName, s.CronExpression, s.TimeZoneId,
       s.StartDateUtc, s.EndDateUtc, s.MisfirePolicy, s.NextRunDateUtc, s.LastRunDateUtc, s.IsEnabled, s.CreatedDateUtc
FROM Automation.JobSchedule s
JOIN Automation.JobDefinition jd ON jd.JobDefinitionId = s.JobDefinitionId
WHERE s.IsDeleted = 0
  AND s.IsEnabled = 1
  AND jd.IsDeleted = 0
  AND jd.IsActive = 1
  AND jd.StatusCode = N'Published'
  AND (s.StartDateUtc IS NULL OR s.StartDateUtc <= @DueUtc)
  AND (s.EndDateUtc IS NULL OR s.EndDateUtc >= @DueUtc)
  AND s.NextRunDateUtc IS NOT NULL
  AND s.NextRunDateUtc <= @DueUtc
  AND NOT EXISTS (
      SELECT 1 FROM Automation.JobRun r
      WHERE r.JobDefinitionId = s.JobDefinitionId
        AND r.IsDeleted = 0
        AND r.StatusCode IN (N'Queued', N'Running')
        AND jd.ConcurrencyPolicy = N'DisallowConcurrent')
ORDER BY s.NextRunDateUtc, s.CreatedDateUtc;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<JobScheduleDto>(new CommandDefinition(sql, new { DueUtc = dueUtc, Take = Math.Max(1, take) }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<Guid> CreateScheduledJobRunAsync(CreateScheduledJobRunRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
DECLARE @TotalSteps INT = (SELECT COUNT(1) FROM Automation.JobStep WHERE JobDefinitionId = @JobDefinitionId AND IsDeleted = 0 AND IsEnabled = 1);
IF NOT EXISTS (SELECT 1 FROM Automation.JobRun WHERE JobScheduleId = @JobScheduleId AND CorrelationId = @CorrelationId AND IsDeleted = 0)
BEGIN
    INSERT INTO Automation.JobRun
        (JobRunId, TenantId, JobDefinitionId, JobScheduleId, CorrelationId, TriggerType, StatusCode, TotalSteps, ExecutionContextJson, CreatedDateUtc, IsDeleted)
    VALUES
        (@Id, @TenantId, @JobDefinitionId, @JobScheduleId, @CorrelationId, N'Schedule', N'Queued', @TotalSteps, COALESCE(NULLIF(@ExecutionContextJson, N''), N'{}'), SYSUTCDATETIME(), 0);
END;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.JobDefinitionId, request.JobScheduleId, request.CorrelationId, request.ExecutionContextJson }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<IReadOnlyCollection<JobRunDto>> GetQueuedJobRunsAsync(int take, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP (@Take) jr.JobRunId, jr.TenantId, jr.JobDefinitionId, jr.JobScheduleId, jd.JobName, jd.JobCode, jr.CorrelationId, jr.TriggerType, jr.StatusCode,
       jr.StartedDateUtc, jr.CompletedDateUtc, jr.DurationMs, jr.CurrentStepOrder, jr.TotalSteps, jr.SuccessfulSteps, jr.FailedSteps,
       jr.RetryAttempt, jr.ErrorMessage, jr.ExecutionContextJson, jr.CreatedDateUtc
FROM Automation.JobRun jr
JOIN Automation.JobDefinition jd ON jd.JobDefinitionId = jr.JobDefinitionId
WHERE jr.IsDeleted = 0 AND jr.StatusCode = N'Queued' AND jd.IsDeleted = 0 AND jd.IsActive = 1
ORDER BY jr.CreatedDateUtc;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<JobRunDto>(new CommandDefinition(sql, new { Take = Math.Max(1, take) }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<bool> TryStartJobRunAsync(Guid jobRunId, StartJobRunRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Automation.JobRun
SET StatusCode = N'Running', StartedDateUtc = @StartedDateUtc, ModifiedDateUtc = SYSUTCDATETIME()
WHERE JobRunId = @JobRunId AND StatusCode = N'Queued' AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { JobRunId = jobRunId, request.StartedDateUtc }, cancellationToken: cancellationToken));
        return affected == 1;
    }

    public async Task CompleteJobRunAsync(Guid jobRunId, CompleteJobRunRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Automation.JobRun
SET StatusCode = @StatusCode,
    CompletedDateUtc = @CompletedDateUtc,
    DurationMs = DATEDIFF(millisecond, StartedDateUtc, @CompletedDateUtc),
    SuccessfulSteps = @SuccessfulSteps,
    FailedSteps = @FailedSteps,
    ErrorMessage = @ErrorMessage,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE JobRunId = @JobRunId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { JobRunId = jobRunId, request.StatusCode, request.SuccessfulSteps, request.FailedSteps, request.ErrorMessage, request.CompletedDateUtc }, cancellationToken: cancellationToken));
    }

    public async Task UpdateJobScheduleRunStateAsync(Guid jobScheduleId, UpdateJobScheduleRunStateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Automation.JobSchedule
SET LastRunDateUtc = @LastRunDateUtc,
    NextRunDateUtc = @NextRunDateUtc,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE JobScheduleId = @JobScheduleId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { JobScheduleId = jobScheduleId, request.LastRunDateUtc, request.NextRunDateUtc }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<JobStepDto>> GetEnabledJobStepsAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT JobStepId, TenantId, JobDefinitionId, StepOrder, StepCode, StepName, StepExecutorType, Description,
       InputMappingJson, OutputMappingJson, RetryPolicyJson, DynamicFieldSchemaJson, InputParameterJson, OutputContractJson, DependsOnStepCodes, TimeoutSeconds, ContinueOnError, IsEnabled, CreatedDateUtc
FROM Automation.JobStep
WHERE JobDefinitionId = @JobDefinitionId AND IsDeleted = 0 AND IsEnabled = 1
ORDER BY StepOrder;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<JobStepDto>(new CommandDefinition(sql, new { JobDefinitionId = jobDefinitionId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<Guid> CreateJobStepRunAsync(CreateJobStepRunRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Automation.JobStepRun
    (JobStepRunId, TenantId, JobRunId, JobStepId, StepOrder, StepExecutorType, StatusCode, InputJson, OutputJson, CreatedDateUtc, IsDeleted)
VALUES
    (@Id, @TenantId, @JobRunId, @JobStepId, @StepOrder, @StepExecutorType, N'Queued', COALESCE(NULLIF(@InputJson, N''), N'{}'), N'{}', SYSUTCDATETIME(), 0);

UPDATE Automation.JobRun
SET CurrentStepOrder = @StepOrder,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE JobRunId = @JobRunId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.JobRunId, request.JobStepId, request.StepOrder, request.StepExecutorType, request.InputJson }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task StartJobStepRunAsync(Guid jobStepRunId, DateTime startedDateUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Automation.JobStepRun
SET StatusCode = N'Running', StartedDateUtc = @StartedDateUtc, ModifiedDateUtc = SYSUTCDATETIME()
WHERE JobStepRunId = @JobStepRunId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { JobStepRunId = jobStepRunId, StartedDateUtc = startedDateUtc }, cancellationToken: cancellationToken));
    }

    public async Task CompleteJobStepRunAsync(Guid jobStepRunId, CompleteJobStepRunRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Automation.JobStepRun
SET StatusCode = @StatusCode,
    CompletedDateUtc = @CompletedDateUtc,
    DurationMs = DATEDIFF(millisecond, StartedDateUtc, @CompletedDateUtc),
    OutputJson = COALESCE(NULLIF(@OutputJson, N''), N'{}'),
    ErrorMessage = @ErrorMessage,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE JobStepRunId = @JobStepRunId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { JobStepRunId = jobStepRunId, request.StatusCode, request.OutputJson, request.ErrorMessage, request.CompletedDateUtc }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateFileExecutionLogAsync(CreateFileExecutionLogRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Automation.FileExecutionLog
    (FileExecutionLogId, TenantId, FileSaveId, JobRunId, JobStepRunId, LogLevel, EventType, Message, ExceptionType, ExceptionDetail, PayloadJson, CreatedDateUtc, IsDeleted)
VALUES
    (@Id, @TenantId, @FileSaveId, @JobRunId, @JobStepRunId, @LogLevel, @EventType, @Message, @ExceptionType, @ExceptionDetail, @PayloadJson, SYSUTCDATETIME(), 0);";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.FileSaveId, request.JobRunId, request.JobStepRunId, request.LogLevel, request.EventType, request.Message, request.ExceptionType, request.ExceptionDetail, request.PayloadJson }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> CreateFileRunLogAsync(CreateFileRunLogRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Automation.FileRunLog
    (FileRunLogId, TenantId, JobRunId, FileSaveId, Stage, StatusCode, RecordsReceived, RecordsProcessed, RecordsFailed, StartedDateUtc, CompletedDateUtc, ErrorMessage, MetricsJson, CreatedDateUtc, IsDeleted)
VALUES
    (@Id, @TenantId, @JobRunId, @FileSaveId, @Stage, @StatusCode, @RecordsReceived, @RecordsProcessed, @RecordsFailed, @StartedDateUtc, @CompletedDateUtc, @ErrorMessage, COALESCE(NULLIF(@MetricsJson, N''), N'{}'), SYSUTCDATETIME(), 0);";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.JobRunId, request.FileSaveId, request.Stage, request.StatusCode, request.RecordsReceived, request.RecordsProcessed, request.RecordsFailed, request.StartedDateUtc, request.CompletedDateUtc, request.ErrorMessage, request.MetricsJson }, cancellationToken: cancellationToken));
        return id;
    }
}
