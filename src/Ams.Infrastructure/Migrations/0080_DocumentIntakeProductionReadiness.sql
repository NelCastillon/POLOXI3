SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'DMS.IntakeMalwareScan', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeMalwareScan
	(
		IntakeMalwareScanId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeMalwareScan PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		DocumentId UNIQUEIDENTIFIER NOT NULL,
		StoragePath NVARCHAR(1000) NOT NULL,
		ProviderCode NVARCHAR(100) NOT NULL CONSTRAINT DF_DMS_IntakeMalwareScan_Provider DEFAULT N'MICROSOFT_DEFENDER_STORAGE',
		StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_IntakeMalwareScan_Status DEFAULT N'PENDING',
		ThreatName NVARCHAR(500) NULL,
		ProviderResult NVARCHAR(2000) NULL,
		ScanRequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeMalwareScan_Requested DEFAULT SYSUTCDATETIME(),
		ScanCompletedDateUtc DATETIME2 NULL,
		QuarantinedDateUtc DATETIME2 NULL,
		ReleasedDateUtc DATETIME2 NULL,
		ReleasedByUserId UNIQUEIDENTIFIER NULL,
		ReleaseReason NVARCHAR(2000) NULL,
		ModifiedDateUtc DATETIME2 NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_DMS_IntakeMalwareScan_Status CHECK (StatusCode IN (N'PENDING',N'CLEAN',N'INFECTED',N'ERROR',N'QUARANTINED',N'RELEASED'))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakeMalwareScan') AND name=N'UX_DMS_IntakeMalwareScan_Document')
	CREATE UNIQUE INDEX UX_DMS_IntakeMalwareScan_Document ON DMS.IntakeMalwareScan(TenantId,DocumentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakeMalwareScan') AND name=N'IX_DMS_IntakeMalwareScan_Status')
	CREATE INDEX IX_DMS_IntakeMalwareScan_Status ON DMS.IntakeMalwareScan(StatusCode,ScanRequestedDateUtc) INCLUDE(TenantId,DocumentId,StoragePath);

INSERT DMS.IntakeMalwareScan(TenantId,DocumentId,StoragePath,StatusCode)
SELECT DISTINCT link.TenantId,link.DocumentId,document.StoragePath,N'PENDING'
FROM DMS.IntakeSessionDocument link
JOIN DMS.Document document ON document.TenantId=link.TenantId AND document.DocumentId=link.DocumentId AND document.IsDeleted=0
WHERE NOT EXISTS(SELECT 1 FROM DMS.IntakeMalwareScan scan WHERE scan.TenantId=link.TenantId AND scan.DocumentId=link.DocumentId);

IF OBJECT_ID(N'DMS.IntakePayloadGovernance', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakePayloadGovernance
	(
		IntakePayloadGovernanceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakePayloadGovernance PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		StorageReference NVARCHAR(1000) NOT NULL,
		PayloadTypeCode NVARCHAR(50) NOT NULL,
		ContainsPii BIT NOT NULL CONSTRAINT DF_DMS_IntakePayloadGovernance_Pii DEFAULT 1,
		RetainUntilDateUtc DATETIME2 NOT NULL,
		LegalHoldCount INT NOT NULL CONSTRAINT DF_DMS_IntakePayloadGovernance_Holds DEFAULT 0,
		StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_IntakePayloadGovernance_Status DEFAULT N'ACTIVE',
		PurgedDateUtc DATETIME2 NULL,
		PurgeReason NVARCHAR(1000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakePayloadGovernance_Created DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc DATETIME2 NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_DMS_IntakePayloadGovernance_Status CHECK (StatusCode IN (N'ACTIVE',N'HELD',N'PURGE_PENDING',N'PURGED',N'PURGE_FAILED')),
		CONSTRAINT CK_DMS_IntakePayloadGovernance_Holds CHECK (LegalHoldCount>=0)
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakePayloadGovernance') AND name=N'UX_DMS_IntakePayloadGovernance_Reference')
	CREATE UNIQUE INDEX UX_DMS_IntakePayloadGovernance_Reference ON DMS.IntakePayloadGovernance(TenantId,StorageReference);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakePayloadGovernance') AND name=N'IX_DMS_IntakePayloadGovernance_Purge')
	CREATE INDEX IX_DMS_IntakePayloadGovernance_Purge ON DMS.IntakePayloadGovernance(StatusCode,RetainUntilDateUtc) INCLUDE(TenantId,IntakeSessionId,StorageReference,LegalHoldCount);

IF OBJECT_ID(N'DMS.IntakeLegalHold', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeLegalHold
	(
		IntakeLegalHoldId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeLegalHold PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		HoldCode NVARCHAR(100) NOT NULL,
		Reason NVARCHAR(2000) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_IntakeLegalHold_Status DEFAULT N'ACTIVE',
		PlacedByUserId UNIQUEIDENTIFIER NOT NULL,
		PlacedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeLegalHold_Placed DEFAULT SYSUTCDATETIME(),
		ReleasedByUserId UNIQUEIDENTIFIER NULL,
		ReleasedDateUtc DATETIME2 NULL,
		ReleaseReason NVARCHAR(2000) NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_DMS_IntakeLegalHold_Status CHECK (StatusCode IN (N'ACTIVE',N'RELEASED'))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakeLegalHold') AND name=N'UX_DMS_IntakeLegalHold_Active')
	CREATE UNIQUE INDEX UX_DMS_IntakeLegalHold_Active ON DMS.IntakeLegalHold(TenantId,IntakeSessionId,HoldCode) WHERE StatusCode=N'ACTIVE';

IF OBJECT_ID(N'DMS.IntakePayloadAccessAudit', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakePayloadAccessAudit
	(
		IntakePayloadAccessAuditId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakePayloadAccessAudit PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		StorageReference NVARCHAR(1000) NOT NULL,
		ActionCode NVARCHAR(50) NOT NULL,
		ActorTypeCode NVARCHAR(30) NOT NULL,
		ActorId NVARCHAR(200) NOT NULL,
		CorrelationId NVARCHAR(120) NULL,
		Purpose NVARCHAR(500) NOT NULL,
		OutcomeCode NVARCHAR(30) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakePayloadAccessAudit_Created DEFAULT SYSUTCDATETIME()
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakePayloadAccessAudit') AND name=N'IX_DMS_IntakePayloadAccessAudit_Session')
	CREATE INDEX IX_DMS_IntakePayloadAccessAudit_Session ON DMS.IntakePayloadAccessAudit(TenantId,IntakeSessionId,CreatedDateUtc DESC);

IF OBJECT_ID(N'DMS.IntakeWorkReplayHistory', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeWorkReplayHistory
	(
		IntakeWorkReplayHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeWorkReplayHistory PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeWorkItemId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		PreviousStatusCode NVARCHAR(30) NOT NULL,
		ReplayFromWorkTypeCode NVARCHAR(50) NOT NULL,
		Reason NVARCHAR(2000) NOT NULL,
		ReplayedByUserId UNIQUEIDENTIFIER NOT NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeWorkReplayHistory_Created DEFAULT SYSUTCDATETIME()
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakeWorkReplayHistory') AND name=N'IX_DMS_IntakeWorkReplayHistory_Work')
	CREATE INDEX IX_DMS_IntakeWorkReplayHistory_Work ON DMS.IntakeWorkReplayHistory(TenantId,IntakeWorkItemId,CreatedDateUtc DESC);

IF OBJECT_ID(N'DMS.AiPromptEvaluationSuite', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.AiPromptEvaluationSuite
	(
		AiPromptEvaluationSuiteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_AiPromptEvaluationSuite PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		PromptCode NVARCHAR(100) NOT NULL,
		SuiteName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(2000) NULL,
		MinimumPassRate DECIMAL(5,4) NOT NULL,
		MinimumAverageScore DECIMAL(5,4) NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationSuite_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationSuite_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_DMS_AiPromptEvaluationSuite_Thresholds CHECK (MinimumPassRate BETWEEN 0 AND 1 AND MinimumAverageScore BETWEEN 0 AND 1)
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.AiPromptEvaluationSuite') AND name=N'UX_DMS_AiPromptEvaluationSuite_Name')
	CREATE UNIQUE INDEX UX_DMS_AiPromptEvaluationSuite_Name ON DMS.AiPromptEvaluationSuite(TenantId,PromptCode,SuiteName);

IF OBJECT_ID(N'DMS.AiPromptEvaluationCase', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.AiPromptEvaluationCase
	(
		AiPromptEvaluationCaseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_AiPromptEvaluationCase PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		AiPromptEvaluationSuiteId UNIQUEIDENTIFIER NOT NULL,
		CaseName NVARCHAR(200) NOT NULL,
		InputPayloadReference NVARCHAR(1000) NOT NULL,
		ExpectedOutputJson NVARCHAR(MAX) NOT NULL,
		EvaluationRulesJson NVARCHAR(MAX) NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationCase_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationCase_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		RowVersion ROWVERSION NOT NULL
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.AiPromptEvaluationCase') AND name=N'UX_DMS_AiPromptEvaluationCase_Name')
	CREATE UNIQUE INDEX UX_DMS_AiPromptEvaluationCase_Name ON DMS.AiPromptEvaluationCase(AiPromptEvaluationSuiteId,CaseName);

IF OBJECT_ID(N'DMS.AiPromptEvaluationRun', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.AiPromptEvaluationRun
	(
		AiPromptEvaluationRunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_AiPromptEvaluationRun PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		AiPromptDefinitionId UNIQUEIDENTIFIER NOT NULL,
		AiPromptEvaluationSuiteId UNIQUEIDENTIFIER NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationRun_Status DEFAULT N'QUEUED',
		TotalCaseCount INT NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationRun_Total DEFAULT 0,
		PassedCaseCount INT NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationRun_Passed DEFAULT 0,
		PassRate DECIMAL(5,4) NULL,
		AverageScore DECIMAL(5,4) NULL,
		StartedDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		RequestedByUserId UNIQUEIDENTIFIER NOT NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationRun_Created DEFAULT SYSUTCDATETIME(),
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_DMS_AiPromptEvaluationRun_Status CHECK (StatusCode IN (N'QUEUED',N'PROCESSING',N'PASSED',N'FAILED',N'CANCELLED'))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.AiPromptEvaluationRun') AND name=N'IX_DMS_AiPromptEvaluationRun_Queue')
	CREATE INDEX IX_DMS_AiPromptEvaluationRun_Queue ON DMS.AiPromptEvaluationRun(StatusCode,CreatedDateUtc) INCLUDE(TenantId,AiPromptDefinitionId,AiPromptEvaluationSuiteId);

IF OBJECT_ID(N'DMS.AiPromptEvaluationResult', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.AiPromptEvaluationResult
	(
		AiPromptEvaluationResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_AiPromptEvaluationResult PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		AiPromptEvaluationRunId UNIQUEIDENTIFIER NOT NULL,
		AiPromptEvaluationCaseId UNIQUEIDENTIFIER NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		Score DECIMAL(5,4) NOT NULL,
		ActualOutputReference NVARCHAR(1000) NULL,
		DifferenceJson NVARCHAR(MAX) NULL,
		ErrorCode NVARCHAR(100) NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		DurationMilliseconds BIGINT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_AiPromptEvaluationResult_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT CK_DMS_AiPromptEvaluationResult_Status CHECK (StatusCode IN (N'PASSED',N'FAILED',N'ERROR')),
		CONSTRAINT CK_DMS_AiPromptEvaluationResult_Score CHECK (Score BETWEEN 0 AND 1)
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.AiPromptEvaluationResult') AND name=N'UX_DMS_AiPromptEvaluationResult_Case')
	CREATE UNIQUE INDEX UX_DMS_AiPromptEvaluationResult_Case ON DMS.AiPromptEvaluationResult(AiPromptEvaluationRunId,AiPromptEvaluationCaseId);

IF OBJECT_ID(N'DMS.AiPromptApproval', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.AiPromptApproval
	(
		AiPromptApprovalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_AiPromptApproval PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		AiPromptDefinitionId UNIQUEIDENTIFIER NOT NULL,
		AiPromptEvaluationRunId UNIQUEIDENTIFIER NOT NULL,
		DecisionCode NVARCHAR(30) NOT NULL,
		DecisionReason NVARCHAR(2000) NOT NULL,
		DecidedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_AiPromptApproval_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT CK_DMS_AiPromptApproval_Decision CHECK (DecisionCode IN (N'APPROVED',N'REJECTED',N'REVOKED'))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.AiPromptApproval') AND name=N'IX_DMS_AiPromptApproval_Prompt')
	CREATE INDEX IX_DMS_AiPromptApproval_Prompt ON DMS.AiPromptApproval(TenantId,AiPromptDefinitionId,CreatedDateUtc DESC);

IF OBJECT_ID(N'DMS.IntakeTelemetrySnapshot', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeTelemetrySnapshot
	(
		IntakeTelemetrySnapshotId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeTelemetrySnapshot PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		WindowStartUtc DATETIME2 NOT NULL,
		WindowEndUtc DATETIME2 NOT NULL,
		QueueDepth INT NOT NULL,
		OldestQueuedAgeSeconds BIGINT NOT NULL,
		ProcessingCount INT NOT NULL,
		RetryCount INT NOT NULL,
		DeadLetterCount INT NOT NULL,
		CompletedCount INT NOT NULL,
		FailedCount INT NOT NULL,
		P50DurationMilliseconds BIGINT NULL,
		P95DurationMilliseconds BIGINT NULL,
		ProviderThrottleCount INT NOT NULL,
		InputTokenCount BIGINT NOT NULL,
		OutputTokenCount BIGINT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeTelemetrySnapshot_Created DEFAULT SYSUTCDATETIME()
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakeTelemetrySnapshot') AND name=N'IX_DMS_IntakeTelemetrySnapshot_Window')
	CREATE INDEX IX_DMS_IntakeTelemetrySnapshot_Window ON DMS.IntakeTelemetrySnapshot(TenantId,WindowEndUtc DESC);

IF OBJECT_ID(N'DMS.IntakeSloDefinition', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeSloDefinition
	(
		IntakeSloDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeSloDefinition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		SloCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		MetricCode NVARCHAR(100) NOT NULL,
		ComparisonCode NVARCHAR(10) NOT NULL,
		TargetValue DECIMAL(18,4) NOT NULL,
		WarningValue DECIMAL(18,4) NOT NULL,
		CriticalValue DECIMAL(18,4) NOT NULL,
		EvaluationWindowMinutes INT NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_DMS_IntakeSloDefinition_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeSloDefinition_Created DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc DATETIME2 NULL,
		RowVersion ROWVERSION NOT NULL
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakeSloDefinition') AND name=N'UX_DMS_IntakeSloDefinition_Code')
	CREATE UNIQUE INDEX UX_DMS_IntakeSloDefinition_Code ON DMS.IntakeSloDefinition(TenantId,SloCode);

IF OBJECT_ID(N'DMS.IntakeAlertIncident', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeAlertIncident
	(
		IntakeAlertIncidentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeAlertIncident PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		IntakeSloDefinitionId UNIQUEIDENTIFIER NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_IntakeAlertIncident_Status DEFAULT N'OPEN',
		MetricValue DECIMAL(18,4) NOT NULL,
		ThresholdValue DECIMAL(18,4) NOT NULL,
		Summary NVARCHAR(1000) NOT NULL,
		FirstObservedDateUtc DATETIME2 NOT NULL,
		LastObservedDateUtc DATETIME2 NOT NULL,
		AcknowledgedByUserId UNIQUEIDENTIFIER NULL,
		AcknowledgedDateUtc DATETIME2 NULL,
		ResolvedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeAlertIncident_Created DEFAULT SYSUTCDATETIME(),
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_DMS_IntakeAlertIncident_Severity CHECK (SeverityCode IN (N'WARNING',N'CRITICAL')),
		CONSTRAINT CK_DMS_IntakeAlertIncident_Status CHECK (StatusCode IN (N'OPEN',N'ACKNOWLEDGED',N'RESOLVED'))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.IntakeAlertIncident') AND name=N'IX_DMS_IntakeAlertIncident_Open')
	CREATE INDEX IX_DMS_IntakeAlertIncident_Open ON DMS.IntakeAlertIncident(StatusCode,SeverityCode,LastObservedDateUtc DESC) INCLUDE(TenantId,IntakeSloDefinitionId);

IF OBJECT_ID(N'Core.ConfigurationSetting', N'U') IS NOT NULL
BEGIN
	MERGE Core.ConfigurationSetting AS target
	USING
	(
		SELECT * FROM (VALUES
			(N'DocumentIntake.Worker.BatchSize',N'10',N'Integer',N'Maximum work items leased in one intake cycle.'),
			(N'DocumentIntake.Worker.PollIntervalSeconds',N'10',N'Integer',N'Idle polling interval for the intake worker.'),
			(N'DocumentIntake.Worker.LeaseDurationSeconds',N'300',N'Integer',N'Exclusive processing lease duration.'),
			(N'DocumentIntake.Malware.Enabled',N'true',N'Boolean',N'Require malware scan evidence before intake processing.'),
			(N'DocumentIntake.Malware.FailClosed',N'true',N'Boolean',N'Block processing when malware status is unavailable.'),
			(N'DocumentIntake.Malware.ProviderCode',N'MICROSOFT_DEFENDER_STORAGE',N'Text',N'Authoritative malware scanning provider.'),
			(N'DocumentIntake.Malware.PendingTimeoutMinutes',N'15',N'Integer',N'Maximum time allowed for a pending malware scan.'),
			(N'DocumentIntake.Payload.RetentionDays',N'90',N'Integer',N'Default raw OCR and AI payload retention period.'),
			(N'DocumentIntake.Payload.PurgeBatchSize',N'100',N'Integer',N'Maximum payloads purged per retention cycle.'),
			(N'DocumentIntake.Payload.RetentionWorkerIntervalMinutes',N'60',N'Integer',N'Payload retention worker interval.'),
			(N'DocumentIntake.Payload.AccessAuditEnabled',N'true',N'Boolean',N'Record every raw payload read, write, and purge.'),
			(N'DocumentIntake.Telemetry.Enabled',N'true',N'Boolean',N'Enable intake metrics, traces, snapshots, and SLO evaluation.'),
			(N'DocumentIntake.Telemetry.SnapshotIntervalMinutes',N'5',N'Integer',N'Operational telemetry snapshot interval.'),
			(N'DocumentIntake.Telemetry.OtlpEndpoint',N'',N'Text',N'Optional OTLP exporter endpoint.'),
			(N'DocumentIntake.Slo.QueueDepth.Warning',N'100',N'Decimal',N'Queue depth warning threshold.'),
			(N'DocumentIntake.Slo.QueueDepth.Critical',N'500',N'Decimal',N'Queue depth critical threshold.'),
			(N'DocumentIntake.Slo.OldestQueuedAgeSeconds.Warning',N'300',N'Decimal',N'Oldest queued work warning threshold.'),
			(N'DocumentIntake.Slo.OldestQueuedAgeSeconds.Critical',N'900',N'Decimal',N'Oldest queued work critical threshold.'),
			(N'DocumentIntake.Slo.DeadLetterCount.Warning',N'1',N'Decimal',N'Dead-letter warning threshold.'),
			(N'DocumentIntake.Slo.DeadLetterCount.Critical',N'10',N'Decimal',N'Dead-letter critical threshold.'),
			(N'DocumentIntake.PromptEvaluation.MinimumPassRate',N'0.95',N'Decimal',N'Default minimum prompt evaluation pass rate.'),
			(N'DocumentIntake.PromptEvaluation.MinimumAverageScore',N'0.90',N'Decimal',N'Default minimum prompt evaluation average score.'),
			(N'DocumentIntake.PromptEvaluation.RequirePassedRunForApproval',N'true',N'Boolean',N'Require a passing evaluation run before prompt approval.'),
			(N'DocumentIntake.DeadLetter.ReplayMaxAttempts',N'3',N'Integer',N'Maximum operator replay cycles for dead-lettered work.'),
			(N'DocumentIntake.Health.ReadinessTimeoutSeconds',N'10',N'Integer',N'Per-provider readiness probe timeout.')
		) valueset(SettingKey,DefaultValue,DataTypeCode,Description)
	) source
	ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET DefaultValue=source.DefaultValue,DataTypeCode=source.DataTypeCode,Description=source.Description,ModuleCode=N'DocumentIntake',ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,SettingKey,SettingValue,DataTypeCode,DefaultValue,Description,IsEncrypted,IsReadOnly,ModuleCode,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,N'Platform',source.SettingKey,source.DefaultValue,source.DataTypeCode,source.DefaultValue,source.Description,0,0,N'DocumentIntake',SYSUTCDATETIME(),0);
END;

MERGE DMS.IntakeSloDefinition AS target
USING
(
	SELECT * FROM (VALUES
		(N'QUEUE_DEPTH',N'Queue depth',N'QueueDepth',N'LTE',CAST(0 AS DECIMAL(18,4)),CAST(100 AS DECIMAL(18,4)),CAST(500 AS DECIMAL(18,4)),5),
		(N'OLDEST_QUEUED_AGE',N'Oldest queued work age',N'OldestQueuedAgeSeconds',N'LTE',CAST(0 AS DECIMAL(18,4)),CAST(300 AS DECIMAL(18,4)),CAST(900 AS DECIMAL(18,4)),5),
		(N'DEAD_LETTERS',N'Dead-letter work items',N'DeadLetterCount',N'LTE',CAST(0 AS DECIMAL(18,4)),CAST(1 AS DECIMAL(18,4)),CAST(10 AS DECIMAL(18,4)),5)
	) valueset(SloCode,DisplayName,MetricCode,ComparisonCode,TargetValue,WarningValue,CriticalValue,EvaluationWindowMinutes)
) source
ON target.TenantId IS NULL AND target.SloCode=source.SloCode
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,MetricCode=source.MetricCode,ComparisonCode=source.ComparisonCode,TargetValue=source.TargetValue,WarningValue=source.WarningValue,CriticalValue=source.CriticalValue,EvaluationWindowMinutes=source.EvaluationWindowMinutes,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TenantId,SloCode,DisplayName,MetricCode,ComparisonCode,TargetValue,WarningValue,CriticalValue,EvaluationWindowMinutes,IsActive)
	VALUES(NULL,source.SloCode,source.DisplayName,source.MetricCode,source.ComparisonCode,source.TargetValue,source.WarningValue,source.CriticalValue,source.EvaluationWindowMinutes,1);

COMMIT TRANSACTION;
