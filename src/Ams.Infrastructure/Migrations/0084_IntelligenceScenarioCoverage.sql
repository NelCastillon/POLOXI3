SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Submissions.BindRequirement',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Submissions.BindRequirement',N'CanBeWaived') IS NULL ALTER TABLE Submissions.BindRequirement ADD CanBeWaived BIT NOT NULL CONSTRAINT DF_BindRequirement_CanBeWaived_0276 DEFAULT 0;
	IF COL_LENGTH(N'Submissions.BindRequirement',N'WaiverPermissionCode') IS NULL ALTER TABLE Submissions.BindRequirement ADD WaiverPermissionCode NVARCHAR(150) NULL;
	IF COL_LENGTH(N'Submissions.BindRequirement',N'ApprovalPermissionCode') IS NULL ALTER TABLE Submissions.BindRequirement ADD ApprovalPermissionCode NVARCHAR(150) NULL;
END;

IF OBJECT_ID(N'AI.ComplianceRequirement',N'U') IS NULL
BEGIN
	CREATE TABLE AI.ComplianceRequirement
	(
		ComplianceRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_ComplianceRequirement PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		RequirementCode NVARCHAR(120) NOT NULL,
		DisplayName NVARCHAR(240) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		RequirementScopeCode NVARCHAR(50) NOT NULL,
		JurisdictionCode NVARCHAR(20) NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		LineOfBusinessCode NVARCHAR(100) NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		RequirementTypeCode NVARCHAR(50) NOT NULL,
		EvaluationJson NVARCHAR(MAX) NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		BlocksTransaction BIT NOT NULL,
		CanBeWaived BIT NOT NULL,
		WaiverPermissionCode NVARCHAR(150) NULL,
		ApprovalPermissionCode NVARCHAR(150) NULL,
		EffectiveFromUtc DATETIME2 NOT NULL,
		EffectiveToUtc DATETIME2 NULL,
		VersionNumber INT NOT NULL,
		IsActive BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_ComplianceRequirement_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_ComplianceRequirement_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_AI_ComplianceRequirement_Json CHECK(ISJSON(EvaluationJson)=1),
		CONSTRAINT CK_AI_ComplianceRequirement_Version CHECK(VersionNumber>0),
		CONSTRAINT CK_AI_ComplianceRequirement_Dates CHECK(EffectiveToUtc IS NULL OR EffectiveToUtc>EffectiveFromUtc)
	);
	CREATE UNIQUE INDEX UX_AI_ComplianceRequirement_Version ON AI.ComplianceRequirement(TenantId,RequirementCode,VersionNumber) WHERE IsDeleted=0;
	CREATE INDEX IX_AI_ComplianceRequirement_Scope ON AI.ComplianceRequirement(TenantId,EntityTypeCode,RequirementScopeCode,JurisdictionCode,CarrierId,LineOfBusinessCode,IsActive) INCLUDE(RequirementCode,SeverityCode,BlocksTransaction,CanBeWaived);
END;

IF OBJECT_ID(N'AI.EvaluationSampleLabel',N'U') IS NULL
BEGIN
	CREATE TABLE AI.EvaluationSampleLabel
	(
		EvaluationSampleLabelId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_EvaluationSampleLabel PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ExecutionId UNIQUEIDENTIFIER NOT NULL,
		EvaluationDefinitionId UNIQUEIDENTIFIER NULL,
		PredictedPositive BIT NOT NULL,
		ActualPositive BIT NOT NULL,
		IsHallucination BIT NOT NULL,
		IsAccurate BIT NOT NULL,
		LabelSourceCode NVARCHAR(50) NOT NULL,
		Notes NVARCHAR(2000) NULL,
		LabeledByUserId UNIQUEIDENTIFIER NOT NULL,
		LabeledDateUtc DATETIME2 NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_EvaluationSampleLabel_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_EvaluationSampleLabel_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_EvaluationSampleLabel_Execution FOREIGN KEY(ExecutionId) REFERENCES AI.Execution(ExecutionId),
		CONSTRAINT FK_AI_EvaluationSampleLabel_Definition FOREIGN KEY(EvaluationDefinitionId) REFERENCES AI.EvaluationDefinition(EvaluationDefinitionId)
	);
	CREATE INDEX IX_AI_EvaluationSampleLabel_Window ON AI.EvaluationSampleLabel(TenantId,LabeledDateUtc,IsDeleted) INCLUDE(PredictedPositive,ActualPositive,IsHallucination,IsAccurate,ExecutionId);
END;

IF OBJECT_ID(N'AI.SafetyEvent',N'U') IS NULL
BEGIN
	CREATE TABLE AI.SafetyEvent
	(
		SafetyEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_SafetyEvent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SafetyControlId UNIQUEIDENTIFIER NOT NULL,
		ExecutionId UNIQUEIDENTIFIER NULL,
		ReasoningSessionId UNIQUEIDENTIFIER NULL,
		UserId UNIQUEIDENTIFIER NULL,
		EventTypeCode NVARCHAR(100) NOT NULL,
		EnforcementStageCode NVARCHAR(50) NOT NULL,
		ActionCode NVARCHAR(50) NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		InputHash CHAR(64) NULL,
		DetailsJson NVARCHAR(MAX) NOT NULL,
		RequiresHumanReview BIT NOT NULL,
		ReviewStatusCode NVARCHAR(30) NULL,
		DetectedDateUtc DATETIME2 NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_SafetyEvent_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_SafetyEvent_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_SafetyEvent_Control FOREIGN KEY(SafetyControlId) REFERENCES AI.SafetyControl(SafetyControlId),
		CONSTRAINT FK_AI_SafetyEvent_Execution FOREIGN KEY(ExecutionId) REFERENCES AI.Execution(ExecutionId),
		CONSTRAINT FK_AI_SafetyEvent_Reasoning FOREIGN KEY(ReasoningSessionId) REFERENCES AI.ReasoningSession(ReasoningSessionId),
		CONSTRAINT CK_AI_SafetyEvent_Json CHECK(ISJSON(DetailsJson)=1)
	);
	CREATE INDEX IX_AI_SafetyEvent_TenantDate ON AI.SafetyEvent(TenantId,DetectedDateUtc DESC) INCLUDE(SafetyControlId,EventTypeCode,ActionCode,SeverityCode,RequiresHumanReview,ReviewStatusCode);
END;

MERGE AI.EvaluationDefinition target USING(VALUES
(CONVERT(uniqueidentifier,'93000000-0000-0000-0000-000000000001'),N'LABELED_ACCURACY',N'Labeled output accuracy',N'ALL',N'ACCURACY',N'LABELED_ACCURACY',CONVERT(decimal(18,6),0.95),CONVERT(decimal(18,6),0.90),168,20,N'{"direction":"HIGHER_IS_BETTER","labelSource":"HUMAN_REVIEW"}'),
(CONVERT(uniqueidentifier,'93000000-0000-0000-0000-000000000002'),N'LABELED_PRECISION',N'Labeled output precision',N'ALL',N'PRECISION',N'LABELED_PRECISION',CONVERT(decimal(18,6),0.95),CONVERT(decimal(18,6),0.90),168,20,N'{"direction":"HIGHER_IS_BETTER","labelSource":"HUMAN_REVIEW"}'),
(CONVERT(uniqueidentifier,'93000000-0000-0000-0000-000000000003'),N'LABELED_RECALL',N'Labeled output recall',N'ALL',N'RECALL',N'LABELED_RECALL',CONVERT(decimal(18,6),0.95),CONVERT(decimal(18,6),0.90),168,20,N'{"direction":"HIGHER_IS_BETTER","labelSource":"HUMAN_REVIEW"}'),
(CONVERT(uniqueidentifier,'93000000-0000-0000-0000-000000000004'),N'HALLUCINATION_RATE',N'Human-labeled hallucination rate',N'ALL',N'HALLUCINATION_RATE',N'HALLUCINATION_RATE',CONVERT(decimal(18,6),0.02),CONVERT(decimal(18,6),0.05),168,20,N'{"direction":"LOWER_IS_BETTER","labelSource":"HUMAN_REVIEW"}'),
(CONVERT(uniqueidentifier,'93000000-0000-0000-0000-000000000005'),N'SAFETY_EVENT_RATE',N'Safety event rate',N'ALL',N'SAFETY_EVENT_RATE',N'SAFETY_EVENT_RATE',CONVERT(decimal(18,6),0.01),CONVERT(decimal(18,6),0.03),168,20,N'{"direction":"LOWER_IS_BETTER"}')) source(Id,Code,Name,FeatureCode,MetricCode,CalculationCode,TargetValue,WarningValue,WindowHours,MinimumSampleSize,ConfigurationJson)
ON target.TenantId IS NULL AND target.EvaluationCode=source.Code
WHEN MATCHED THEN UPDATE SET DisplayName=source.Name,FeatureCode=source.FeatureCode,MetricCode=source.MetricCode,CalculationCode=source.CalculationCode,TargetValue=source.TargetValue,WarningValue=source.WarningValue,WindowHours=source.WindowHours,MinimumSampleSize=source.MinimumSampleSize,ConfigurationJson=source.ConfigurationJson,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(EvaluationDefinitionId,TenantId,EvaluationCode,DisplayName,FeatureCode,MetricCode,CalculationCode,TargetValue,WarningValue,WindowHours,MinimumSampleSize,ConfigurationJson,IsActive,CreatedDateUtc,IsDeleted) VALUES(source.Id,NULL,source.Code,source.Name,source.FeatureCode,source.MetricCode,source.CalculationCode,source.TargetValue,source.WarningValue,source.WindowHours,source.MinimumSampleSize,source.ConfigurationJson,1,SYSUTCDATETIME(),0);

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),Value NVARCHAR(2000),DataType NVARCHAR(50),Name NVARCHAR(200),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Intelligence.Risk.LargeLossThreshold',N'100000',N'Decimal',N'Large loss threshold',N'Configured incurred-loss threshold used by advisory claims and account risk findings.'),
	(N'Intelligence.Risk.HighRiskAccountLossCount',N'3',N'Integer',N'High-risk account loss count',N'Configured open or recent loss count used for advisory account risk findings.'),
	(N'Intelligence.Renewal.ReadinessDays',N'90',N'Integer',N'Renewal readiness window',N'Days before expiration when renewal readiness evidence should be evaluated.'),
	(N'Intelligence.Workflow.DelayDays',N'7',N'Integer',N'Workflow delay threshold',N'Days without authoritative workflow activity before a delay signal is generated.'),
	(N'Intelligence.Producer.FollowUpDays',N'5',N'Integer',N'Producer follow-up threshold',N'Days without account or opportunity activity before a producer follow-up signal is generated.');
	MERGE Core.ConfigurationSetting target USING @Config source ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.Value,DataTypeCode=source.DataType,Description=source.Name+N'. '+source.Description,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc) VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.Value,source.Value,source.DataType,source.Name+N'. '+source.Description,0,0,0,SYSUTCDATETIME());
END;
