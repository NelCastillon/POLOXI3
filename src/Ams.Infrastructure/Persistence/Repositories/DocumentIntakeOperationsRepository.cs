using System.Data;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.DocumentIntake;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DocumentIntakeOperationsRepository(ISqlConnectionFactory connectionFactory):IDocumentIntakeOperationsRepository
{
    public async Task<DocumentIntakeRuntimeSettings> GetSettingsAsync(Guid? tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT platform.SettingKey,COALESCE(tenant.SettingValue,platform.SettingValue,platform.DefaultValue) SettingValue
            FROM Core.ConfigurationSetting platform
            LEFT JOIN Core.ConfigurationSetting tenant ON tenant.TenantId=@TenantId AND tenant.ScopeCode=N'Tenant' AND tenant.SettingKey=platform.SettingKey AND tenant.IsDeleted=0
            WHERE platform.TenantId IS NULL AND platform.ScopeCode=N'Platform' AND platform.ModuleCode=N'DocumentIntake' AND platform.IsDeleted=0;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var values=(await connection.QueryAsync<SettingRow>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).ToDictionary(x=>x.SettingKey,x=>x.SettingValue,StringComparer.OrdinalIgnoreCase);
        int Integer(string key,int fallback)=>int.TryParse(values.GetValueOrDefault(key),out var value)?value:fallback;
        bool Boolean(string key,bool fallback)=>bool.TryParse(values.GetValueOrDefault(key),out var value)?value:fallback;
        string Text(string key,string fallback)=>values.GetValueOrDefault(key)??fallback;
        return new(Integer("DocumentIntake.Worker.BatchSize",10),Integer("DocumentIntake.Worker.PollIntervalSeconds",10),Integer("DocumentIntake.Worker.LeaseDurationSeconds",300),Boolean("DocumentIntake.Malware.Enabled",true),Boolean("DocumentIntake.Malware.FailClosed",true),Text("DocumentIntake.Malware.ProviderCode","MICROSOFT_DEFENDER_STORAGE"),Integer("DocumentIntake.Malware.PendingTimeoutMinutes",15),Integer("DocumentIntake.Payload.RetentionDays",90),Integer("DocumentIntake.Payload.PurgeBatchSize",100),Integer("DocumentIntake.Payload.RetentionWorkerIntervalMinutes",60),Boolean("DocumentIntake.Payload.AccessAuditEnabled",true),Boolean("DocumentIntake.Telemetry.Enabled",true),Integer("DocumentIntake.Telemetry.SnapshotIntervalMinutes",5),Integer("DocumentIntake.DeadLetter.ReplayMaxAttempts",3),Boolean("DocumentIntake.PromptEvaluation.RequirePassedRunForApproval",true));
    }

    public async Task<IReadOnlyCollection<DocumentIntakeDeadLetterDto>> GetDeadLettersAsync(Guid tenantId,int pageSize=100,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT TOP(@PageSize) w.IntakeWorkItemId WorkItemId,w.IntakeSessionId,s.SessionNumber,w.DocumentId,w.WorkTypeCode,w.StatusCode,w.AttemptCount,w.MaxAttempts,w.LastErrorCode,w.LastErrorMessage,w.AvailableDateUtc,w.RowVersion
            FROM DMS.IntakeWorkItem w JOIN DMS.IntakeSession s ON s.TenantId=w.TenantId AND s.IntakeSessionId=w.IntakeSessionId
            WHERE w.TenantId=@TenantId AND w.StatusCode IN(N'DEAD_LETTERED',N'FAILED') ORDER BY w.AvailableDateUtc,w.IntakeWorkItemId;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DocumentIntakeDeadLetterDto>(new CommandDefinition(sql,new{TenantId=tenantId,PageSize=Math.Clamp(pageSize,1,500)},cancellationToken:cancellationToken))).AsList();
    }

    public Task ReplayDeadLetterAsync(ReplayDocumentIntakeWorkCommand command,CancellationToken cancellationToken=default)=>TransactionAsync(async(connection,transaction)=>
    {
        var settings=await GetSettingsWithinAsync(connection,transaction,command.TenantId,cancellationToken);
        const string sql="""
            DECLARE @SessionId UNIQUEIDENTIFIER,@PreviousStatus NVARCHAR(30),@WorkType NVARCHAR(50),@AttemptCount INT;
            SELECT @SessionId=IntakeSessionId,@PreviousStatus=StatusCode,@WorkType=WorkTypeCode,@AttemptCount=AttemptCount FROM DMS.IntakeWorkItem WITH(UPDLOCK) WHERE TenantId=@TenantId AND IntakeWorkItemId=@WorkItemId AND RowVersion=@RowVersion AND StatusCode IN(N'DEAD_LETTERED',N'FAILED');
            IF @SessionId IS NULL THROW 51000,'Dead-letter work changed or was not found for tenant.',1;
            IF (SELECT COUNT(1) FROM DMS.IntakeWorkReplayHistory WHERE TenantId=@TenantId AND IntakeWorkItemId=@WorkItemId)>=@ReplayMaxAttempts THROW 51000,'Maximum operator replay cycles reached.',1;
            UPDATE DMS.IntakeWorkItem SET StatusCode=N'RETRY_SCHEDULED',AttemptCount=0,AvailableDateUtc=SYSUTCDATETIME(),LeaseOwner=NULL,LeaseExpiresDateUtc=NULL,LastErrorCode=NULL,LastErrorMessage=NULL,CompletedDateUtc=NULL WHERE TenantId=@TenantId AND IntakeWorkItemId=@WorkItemId;
            UPDATE DMS.IntakeSession SET StatusCode=N'QUEUED',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND StatusCode=N'FAILED';
            INSERT DMS.IntakeWorkReplayHistory(TenantId,IntakeWorkItemId,IntakeSessionId,PreviousStatusCode,ReplayFromWorkTypeCode,Reason,ReplayedByUserId,CorrelationId) VALUES(@TenantId,@WorkItemId,@SessionId,@PreviousStatus,@WorkType,@Reason,@ActorUserId,@CorrelationId);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,new{command.TenantId,command.WorkItemId,command.Reason,command.ActorUserId,command.CorrelationId,command.RowVersion,ReplayMaxAttempts=settings.DeadLetterReplayMaxAttempts},transaction,cancellationToken:cancellationToken));
    },cancellationToken);

    public async Task<IReadOnlyCollection<DocumentIntakeMalwareStatusDto>> GetPendingMalwareScansAsync(int batchSize,int errorRetryMinutes=15,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT TOP(@BatchSize) scan.TenantId,scan.DocumentId,document.FileName,scan.StoragePath,scan.StatusCode,scan.ProviderCode,scan.ThreatName,scan.ProviderResult,scan.ScanRequestedDateUtc,scan.ScanCompletedDateUtc,scan.RowVersion
            FROM DMS.IntakeMalwareScan scan JOIN DMS.Document document ON document.TenantId=scan.TenantId AND document.DocumentId=scan.DocumentId AND document.IsDeleted=0
            WHERE scan.StatusCode=N'PENDING' OR (scan.StatusCode=N'ERROR' AND COALESCE(scan.ModifiedDateUtc,scan.ScanRequestedDateUtc)<=DATEADD(MINUTE,-@ErrorRetryMinutes,SYSUTCDATETIME())) ORDER BY scan.ScanRequestedDateUtc;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DocumentIntakeMalwareStatusDto>(new CommandDefinition(sql,new{BatchSize=Math.Clamp(batchSize,1,500),ErrorRetryMinutes=Math.Clamp(errorRetryMinutes,1,1440)},cancellationToken:cancellationToken))).AsList();
    }

    public async Task UpsertMalwareStatusAsync(Guid tenantId,Guid documentId,string storagePath,string statusCode,string providerCode,string? threatName,string? providerResult,CancellationToken cancellationToken=default)
    {
        const string sql="""
            MERGE DMS.IntakeMalwareScan target USING(SELECT @TenantId TenantId,@DocumentId DocumentId) source ON target.TenantId=source.TenantId AND target.DocumentId=source.DocumentId
            WHEN MATCHED THEN UPDATE SET StoragePath=@StoragePath,StatusCode=@StatusCode,ProviderCode=@ProviderCode,ThreatName=@ThreatName,ProviderResult=@ProviderResult,ScanCompletedDateUtc=CASE WHEN @StatusCode<>N'PENDING' THEN SYSUTCDATETIME() ELSE NULL END,QuarantinedDateUtc=CASE WHEN @StatusCode IN(N'INFECTED',N'QUARANTINED') THEN COALESCE(target.QuarantinedDateUtc,SYSUTCDATETIME()) ELSE target.QuarantinedDateUtc END,ModifiedDateUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(TenantId,DocumentId,StoragePath,ProviderCode,StatusCode,ThreatName,ProviderResult,ScanCompletedDateUtc,QuarantinedDateUtc) VALUES(@TenantId,@DocumentId,@StoragePath,@ProviderCode,@StatusCode,@ThreatName,@ProviderResult,CASE WHEN @StatusCode<>N'PENDING' THEN SYSUTCDATETIME() END,CASE WHEN @StatusCode IN(N'INFECTED',N'QUARANTINED') THEN SYSUTCDATETIME() END);
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,DocumentId=documentId,StoragePath=storagePath,StatusCode=statusCode,ProviderCode=providerCode,ThreatName=threatName,ProviderResult=providerResult},cancellationToken:cancellationToken));
    }

    public async Task EnsureDocumentCleanAsync(Guid tenantId,Guid intakeSessionId,bool failClosed,CancellationToken cancellationToken=default)
    {
        const string sql="""
            IF EXISTS(SELECT 1 FROM DMS.IntakeSessionDocument link LEFT JOIN DMS.IntakeMalwareScan scan ON scan.TenantId=link.TenantId AND scan.DocumentId=link.DocumentId WHERE link.TenantId=@TenantId AND link.IntakeSessionId=@SessionId AND (scan.DocumentId IS NULL OR scan.StatusCode<>N'CLEAN'))
            BEGIN
                IF @FailClosed=1 THROW 52000,'All evidence documents must have a CLEAN malware scan before processing.',1;
                IF EXISTS(SELECT 1 FROM DMS.IntakeSessionDocument link JOIN DMS.IntakeMalwareScan scan ON scan.TenantId=link.TenantId AND scan.DocumentId=link.DocumentId WHERE link.TenantId=@TenantId AND link.IntakeSessionId=@SessionId AND scan.StatusCode IN(N'INFECTED',N'QUARANTINED')) THROW 52000,'Quarantined evidence cannot be processed.',1;
            END;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,SessionId=intakeSessionId,FailClosed=failClosed},cancellationToken:cancellationToken));
    }

    public Task RegisterPayloadAsync(Guid tenantId,Guid intakeSessionId,string storageReference,string payloadType,bool containsPii,int retentionDays,string actorId,string? correlationId,CancellationToken cancellationToken=default)=>TransactionAsync(async(connection,transaction)=>
    {
        const string sql="""
            MERGE DMS.IntakePayloadGovernance target USING(SELECT @TenantId TenantId,@StorageReference StorageReference) source ON target.TenantId=source.TenantId AND target.StorageReference=source.StorageReference
            WHEN MATCHED THEN UPDATE SET RetainUntilDateUtc=CASE WHEN target.RetainUntilDateUtc<DATEADD(DAY,@RetentionDays,SYSUTCDATETIME()) THEN DATEADD(DAY,@RetentionDays,SYSUTCDATETIME()) ELSE target.RetainUntilDateUtc END,ModifiedDateUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(TenantId,IntakeSessionId,StorageReference,PayloadTypeCode,ContainsPii,RetainUntilDateUtc) VALUES(@TenantId,@SessionId,@StorageReference,UPPER(@PayloadType),@ContainsPii,DATEADD(DAY,@RetentionDays,SYSUTCDATETIME()));
            INSERT DMS.IntakePayloadAccessAudit(TenantId,IntakeSessionId,StorageReference,ActionCode,ActorTypeCode,ActorId,CorrelationId,Purpose,OutcomeCode) VALUES(@TenantId,@SessionId,@StorageReference,N'WRITE',N'WORKER',@ActorId,@CorrelationId,N'Retain governed AI processing payload.',N'SUCCEEDED');
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,SessionId=intakeSessionId,StorageReference=storageReference,PayloadType=payloadType,ContainsPii=containsPii,RetentionDays=Math.Clamp(retentionDays,1,3650),ActorId=actorId,CorrelationId=correlationId},transaction,cancellationToken:cancellationToken));
    },cancellationToken);

    public async Task RecordPayloadAccessAsync(Guid tenantId,Guid intakeSessionId,string storageReference,string actionCode,string actorType,string actorId,string purpose,string outcomeCode,string? correlationId,CancellationToken cancellationToken=default)
    {
        const string sql="INSERT DMS.IntakePayloadAccessAudit(TenantId,IntakeSessionId,StorageReference,ActionCode,ActorTypeCode,ActorId,CorrelationId,Purpose,OutcomeCode) VALUES(@TenantId,@SessionId,@StorageReference,@ActionCode,@ActorType,@ActorId,@CorrelationId,@Purpose,@OutcomeCode);";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,SessionId=intakeSessionId,StorageReference=storageReference,ActionCode=actionCode,ActorType=actorType,ActorId=actorId,CorrelationId=correlationId,Purpose=purpose,OutcomeCode=outcomeCode},cancellationToken:cancellationToken));
    }

    public Task<IReadOnlyCollection<DocumentIntakePayloadDto>> LeaseExpiredPayloadsAsync(int batchSize,CancellationToken cancellationToken=default)=>TransactionResultAsync(async(connection,transaction)=>
    {
        const string sql="""
            DECLARE @Leased TABLE(PayloadGovernanceId UNIQUEIDENTIFIER,IntakeSessionId UNIQUEIDENTIFIER,StorageReference NVARCHAR(1000),PayloadTypeCode NVARCHAR(50),ContainsPii BIT,RetainUntilDateUtc DATETIME2,LegalHoldCount INT,StatusCode NVARCHAR(30),CreatedDateUtc DATETIME2,RowVersion BINARY(8),TenantId UNIQUEIDENTIFIER);
            ;WITH candidates AS(SELECT TOP(@BatchSize)* FROM DMS.IntakePayloadGovernance WITH(UPDLOCK,READPAST,ROWLOCK) WHERE StatusCode=N'ACTIVE' AND LegalHoldCount=0 AND RetainUntilDateUtc<=SYSUTCDATETIME() ORDER BY RetainUntilDateUtc)
            UPDATE candidates SET StatusCode=N'PURGE_PENDING',ModifiedDateUtc=SYSUTCDATETIME() OUTPUT inserted.IntakePayloadGovernanceId,inserted.IntakeSessionId,inserted.StorageReference,inserted.PayloadTypeCode,inserted.ContainsPii,inserted.RetainUntilDateUtc,inserted.LegalHoldCount,inserted.StatusCode,inserted.CreatedDateUtc,inserted.RowVersion,inserted.TenantId INTO @Leased;
            SELECT PayloadGovernanceId,TenantId,IntakeSessionId,StorageReference,PayloadTypeCode,ContainsPii,RetainUntilDateUtc,LegalHoldCount,StatusCode,CreatedDateUtc,RowVersion FROM @Leased;
            """;
        return (IReadOnlyCollection<DocumentIntakePayloadDto>)(await connection.QueryAsync<DocumentIntakePayloadDto>(new CommandDefinition(sql,new{BatchSize=Math.Clamp(batchSize,1,500)},transaction,cancellationToken:cancellationToken))).AsList();
    },cancellationToken);

    public async Task CompletePayloadPurgeAsync(Guid tenantId,Guid payloadGovernanceId,bool succeeded,string? error,CancellationToken cancellationToken=default)
    {
        const string sql="UPDATE DMS.IntakePayloadGovernance SET StatusCode=CASE WHEN @Succeeded=1 THEN N'PURGED' ELSE N'PURGE_FAILED' END,PurgedDateUtc=CASE WHEN @Succeeded=1 THEN SYSUTCDATETIME() END,PurgeReason=@Error,ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IntakePayloadGovernanceId=@Id AND StatusCode=N'PURGE_PENDING';";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,Id=payloadGovernanceId,Succeeded=succeeded,Error=error},cancellationToken:cancellationToken));
    }

    public Task PlaceLegalHoldAsync(PlaceDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken=default)=>TransactionAsync(async(connection,transaction)=>
    {
        const string sql="""
            IF NOT EXISTS(SELECT 1 FROM DMS.IntakeSession WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId) THROW 51000,'Intake session not found for tenant.',1;
            IF NOT EXISTS(SELECT 1 FROM DMS.IntakeLegalHold WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND HoldCode=@HoldCode AND StatusCode=N'ACTIVE') INSERT DMS.IntakeLegalHold(TenantId,IntakeSessionId,HoldCode,Reason,PlacedByUserId) VALUES(@TenantId,@IntakeSessionId,UPPER(@HoldCode),@Reason,@ActorUserId);
            UPDATE DMS.IntakePayloadGovernance SET LegalHoldCount=(SELECT COUNT(1) FROM DMS.IntakeLegalHold WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND StatusCode=N'ACTIVE'),StatusCode=N'HELD',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND StatusCode<>N'PURGED';
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,command,transaction,cancellationToken:cancellationToken));
    },cancellationToken);

    public Task ReleaseLegalHoldAsync(ReleaseDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken=default)=>TransactionAsync(async(connection,transaction)=>
    {
        const string sql="""
            DECLARE @SessionId UNIQUEIDENTIFIER; SELECT @SessionId=IntakeSessionId FROM DMS.IntakeLegalHold WITH(UPDLOCK) WHERE TenantId=@TenantId AND IntakeLegalHoldId=@LegalHoldId AND StatusCode=N'ACTIVE' AND RowVersion=@RowVersion; IF @SessionId IS NULL THROW 51000,'Legal hold changed or was not found for tenant.',1;
            UPDATE DMS.IntakeLegalHold SET StatusCode=N'RELEASED',ReleasedByUserId=@ActorUserId,ReleasedDateUtc=SYSUTCDATETIME(),ReleaseReason=@Reason WHERE TenantId=@TenantId AND IntakeLegalHoldId=@LegalHoldId;
            DECLARE @Count INT=(SELECT COUNT(1) FROM DMS.IntakeLegalHold WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND StatusCode=N'ACTIVE'); UPDATE DMS.IntakePayloadGovernance SET LegalHoldCount=@Count,StatusCode=CASE WHEN @Count=0 THEN N'ACTIVE' ELSE N'HELD' END,ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND StatusCode<>N'PURGED';
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,command,transaction,cancellationToken:cancellationToken));
    },cancellationToken);

    public async Task<IReadOnlyCollection<DocumentIntakePromptSuiteDto>> GetPromptSuitesAsync(Guid? tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT suite.AiPromptEvaluationSuiteId SuiteId,suite.TenantId,suite.PromptCode,suite.SuiteName,suite.Description,suite.MinimumPassRate,suite.MinimumAverageScore,COUNT(CASE WHEN test.IsActive=1 THEN 1 END) ActiveCaseCount,suite.IsActive,suite.RowVersion FROM DMS.AiPromptEvaluationSuite suite LEFT JOIN DMS.AiPromptEvaluationCase test ON test.AiPromptEvaluationSuiteId=suite.AiPromptEvaluationSuiteId WHERE (suite.TenantId=@TenantId OR suite.TenantId IS NULL) GROUP BY suite.AiPromptEvaluationSuiteId,suite.TenantId,suite.PromptCode,suite.SuiteName,suite.Description,suite.MinimumPassRate,suite.MinimumAverageScore,suite.IsActive,suite.RowVersion ORDER BY suite.PromptCode,suite.SuiteName;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DocumentIntakePromptSuiteDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<IReadOnlyCollection<DocumentIntakePromptEvaluationRunDto>> GetPromptEvaluationRunsAsync(Guid? tenantId,int pageSize=100,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT TOP(@PageSize) run.AiPromptEvaluationRunId RunId,run.TenantId,run.AiPromptDefinitionId PromptDefinitionId,run.AiPromptEvaluationSuiteId SuiteId,prompt.PromptCode,prompt.VersionLabel PromptVersion,run.StatusCode,run.TotalCaseCount,run.PassedCaseCount,run.PassRate,run.AverageScore,run.CreatedDateUtc,run.CompletedDateUtc,run.RowVersion FROM DMS.AiPromptEvaluationRun run JOIN DMS.AiPromptDefinition prompt ON prompt.AiPromptDefinitionId=run.AiPromptDefinitionId WHERE (run.TenantId=@TenantId OR run.TenantId IS NULL) ORDER BY run.CreatedDateUtc DESC;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DocumentIntakePromptEvaluationRunDto>(new CommandDefinition(sql,new{TenantId=tenantId,PageSize=Math.Clamp(pageSize,1,500)},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<Guid> QueuePromptEvaluationAsync(QueuePromptEvaluationCommand command,CancellationToken cancellationToken=default)
    {
        const string sql="""
            IF NOT EXISTS(SELECT 1 FROM DMS.AiPromptDefinition WHERE AiPromptDefinitionId=@PromptDefinitionId AND (TenantId=@TenantId OR TenantId IS NULL) AND StatusCode=N'DRAFT') THROW 51000,'Draft prompt was not found for tenant.',1;
            IF NOT EXISTS(SELECT 1 FROM DMS.AiPromptEvaluationSuite WHERE AiPromptEvaluationSuiteId=@SuiteId AND (TenantId=@TenantId OR TenantId IS NULL) AND IsActive=1) THROW 51000,'Active evaluation suite was not found for tenant.',1;
            DECLARE @Id UNIQUEIDENTIFIER=NEWID(); INSERT DMS.AiPromptEvaluationRun(AiPromptEvaluationRunId,TenantId,AiPromptDefinitionId,AiPromptEvaluationSuiteId,StatusCode,TotalCaseCount,RequestedByUserId,CorrelationId) SELECT @Id,@TenantId,@PromptDefinitionId,@SuiteId,N'QUEUED',COUNT(1),@ActorUserId,@CorrelationId FROM DMS.AiPromptEvaluationCase WHERE AiPromptEvaluationSuiteId=@SuiteId AND IsActive=1; SELECT @Id;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,new{command.TenantId,command.PromptDefinitionId,command.SuiteId,command.ActorUserId,command.CorrelationId},cancellationToken:cancellationToken));
    }

    public async Task<IReadOnlyCollection<DocumentIntakePromptEvaluationWorkDto>> LeasePromptEvaluationsAsync(string leaseOwner,int batchSize,CancellationToken cancellationToken=default)
    {
        const string sql="""
            DECLARE @Leased TABLE(RunId UNIQUEIDENTIFIER);
            ;WITH candidates AS(SELECT TOP(@BatchSize)* FROM DMS.AiPromptEvaluationRun WITH(UPDLOCK,READPAST,ROWLOCK) WHERE StatusCode=N'QUEUED' ORDER BY CreatedDateUtc)
            UPDATE candidates SET StatusCode=N'PROCESSING',StartedDateUtc=SYSUTCDATETIME() OUTPUT inserted.AiPromptEvaluationRunId INTO @Leased;
            SELECT run.AiPromptEvaluationRunId RunId,run.TenantId,run.AiPromptDefinitionId PromptDefinitionId,run.AiPromptEvaluationSuiteId SuiteId,prompt.PromptCode,prompt.VersionLabel PromptVersion,prompt.SystemPrompt,prompt.OutputSchemaJson,run.CorrelationId FROM DMS.AiPromptEvaluationRun run JOIN @Leased leased ON leased.RunId=run.AiPromptEvaluationRunId JOIN DMS.AiPromptDefinition prompt ON prompt.AiPromptDefinitionId=run.AiPromptDefinitionId;
            SELECT test.AiPromptEvaluationSuiteId SuiteId,test.AiPromptEvaluationCaseId CaseId,test.CaseName,test.InputPayloadReference,test.ExpectedOutputJson,test.EvaluationRulesJson FROM DMS.AiPromptEvaluationCase test JOIN DMS.AiPromptEvaluationRun run ON run.AiPromptEvaluationSuiteId=test.AiPromptEvaluationSuiteId JOIN @Leased leased ON leased.RunId=run.AiPromptEvaluationRunId WHERE test.IsActive=1 ORDER BY test.CaseName;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{LeaseOwner=leaseOwner,BatchSize=Math.Clamp(batchSize,1,20)},cancellationToken:cancellationToken));var runs=(await multi.ReadAsync<PromptRunRow>()).AsList();var cases=(await multi.ReadAsync<PromptCaseRow>()).AsList();return runs.Select(run=>new DocumentIntakePromptEvaluationWorkDto(run.RunId,run.TenantId,run.PromptDefinitionId,run.SuiteId,run.PromptCode,run.PromptVersion,run.SystemPrompt,run.OutputSchemaJson,run.CorrelationId,cases.Where(test=>test.SuiteId==run.SuiteId).Select(test=>new DocumentIntakePromptEvaluationCaseDto(test.CaseId,test.CaseName,test.InputPayloadReference,test.ExpectedOutputJson,test.EvaluationRulesJson)).ToArray())).ToArray();
    }

    public Task CompletePromptEvaluationAsync(Guid runId,IReadOnlyCollection<DocumentIntakePromptEvaluationCaseResult> results,CancellationToken cancellationToken=default)=>TransactionAsync(async(connection,transaction)=>
    {
        foreach(var result in results)await connection.ExecuteAsync(new CommandDefinition("""
            INSERT DMS.AiPromptEvaluationResult(TenantId,AiPromptEvaluationRunId,AiPromptEvaluationCaseId,StatusCode,Score,ActualOutputReference,DifferenceJson,ErrorCode,ErrorMessage,DurationMilliseconds) SELECT run.TenantId,@RunId,@CaseId,@StatusCode,@Score,@ActualOutputReference,@DifferenceJson,@ErrorCode,@ErrorMessage,@DurationMilliseconds FROM DMS.AiPromptEvaluationRun run WHERE run.AiPromptEvaluationRunId=@RunId;
            """,new{RunId=runId,result.CaseId,result.StatusCode,result.Score,result.ActualOutputReference,result.DifferenceJson,result.ErrorCode,result.ErrorMessage,result.DurationMilliseconds},transaction,cancellationToken:cancellationToken));
        const string complete="""
            UPDATE run SET TotalCaseCount=summary.TotalCount,PassedCaseCount=summary.PassedCount,PassRate=summary.PassRate,AverageScore=summary.AverageScore,StatusCode=CASE WHEN summary.PassRate>=suite.MinimumPassRate AND summary.AverageScore>=suite.MinimumAverageScore THEN N'PASSED' ELSE N'FAILED' END,CompletedDateUtc=SYSUTCDATETIME() FROM DMS.AiPromptEvaluationRun run JOIN DMS.AiPromptEvaluationSuite suite ON suite.AiPromptEvaluationSuiteId=run.AiPromptEvaluationSuiteId CROSS APPLY(SELECT COUNT(1) TotalCount,SUM(CASE WHEN result.StatusCode=N'PASSED' THEN 1 ELSE 0 END) PassedCount,CAST(SUM(CASE WHEN result.StatusCode=N'PASSED' THEN 1.0 ELSE 0 END)/NULLIF(COUNT(1),0) AS DECIMAL(5,4)) PassRate,CAST(AVG(result.Score) AS DECIMAL(5,4)) AverageScore FROM DMS.AiPromptEvaluationResult result WHERE result.AiPromptEvaluationRunId=run.AiPromptEvaluationRunId) summary WHERE run.AiPromptEvaluationRunId=@RunId AND run.StatusCode=N'PROCESSING';
            """;
        await connection.ExecuteAsync(new CommandDefinition(complete,new{RunId=runId},transaction,cancellationToken:cancellationToken));
    },cancellationToken);

    public Task ApprovePromptAsync(ApproveDocumentIntakePromptCommand command,bool requirePassedRun,CancellationToken cancellationToken=default)=>TransactionAsync(async(connection,transaction)=>
    {
        const string sql="""
            IF @RequirePassedRun=1 AND NOT EXISTS(SELECT 1 FROM DMS.AiPromptEvaluationRun run JOIN DMS.AiPromptEvaluationSuite suite ON suite.AiPromptEvaluationSuiteId=run.AiPromptEvaluationSuiteId WHERE run.AiPromptEvaluationRunId=@EvaluationRunId AND run.AiPromptDefinitionId=@PromptDefinitionId AND run.StatusCode=N'PASSED' AND run.PassRate>=suite.MinimumPassRate AND run.AverageScore>=suite.MinimumAverageScore) THROW 51000,'A passing evaluation run is required before prompt approval.',1;
            UPDATE DMS.AiPromptDefinition SET StatusCode=N'APPROVED',ApprovedByUserId=@ActorUserId,ApprovedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME() WHERE AiPromptDefinitionId=@PromptDefinitionId AND (TenantId=@TenantId OR (@TenantId IS NULL AND TenantId IS NULL)) AND StatusCode=N'DRAFT' AND RowVersion=@PromptRowVersion; IF @@ROWCOUNT=0 THROW 51000,'Prompt changed or cannot be approved.',1;
            INSERT DMS.AiPromptApproval(TenantId,AiPromptDefinitionId,AiPromptEvaluationRunId,DecisionCode,DecisionReason,DecidedByUserId) VALUES(@TenantId,@PromptDefinitionId,@EvaluationRunId,N'APPROVED',@Reason,@ActorUserId);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,new{command.TenantId,command.PromptDefinitionId,command.EvaluationRunId,command.Reason,command.ActorUserId,command.PromptRowVersion,RequirePassedRun=requirePassedRun},transaction,cancellationToken:cancellationToken));
    },cancellationToken);

    public async Task<DocumentIntakeTelemetryDto> CaptureTelemetrySnapshotAsync(CancellationToken cancellationToken=default)
    {
        const string sql="""
            DECLARE @Start DATETIME2=DATEADD(MINUTE,-5,SYSUTCDATETIME()),@End DATETIME2=SYSUTCDATETIME();
            DECLARE @Queue INT=(SELECT COUNT(1) FROM DMS.IntakeWorkItem WHERE StatusCode=N'PENDING'),@Oldest BIGINT=COALESCE((SELECT DATEDIFF_BIG(SECOND,MIN(AvailableDateUtc),@End) FROM DMS.IntakeWorkItem WHERE StatusCode=N'PENDING'),0),@Processing INT=(SELECT COUNT(1) FROM DMS.IntakeWorkItem WHERE StatusCode=N'PROCESSING'),@Retry INT=(SELECT COUNT(1) FROM DMS.IntakeWorkItem WHERE StatusCode=N'RETRY_SCHEDULED'),@Dead INT=(SELECT COUNT(1) FROM DMS.IntakeWorkItem WHERE StatusCode=N'DEAD_LETTERED'),@Completed INT=(SELECT COUNT(1) FROM DMS.IntakeWorkAttempt WHERE StatusCode=N'COMPLETED' AND CompletedDateUtc>=@Start),@Failed INT=(SELECT COUNT(1) FROM DMS.IntakeWorkAttempt WHERE StatusCode=N'FAILED' AND CompletedDateUtc>=@Start),@Throttles INT=(SELECT COUNT(1) FROM DMS.IntakeWorkAttempt WHERE ErrorCode LIKE N'%429%' AND CompletedDateUtc>=@Start),@Input BIGINT=COALESCE((SELECT SUM(CONVERT(BIGINT,InputTokenCount)) FROM DMS.AiExecution WHERE CreatedDateUtc>=@Start),0),@Output BIGINT=COALESCE((SELECT SUM(CONVERT(BIGINT,OutputTokenCount)) FROM DMS.AiExecution WHERE CreatedDateUtc>=@Start),0);
            INSERT DMS.IntakeTelemetrySnapshot(TenantId,WindowStartUtc,WindowEndUtc,QueueDepth,OldestQueuedAgeSeconds,ProcessingCount,RetryCount,DeadLetterCount,CompletedCount,FailedCount,ProviderThrottleCount,InputTokenCount,OutputTokenCount) VALUES(NULL,@Start,@End,@Queue,@Oldest,@Processing,@Retry,@Dead,@Completed,@Failed,@Throttles,@Input,@Output);
            SELECT @Start WindowStartUtc,@End WindowEndUtc,@Queue QueueDepth,@Oldest OldestQueuedAgeSeconds,@Processing ProcessingCount,@Retry RetryCount,@Dead DeadLetterCount,@Completed CompletedCount,@Failed FailedCount,CAST(NULL AS BIGINT) P50DurationMilliseconds,CAST(NULL AS BIGINT) P95DurationMilliseconds,@Throttles ProviderThrottleCount,@Input InputTokenCount,@Output OutputTokenCount;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<DocumentIntakeTelemetryDto>(new CommandDefinition(sql,cancellationToken:cancellationToken));
    }

    public async Task EvaluateSlosAsync(DocumentIntakeTelemetryDto snapshot,CancellationToken cancellationToken=default)
    {
        const string sql="""
            DECLARE @Metrics TABLE(MetricCode NVARCHAR(100),MetricValue DECIMAL(18,4)); INSERT @Metrics VALUES(N'QueueDepth',@QueueDepth),(N'OldestQueuedAgeSeconds',@OldestQueuedAgeSeconds),(N'DeadLetterCount',@DeadLetterCount);
            MERGE DMS.IntakeAlertIncident target USING(SELECT slo.IntakeSloDefinitionId,slo.TenantId,slo.SloCode,slo.DisplayName,metric.MetricValue,slo.WarningValue,slo.CriticalValue,CASE WHEN metric.MetricValue>=slo.CriticalValue THEN N'CRITICAL' WHEN metric.MetricValue>=slo.WarningValue THEN N'WARNING' END SeverityCode FROM DMS.IntakeSloDefinition slo JOIN @Metrics metric ON metric.MetricCode=slo.MetricCode WHERE slo.IsActive=1 AND metric.MetricValue>=slo.WarningValue) source ON target.IntakeSloDefinitionId=source.IntakeSloDefinitionId AND target.StatusCode IN(N'OPEN',N'ACKNOWLEDGED')
            WHEN MATCHED THEN UPDATE SET SeverityCode=source.SeverityCode,MetricValue=source.MetricValue,ThresholdValue=CASE WHEN source.SeverityCode=N'CRITICAL' THEN source.CriticalValue ELSE source.WarningValue END,Summary=CONCAT(source.DisplayName,N' is ',source.MetricValue,N'.'),LastObservedDateUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(TenantId,IntakeSloDefinitionId,SeverityCode,StatusCode,MetricValue,ThresholdValue,Summary,FirstObservedDateUtc,LastObservedDateUtc) VALUES(source.TenantId,source.IntakeSloDefinitionId,source.SeverityCode,N'OPEN',source.MetricValue,CASE WHEN source.SeverityCode=N'CRITICAL' THEN source.CriticalValue ELSE source.WarningValue END,CONCAT(source.DisplayName,N' is ',source.MetricValue,N'.'),SYSUTCDATETIME(),SYSUTCDATETIME());
            UPDATE incident SET StatusCode=N'RESOLVED',ResolvedDateUtc=SYSUTCDATETIME(),LastObservedDateUtc=SYSUTCDATETIME() FROM DMS.IntakeAlertIncident incident JOIN DMS.IntakeSloDefinition slo ON slo.IntakeSloDefinitionId=incident.IntakeSloDefinitionId JOIN @Metrics metric ON metric.MetricCode=slo.MetricCode WHERE incident.StatusCode IN(N'OPEN',N'ACKNOWLEDGED') AND metric.MetricValue<slo.WarningValue;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{snapshot.QueueDepth,snapshot.OldestQueuedAgeSeconds,snapshot.DeadLetterCount},cancellationToken:cancellationToken));
    }

    public async Task<IReadOnlyCollection<DocumentIntakeAlertDto>> GetAlertsAsync(Guid? tenantId,bool openOnly=true,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT incident.IntakeAlertIncidentId AlertId,incident.TenantId,slo.SloCode,slo.DisplayName,incident.SeverityCode,incident.StatusCode,incident.MetricValue,incident.ThresholdValue,incident.Summary,incident.FirstObservedDateUtc,incident.LastObservedDateUtc,incident.RowVersion FROM DMS.IntakeAlertIncident incident JOIN DMS.IntakeSloDefinition slo ON slo.IntakeSloDefinitionId=incident.IntakeSloDefinitionId WHERE (incident.TenantId=@TenantId OR incident.TenantId IS NULL) AND (@OpenOnly=0 OR incident.StatusCode<>N'RESOLVED') ORDER BY CASE incident.SeverityCode WHEN N'CRITICAL' THEN 1 ELSE 2 END,incident.LastObservedDateUtc DESC;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DocumentIntakeAlertDto>(new CommandDefinition(sql,new{TenantId=tenantId,OpenOnly=openOnly},cancellationToken:cancellationToken))).AsList();
    }

    private async Task<DocumentIntakeRuntimeSettings> GetSettingsWithinAsync(IDbConnection connection,IDbTransaction transaction,Guid tenantId,CancellationToken token)
    {
        const string sql="SELECT platform.SettingKey,COALESCE(tenant.SettingValue,platform.SettingValue,platform.DefaultValue) SettingValue FROM Core.ConfigurationSetting platform LEFT JOIN Core.ConfigurationSetting tenant ON tenant.TenantId=@TenantId AND tenant.ScopeCode=N'Tenant' AND tenant.SettingKey=platform.SettingKey AND tenant.IsDeleted=0 WHERE platform.TenantId IS NULL AND platform.ScopeCode=N'Platform' AND platform.ModuleCode=N'DocumentIntake' AND platform.IsDeleted=0;";
        var values=(await connection.QueryAsync<SettingRow>(new CommandDefinition(sql,new{TenantId=tenantId},transaction,cancellationToken:token))).ToDictionary(x=>x.SettingKey,x=>x.SettingValue,StringComparer.OrdinalIgnoreCase);
        int Integer(string key,int fallback)=>int.TryParse(values.GetValueOrDefault(key),out var value)?value:fallback;
        return new(10,10,300,true,true,"MICROSOFT_DEFENDER_STORAGE",15,90,100,60,true,true,5,Integer("DocumentIntake.DeadLetter.ReplayMaxAttempts",3),true);
    }

    private async Task TransactionAsync(Func<IDbConnection,IDbTransaction,Task> action,CancellationToken token){using var connection=await connectionFactory.CreateOpenConnectionAsync(token);using var transaction=connection.BeginTransaction();try{await action(connection,transaction);transaction.Commit();}catch{transaction.Rollback();throw;}}
    private async Task<T> TransactionResultAsync<T>(Func<IDbConnection,IDbTransaction,Task<T>> action,CancellationToken token){using var connection=await connectionFactory.CreateOpenConnectionAsync(token);using var transaction=connection.BeginTransaction();try{var result=await action(connection,transaction);transaction.Commit();return result;}catch{transaction.Rollback();throw;}}
    private sealed record SettingRow(string SettingKey,string? SettingValue);
    private sealed record PromptRunRow(Guid RunId,Guid? TenantId,Guid PromptDefinitionId,Guid SuiteId,string PromptCode,string PromptVersion,string SystemPrompt,string OutputSchemaJson,string CorrelationId);
    private sealed record PromptCaseRow(Guid SuiteId,Guid CaseId,string CaseName,string InputPayloadReference,string ExpectedOutputJson,string EvaluationRulesJson);
}
