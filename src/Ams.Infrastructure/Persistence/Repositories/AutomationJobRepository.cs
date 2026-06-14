using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AutomationJobs;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AutomationJobRepository : IAutomationJobRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AutomationJobRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AutomationSchedulerDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    TotalJobs = (SELECT COUNT(1) FROM Automation.JobDefinition WHERE TenantId = @TenantId AND IsDeleted = 0),
    ActiveJobs = (SELECT COUNT(1) FROM Automation.JobDefinition WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1),
    EnabledSchedules = (SELECT COUNT(1) FROM Automation.JobSchedule WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsEnabled = 1),
    RunningJobs = (SELECT COUNT(1) FROM Automation.JobRun WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode IN (N'Queued', N'Running')),
    FailedRuns24h = (SELECT COUNT(1) FROM Automation.JobRun WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode IN (N'Failed', N'CompletedWithWarnings') AND CreatedDateUtc >= DATEADD(day, -1, SYSUTCDATETIME())),
    FilesProcessed24h = (SELECT COALESCE(SUM(RecordsProcessed), 0) FROM Automation.FileRunLog WHERE TenantId = @TenantId AND IsDeleted = 0 AND CreatedDateUtc >= DATEADD(day, -1, SYSUTCDATETIME())),
    SuccessRate24h = CAST(CASE WHEN (SELECT COUNT(1) FROM Automation.JobRun WHERE TenantId = @TenantId AND IsDeleted = 0 AND CreatedDateUtc >= DATEADD(day, -1, SYSUTCDATETIME())) = 0 THEN 100.0 ELSE
        100.0 * (SELECT COUNT(1) FROM Automation.JobRun WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode IN (N'Completed', N'CompletedWithWarnings') AND CreatedDateUtc >= DATEADD(day, -1, SYSUTCDATETIME())) /
        NULLIF((SELECT COUNT(1) FROM Automation.JobRun WHERE TenantId = @TenantId AND IsDeleted = 0 AND CreatedDateUtc >= DATEADD(day, -1, SYSUTCDATETIME())), 0) END AS float);

SELECT TOP 6 jd.JobDefinitionId, jd.TenantId, jd.JobCode, jd.JobName, jd.Description, jd.JobTypeCode, jd.CategoryCode, jd.StatusCode,
       jd.OwnerTeam, jd.ConcurrencyPolicy, jd.MaxRetryCount, jd.TimeoutSeconds, jd.Tags, jd.ConfigurationJson, jd.IsActive,
       jd.CreatedDateUtc, jd.ModifiedDateUtc,
       StepCount = (SELECT COUNT(1) FROM Automation.JobStep js WHERE js.JobDefinitionId = jd.JobDefinitionId AND js.IsDeleted = 0),
       ScheduleCount = (SELECT COUNT(1) FROM Automation.JobSchedule s WHERE s.JobDefinitionId = jd.JobDefinitionId AND s.IsDeleted = 0),
       NextRunDateUtc = (SELECT MIN(s.NextRunDateUtc) FROM Automation.JobSchedule s WHERE s.JobDefinitionId = jd.JobDefinitionId AND s.IsDeleted = 0 AND s.IsEnabled = 1),
       LastRunStatus = (SELECT TOP 1 r.StatusCode FROM Automation.JobRun r WHERE r.JobDefinitionId = jd.JobDefinitionId AND r.IsDeleted = 0 ORDER BY r.CreatedDateUtc DESC)
FROM Automation.JobDefinition jd
WHERE jd.TenantId = @TenantId AND jd.IsDeleted = 0
ORDER BY jd.ModifiedDateUtc DESC, jd.CreatedDateUtc DESC;

SELECT TOP 10 jr.JobRunId, jr.TenantId, jr.JobDefinitionId, jr.JobScheduleId, jd.JobName, jd.JobCode, jr.CorrelationId, jr.TriggerType, jr.StatusCode,
       jr.StartedDateUtc, jr.CompletedDateUtc, jr.DurationMs, jr.CurrentStepOrder, jr.TotalSteps, jr.SuccessfulSteps, jr.FailedSteps,
       jr.RetryAttempt, jr.ErrorMessage, jr.ExecutionContextJson, jr.CreatedDateUtc
FROM Automation.JobRun jr
JOIN Automation.JobDefinition jd ON jd.JobDefinitionId = jr.JobDefinitionId
WHERE jr.TenantId = @TenantId AND jr.IsDeleted = 0
ORDER BY jr.CreatedDateUtc DESC;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new { TenantId = tenantId });
        var dashboard = await multi.ReadSingleAsync<AutomationSchedulerDashboardDto>();
        dashboard.RecentJobs = (await multi.ReadAsync<JobDefinitionDto>()).ToList();
        dashboard.RecentRuns = (await multi.ReadAsync<JobRunDto>()).ToList();
        return dashboard;
    }

    public async Task<PagedResult<JobDefinitionDto>> SearchJobDefinitionsAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, string? categoryCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT jd.JobDefinitionId, jd.TenantId, jd.JobCode, jd.JobName, jd.Description, jd.JobTypeCode, jd.CategoryCode, jd.StatusCode,
           jd.OwnerTeam, jd.ConcurrencyPolicy, jd.MaxRetryCount, jd.TimeoutSeconds, jd.Tags, jd.ConfigurationJson, jd.DynamicFieldSchemaJson, jd.DefaultParameterJson, jd.IsActive,
           jd.CreatedDateUtc, jd.ModifiedDateUtc,
           StepCount = (SELECT COUNT(1) FROM Automation.JobStep js WHERE js.JobDefinitionId = jd.JobDefinitionId AND js.IsDeleted = 0),
           ScheduleCount = (SELECT COUNT(1) FROM Automation.JobSchedule s WHERE s.JobDefinitionId = jd.JobDefinitionId AND s.IsDeleted = 0),
           NextRunDateUtc = (SELECT MIN(s.NextRunDateUtc) FROM Automation.JobSchedule s WHERE s.JobDefinitionId = jd.JobDefinitionId AND s.IsDeleted = 0 AND s.IsEnabled = 1),
           LastRunStatus = (SELECT TOP 1 r.StatusCode FROM Automation.JobRun r WHERE r.JobDefinitionId = jd.JobDefinitionId AND r.IsDeleted = 0 ORDER BY r.CreatedDateUtc DESC)
    FROM Automation.JobDefinition jd
    WHERE jd.TenantId = @TenantId AND jd.IsDeleted = 0
      AND (@StatusCode IS NULL OR @StatusCode = N'' OR jd.StatusCode = @StatusCode)
      AND (@CategoryCode IS NULL OR @CategoryCode = N'' OR jd.CategoryCode = @CategoryCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR jd.JobCode LIKE N'%' + @SearchTerm + N'%' OR jd.JobName LIKE N'%' + @SearchTerm + N'%' OR jd.Description LIKE N'%' + @SearchTerm + N'%')
)
SELECT COUNT(1) FROM Cte;
;WITH Cte AS
(
    SELECT jd.JobDefinitionId, jd.TenantId, jd.JobCode, jd.JobName, jd.Description, jd.JobTypeCode, jd.CategoryCode, jd.StatusCode,
           jd.OwnerTeam, jd.ConcurrencyPolicy, jd.MaxRetryCount, jd.TimeoutSeconds, jd.Tags, jd.ConfigurationJson, jd.DynamicFieldSchemaJson, jd.DefaultParameterJson, jd.IsActive,
           jd.CreatedDateUtc, jd.ModifiedDateUtc,
           StepCount = (SELECT COUNT(1) FROM Automation.JobStep js WHERE js.JobDefinitionId = jd.JobDefinitionId AND js.IsDeleted = 0),
           ScheduleCount = (SELECT COUNT(1) FROM Automation.JobSchedule s WHERE s.JobDefinitionId = jd.JobDefinitionId AND s.IsDeleted = 0),
           NextRunDateUtc = (SELECT MIN(s.NextRunDateUtc) FROM Automation.JobSchedule s WHERE s.JobDefinitionId = jd.JobDefinitionId AND s.IsDeleted = 0 AND s.IsEnabled = 1),
           LastRunStatus = (SELECT TOP 1 r.StatusCode FROM Automation.JobRun r WHERE r.JobDefinitionId = jd.JobDefinitionId AND r.IsDeleted = 0 ORDER BY r.CreatedDateUtc DESC)
    FROM Automation.JobDefinition jd
    WHERE jd.TenantId = @TenantId AND jd.IsDeleted = 0
      AND (@StatusCode IS NULL OR @StatusCode = N'' OR jd.StatusCode = @StatusCode)
      AND (@CategoryCode IS NULL OR @CategoryCode = N'' OR jd.CategoryCode = @CategoryCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR jd.JobCode LIKE N'%' + @SearchTerm + N'%' OR jd.JobName LIKE N'%' + @SearchTerm + N'%' OR jd.Description LIKE N'%' + @SearchTerm + N'%')
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new { TenantId = tenantId, SearchTerm = searchTerm, StatusCode = statusCode, CategoryCode = categoryCode, PageNumber = pageNumber, PageSize = pageSize });
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<JobDefinitionDto> { Items = (await multi.ReadAsync<JobDefinitionDto>()).ToList(), TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<JobDefinitionDto?> GetJobDefinitionAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT jd.JobDefinitionId, jd.TenantId, jd.JobCode, jd.JobName, jd.Description, jd.JobTypeCode, jd.CategoryCode, jd.StatusCode,
       jd.OwnerTeam, jd.ConcurrencyPolicy, jd.MaxRetryCount, jd.TimeoutSeconds, jd.Tags, jd.ConfigurationJson, jd.DynamicFieldSchemaJson, jd.DefaultParameterJson, jd.IsActive,
       jd.CreatedDateUtc, jd.ModifiedDateUtc,
       StepCount = (SELECT COUNT(1) FROM Automation.JobStep js WHERE js.JobDefinitionId = jd.JobDefinitionId AND js.IsDeleted = 0),
       ScheduleCount = (SELECT COUNT(1) FROM Automation.JobSchedule s WHERE s.JobDefinitionId = jd.JobDefinitionId AND s.IsDeleted = 0),
       NextRunDateUtc = (SELECT MIN(s.NextRunDateUtc) FROM Automation.JobSchedule s WHERE s.JobDefinitionId = jd.JobDefinitionId AND s.IsDeleted = 0 AND s.IsEnabled = 1),
       LastRunStatus = (SELECT TOP 1 r.StatusCode FROM Automation.JobRun r WHERE r.JobDefinitionId = jd.JobDefinitionId AND r.IsDeleted = 0 ORDER BY r.CreatedDateUtc DESC)
FROM Automation.JobDefinition jd
WHERE jd.JobDefinitionId = @JobDefinitionId AND jd.IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<JobDefinitionDto>(sql, new { JobDefinitionId = jobDefinitionId });
    }

    public async Task<IReadOnlyCollection<JobStepDto>> GetJobStepsAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT JobStepId, TenantId, JobDefinitionId, StepOrder, StepCode, StepName, StepExecutorType, Description, InputMappingJson, OutputMappingJson, RetryPolicyJson, DynamicFieldSchemaJson, InputParameterJson, OutputContractJson, DependsOnStepCodes, TimeoutSeconds, ContinueOnError, IsEnabled, CreatedDateUtc FROM Automation.JobStep WHERE JobDefinitionId = @JobDefinitionId AND IsDeleted = 0 ORDER BY StepOrder;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<JobStepDto>(sql, new { JobDefinitionId = jobDefinitionId })).ToList();
    }

    public async Task<IReadOnlyCollection<JobScheduleDto>> GetJobSchedulesAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT JobScheduleId, TenantId, JobDefinitionId, ScheduleName, CronExpression, TimeZoneId, StartDateUtc, EndDateUtc, MisfirePolicy, NextRunDateUtc, LastRunDateUtc, IsEnabled, CreatedDateUtc FROM Automation.JobSchedule WHERE JobDefinitionId = @JobDefinitionId AND IsDeleted = 0 ORDER BY IsEnabled DESC, NextRunDateUtc;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<JobScheduleDto>(sql, new { JobDefinitionId = jobDefinitionId })).ToList();
    }

    public async Task<PagedResult<JobRunDto>> SearchJobRunsAsync(Guid tenantId, Guid? jobDefinitionId = null, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT jr.JobRunId, jr.TenantId, jr.JobDefinitionId, jr.JobScheduleId, jd.JobName, jd.JobCode, jr.CorrelationId, jr.TriggerType, jr.StatusCode, jr.StartedDateUtc, jr.CompletedDateUtc, jr.DurationMs, jr.CurrentStepOrder, jr.TotalSteps, jr.SuccessfulSteps, jr.FailedSteps, jr.RetryAttempt, jr.ErrorMessage, jr.ExecutionContextJson, jr.CreatedDateUtc
    FROM Automation.JobRun jr
    JOIN Automation.JobDefinition jd ON jd.JobDefinitionId = jr.JobDefinitionId
    WHERE jr.TenantId = @TenantId AND jr.IsDeleted = 0
      AND (@JobDefinitionId IS NULL OR jr.JobDefinitionId = @JobDefinitionId)
      AND (@StatusCode IS NULL OR @StatusCode = N'' OR jr.StatusCode = @StatusCode)
)
SELECT COUNT(1) FROM Cte;
;WITH Cte AS
(
    SELECT jr.JobRunId, jr.TenantId, jr.JobDefinitionId, jr.JobScheduleId, jd.JobName, jd.JobCode, jr.CorrelationId, jr.TriggerType, jr.StatusCode, jr.StartedDateUtc, jr.CompletedDateUtc, jr.DurationMs, jr.CurrentStepOrder, jr.TotalSteps, jr.SuccessfulSteps, jr.FailedSteps, jr.RetryAttempt, jr.ErrorMessage, jr.ExecutionContextJson, jr.CreatedDateUtc
    FROM Automation.JobRun jr
    JOIN Automation.JobDefinition jd ON jd.JobDefinitionId = jr.JobDefinitionId
    WHERE jr.TenantId = @TenantId AND jr.IsDeleted = 0
      AND (@JobDefinitionId IS NULL OR jr.JobDefinitionId = @JobDefinitionId)
      AND (@StatusCode IS NULL OR @StatusCode = N'' OR jr.StatusCode = @StatusCode)
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new { TenantId = tenantId, JobDefinitionId = jobDefinitionId, StatusCode = statusCode, PageNumber = pageNumber, PageSize = pageSize });
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<JobRunDto> { Items = (await multi.ReadAsync<JobRunDto>()).ToList(), TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyCollection<JobStepRunDto>> GetJobStepRunsAsync(Guid jobRunId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT JobStepRunId, TenantId, JobRunId, JobStepId, StepOrder, StepExecutorType, StatusCode, StartedDateUtc, CompletedDateUtc, DurationMs, RetryAttempt, InputJson, OutputJson, ErrorMessage FROM Automation.JobStepRun WHERE JobRunId = @JobRunId AND IsDeleted = 0 ORDER BY StepOrder;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<JobStepRunDto>(sql, new { JobRunId = jobRunId })).ToList();
    }

    public async Task<IReadOnlyCollection<FileSaveDto>> GetFileSavesAsync(Guid jobRunId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT FileSaveId, TenantId, JobRunId, JobStepRunId, SourceType, OriginalFileName, StoredFileName, ContentType, FileSizeBytes, ChecksumSha256, StorageProvider, StoragePath, BlobUri, StatusCode, MetadataJson, CreatedDateUtc FROM Automation.FileSave WHERE JobRunId = @JobRunId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<FileSaveDto>(sql, new { JobRunId = jobRunId })).ToList();
    }

    public async Task<IReadOnlyCollection<FileExecutionLogDto>> GetFileExecutionLogsAsync(Guid jobRunId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT FileExecutionLogId, TenantId, FileSaveId, JobRunId, JobStepRunId, LogLevel, EventType, Message, ExceptionType, ExceptionDetail, PayloadJson, CreatedDateUtc FROM Automation.FileExecutionLog WHERE JobRunId = @JobRunId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<FileExecutionLogDto>(sql, new { JobRunId = jobRunId })).ToList();
    }

    public async Task<IReadOnlyCollection<FileRunLogDto>> GetFileRunLogsAsync(Guid jobRunId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT FileRunLogId, TenantId, JobRunId, FileSaveId, Stage, StatusCode, RecordsReceived, RecordsProcessed, RecordsFailed, StartedDateUtc, CompletedDateUtc, ErrorMessage, MetricsJson, CreatedDateUtc FROM Automation.FileRunLog WHERE JobRunId = @JobRunId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<FileRunLogDto>(sql, new { JobRunId = jobRunId })).ToList();
    }

    public async Task<Guid> CreateJobDefinitionAsync(CreateJobDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Automation.JobDefinition (JobDefinitionId, TenantId, JobCode, JobName, Description, JobTypeCode, CategoryCode, StatusCode, OwnerTeam, ConcurrencyPolicy, MaxRetryCount, TimeoutSeconds, Tags, ConfigurationJson, DynamicFieldSchemaJson, DefaultParameterJson, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @JobCode, @JobName, @Description, @JobTypeCode, @CategoryCode, N'Draft', @OwnerTeam, @ConcurrencyPolicy, @MaxRetryCount, @TimeoutSeconds, @Tags, COALESCE(NULLIF(@ConfigurationJson, N''), N'{}'), COALESCE(NULLIF(@DynamicFieldSchemaJson, N''), N'[]'), COALESCE(NULLIF(@DefaultParameterJson, N''), N'{}'), @IsActive, SYSUTCDATETIME(), NULL, 0);";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = id, request.TenantId, request.JobCode, request.JobName, request.Description, request.JobTypeCode, request.CategoryCode, request.OwnerTeam, request.ConcurrencyPolicy, request.MaxRetryCount, request.TimeoutSeconds, request.Tags, request.ConfigurationJson, request.DynamicFieldSchemaJson, request.DefaultParameterJson, request.IsActive });
        return id;
    }

    public async Task UpdateJobDefinitionAsync(Guid jobDefinitionId, UpdateJobDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Automation.JobDefinition
SET JobName = @JobName, Description = @Description, JobTypeCode = @JobTypeCode, CategoryCode = @CategoryCode, StatusCode = @StatusCode,
    OwnerTeam = @OwnerTeam, ConcurrencyPolicy = @ConcurrencyPolicy, MaxRetryCount = @MaxRetryCount, TimeoutSeconds = @TimeoutSeconds,
    Tags = @Tags, ConfigurationJson = COALESCE(NULLIF(@ConfigurationJson, N''), N'{}'), DynamicFieldSchemaJson = COALESCE(NULLIF(@DynamicFieldSchemaJson, N''), N'[]'), DefaultParameterJson = COALESCE(NULLIF(@DefaultParameterJson, N''), N'{}'), IsActive = @IsActive, ModifiedDateUtc = SYSUTCDATETIME()
WHERE JobDefinitionId = @JobDefinitionId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { JobDefinitionId = jobDefinitionId, request.JobName, request.Description, request.JobTypeCode, request.CategoryCode, request.StatusCode, request.OwnerTeam, request.ConcurrencyPolicy, request.MaxRetryCount, request.TimeoutSeconds, request.Tags, request.ConfigurationJson, request.DynamicFieldSchemaJson, request.DefaultParameterJson, request.IsActive });
    }

    public async Task SetJobDefinitionStatusAsync(Guid jobDefinitionId, SetJobDefinitionStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE Automation.JobDefinition SET StatusCode = @StatusCode, ModifiedDateUtc = SYSUTCDATETIME() WHERE JobDefinitionId = @JobDefinitionId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { JobDefinitionId = jobDefinitionId, request.StatusCode });
    }

    public async Task<Guid> UpsertJobStepAsync(Guid? jobStepId, UpsertJobStepRequest request, CancellationToken cancellationToken = default)
    {
        var id = jobStepId ?? Guid.NewGuid();
        const string sql = @"
IF EXISTS (SELECT 1 FROM Automation.JobStep WHERE JobStepId = @Id AND IsDeleted = 0)
BEGIN
    UPDATE Automation.JobStep
    SET StepOrder = @StepOrder, StepCode = @StepCode, StepName = @StepName, StepExecutorType = @StepExecutorType, Description = @Description,
        InputMappingJson = COALESCE(NULLIF(@InputMappingJson, N''), N'{}'), OutputMappingJson = COALESCE(NULLIF(@OutputMappingJson, N''), N'{}'), RetryPolicyJson = COALESCE(NULLIF(@RetryPolicyJson, N''), N'{}'), DynamicFieldSchemaJson = COALESCE(NULLIF(@DynamicFieldSchemaJson, N''), N'[]'), InputParameterJson = COALESCE(NULLIF(@InputParameterJson, N''), N'{}'), OutputContractJson = COALESCE(NULLIF(@OutputContractJson, N''), N'{}'), DependsOnStepCodes = @DependsOnStepCodes,
        TimeoutSeconds = @TimeoutSeconds, ContinueOnError = @ContinueOnError, IsEnabled = @IsEnabled, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE JobStepId = @Id;
END
ELSE
BEGIN
    INSERT INTO Automation.JobStep (JobStepId, TenantId, JobDefinitionId, StepOrder, StepCode, StepName, StepExecutorType, Description, InputMappingJson, OutputMappingJson, RetryPolicyJson, DynamicFieldSchemaJson, InputParameterJson, OutputContractJson, DependsOnStepCodes, TimeoutSeconds, ContinueOnError, IsEnabled, CreatedDateUtc, IsDeleted)
    VALUES (@Id, @TenantId, @JobDefinitionId, @StepOrder, @StepCode, @StepName, @StepExecutorType, @Description, COALESCE(NULLIF(@InputMappingJson, N''), N'{}'), COALESCE(NULLIF(@OutputMappingJson, N''), N'{}'), COALESCE(NULLIF(@RetryPolicyJson, N''), N'{}'), COALESCE(NULLIF(@DynamicFieldSchemaJson, N''), N'[]'), COALESCE(NULLIF(@InputParameterJson, N''), N'{}'), COALESCE(NULLIF(@OutputContractJson, N''), N'{}'), @DependsOnStepCodes, @TimeoutSeconds, @ContinueOnError, @IsEnabled, SYSUTCDATETIME(), 0);
END;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = id, request.TenantId, request.JobDefinitionId, request.StepOrder, request.StepCode, request.StepName, request.StepExecutorType, request.Description, request.InputMappingJson, request.OutputMappingJson, request.RetryPolicyJson, request.DynamicFieldSchemaJson, request.InputParameterJson, request.OutputContractJson, request.DependsOnStepCodes, request.TimeoutSeconds, request.ContinueOnError, request.IsEnabled });
        return id;
    }

    public async Task<Guid> UpsertJobScheduleAsync(Guid? jobScheduleId, UpsertJobScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var id = jobScheduleId ?? Guid.NewGuid();
        const string sql = @"
IF EXISTS (SELECT 1 FROM Automation.JobSchedule WHERE JobScheduleId = @Id AND IsDeleted = 0)
BEGIN
    UPDATE Automation.JobSchedule
    SET ScheduleName = @ScheduleName, CronExpression = @CronExpression, TimeZoneId = @TimeZoneId, StartDateUtc = @StartDateUtc, EndDateUtc = @EndDateUtc,
        MisfirePolicy = @MisfirePolicy, NextRunDateUtc = @NextRunDateUtc, IsEnabled = @IsEnabled, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE JobScheduleId = @Id;
END
ELSE
BEGIN
    INSERT INTO Automation.JobSchedule (JobScheduleId, TenantId, JobDefinitionId, ScheduleName, CronExpression, TimeZoneId, StartDateUtc, EndDateUtc, MisfirePolicy, NextRunDateUtc, IsEnabled, CreatedDateUtc, IsDeleted)
    VALUES (@Id, @TenantId, @JobDefinitionId, @ScheduleName, @CronExpression, @TimeZoneId, @StartDateUtc, @EndDateUtc, @MisfirePolicy, @NextRunDateUtc, @IsEnabled, SYSUTCDATETIME(), 0);
END;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = id, request.TenantId, request.JobDefinitionId, request.ScheduleName, request.CronExpression, request.TimeZoneId, request.StartDateUtc, request.EndDateUtc, request.MisfirePolicy, request.NextRunDateUtc, request.IsEnabled });
        return id;
    }

    public async Task SetJobScheduleEnabledAsync(Guid jobScheduleId, SetJobScheduleEnabledRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE Automation.JobSchedule SET IsEnabled = @IsEnabled, ModifiedDateUtc = SYSUTCDATETIME() WHERE JobScheduleId = @JobScheduleId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { JobScheduleId = jobScheduleId, request.IsEnabled });
    }

    public async Task<Guid> TriggerJobRunAsync(Guid jobDefinitionId, TriggerJobRunRequest request, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        const string sql = @"
DECLARE @TotalSteps INT = (SELECT COUNT(1) FROM Automation.JobStep WHERE JobDefinitionId = @JobDefinitionId AND IsDeleted = 0 AND IsEnabled = 1);
INSERT INTO Automation.JobRun (JobRunId, TenantId, JobDefinitionId, CorrelationId, TriggerType, StatusCode, TotalSteps, TriggeredByUserId, ExecutionContextJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@RunId, @TenantId, @JobDefinitionId, CONCAT(N'MANUAL-', CONVERT(NVARCHAR(36), @RunId)), N'Manual', N'Queued', @TotalSteps, @TriggeredByUserId, COALESCE(NULLIF(@ExecutionContextJson, N''), N'{}'), SYSUTCDATETIME(), @TriggeredByUserId, 0);";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { RunId = runId, request.TenantId, JobDefinitionId = jobDefinitionId, request.TriggeredByUserId, request.ExecutionContextJson });
        return runId;
    }
}
