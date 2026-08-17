SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'EPH') EXEC(N'CREATE SCHEMA EPH');

-- Wide dynamic disambiguation pipeline execution log.
IF OBJECT_ID(N'EPH.WideExecution',N'U') IS NULL
BEGIN
	CREATE TABLE EPH.WideExecution
	(
		WideExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EphWideExecution PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		UserId UNIQUEIDENTIFIER NOT NULL,
		QueryText NVARCHAR(1000) NOT NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_EphWideExecution_Status DEFAULT N'RUNNING',
		TerminationReasonCode NVARCHAR(50) NULL,
		DepthReached INT NOT NULL CONSTRAINT DF_EphWideExecution_Depth DEFAULT 0,
		LlmCallCount INT NOT NULL CONSTRAINT DF_EphWideExecution_Calls DEFAULT 0,
		FinalConfidence DECIMAL(5,4) NULL,
		AnswerVerificationCode NVARCHAR(50) NULL,
		FinalAnswer NVARCHAR(MAX) NULL,
		DurationMilliseconds BIGINT NULL,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EphWideExecution_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EphWideExecution_Deleted DEFAULT 0
	);
	CREATE INDEX IX_EphWideExecution_Tenant ON EPH.WideExecution(TenantId,CreatedDateUtc DESC) WHERE IsDeleted=0;
END;

-- Wide branch audit: every proposed branch per level, including eliminated ones (never deleted).
IF OBJECT_ID(N'EPH.WideBranch',N'U') IS NULL
BEGIN
	CREATE TABLE EPH.WideBranch
	(
		WideBranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EphWideBranch PRIMARY KEY,
		WideExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EphWideBranch_Execution REFERENCES EPH.WideExecution(WideExecutionId),
		ParentWideBranchId UNIQUEIDENTIFIER NULL CONSTRAINT FK_EphWideBranch_Parent REFERENCES EPH.WideBranch(WideBranchId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		LevelNumber INT NOT NULL,
		BranchCode NVARCHAR(120) NOT NULL,
		DisplayName NVARCHAR(300) NOT NULL,
		Interpretation NVARCHAR(1000) NOT NULL,
		CapabilityCode NVARCHAR(100) NULL,
		SearchText NVARCHAR(400) NULL,
		GroundingStatusCode NVARCHAR(50) NOT NULL,
		EvidenceCount INT NOT NULL CONSTRAINT DF_EphWideBranch_Evidence DEFAULT 0,
		Confidence DECIMAL(5,4) NOT NULL,
		ContinueNarrowing BIT NOT NULL CONSTRAINT DF_EphWideBranch_Continue DEFAULT 0,
		StopReason NVARCHAR(50) NULL,
		IsEliminated BIT NOT NULL CONSTRAINT DF_EphWideBranch_Eliminated DEFAULT 0,
		EliminationReason NVARCHAR(400) NULL,
		SortOrder INT NOT NULL,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EphWideBranch_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EphWideBranch_Deleted DEFAULT 0
	);
	CREATE INDEX IX_EphWideBranch_Execution ON EPH.WideBranch(WideExecutionId,LevelNumber,SortOrder) WHERE IsDeleted=0;
END;

-- Wide pipeline configuration settings (Platform scope, tenant-overridable).
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE
	(
		SettingKey NVARCHAR(200) NOT NULL,
		SettingValue NVARCHAR(2000) NOT NULL,
		DataTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(1000) NOT NULL
	);

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.TargetConfidence',N'0.85',N'Decimal',N'Aggregate confidence at which the Wide disambiguation loop stops early.'),
		(N'Intelligence.SearchWide.MinimumBranchConfidence',N'0.35',N'Decimal',N'Branches below this confidence are eliminated during Wide disambiguation.'),
		(N'Intelligence.SearchWide.MaximumBranchesPerLevel',N'5',N'Integer',N'Maximum branches the LLM may propose per hierarchy level in Wide search.'),
		(N'Intelligence.SearchWide.AbsoluteDepthCeiling',N'25',N'Integer',N'Runaway circuit breaker only; the LLM decides natural termination. Reaching this records DEPTH_CEILING_REACHED.'),
		(N'Intelligence.SearchWide.MaximumTotalLlmCalls',N'30',N'Integer',N'Cost circuit breaker for total LLM calls per Wide search execution.');

	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL
	  AND target.ScopeCode=N'Platform'
	  AND target.SettingKey=source.SettingKey
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=N'Intelligence',
			target.SettingValue=COALESCE(NULLIF(target.SettingValue,N''),source.SettingValue),
			target.DefaultValue=source.SettingValue,
			target.DataTypeCode=source.DataTypeCode,
			target.Description=source.Description,
			target.IsEncrypted=0,
			target.IsReadOnly=0,
			target.ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,
			DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,
			source.SettingValue,source.DataTypeCode,source.Description,0,0,
			SYSUTCDATETIME(),0
		);
END;

-- Governed CHAT feature policies for the Wide pipeline stages.
IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.ModelDeployment',N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Provider',N'U') IS NOT NULL
   AND OBJECT_ID(N'Core.Tenant',N'U') IS NOT NULL
BEGIN
	;WITH TenantChatRoute AS
	(
		SELECT tenant.TenantId,
			(SELECT TOP(1) model.ModelDeploymentId
			 FROM AI.ModelDeployment model
			 JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
			 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT'
			   AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL)
			 ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,model.IsFallback,model.Priority,model.CreatedDateUtc)
			 PrimaryModelDeploymentId,
			(SELECT TOP(1) model.ModelDeploymentId
			 FROM AI.ModelDeployment model
			 JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
			 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT'
			   AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL)
			 ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,CASE WHEN model.IsFallback=1 THEN 0 ELSE 1 END,model.Priority,model.CreatedDateUtc)
			 FallbackModelDeploymentId
		FROM Core.Tenant tenant
		WHERE tenant.IsDeleted=0
	),
	Features AS
	(
		SELECT N'INTELLIGENCE_WIDE_INTENT' FeatureCode,CONVERT(decimal(4,3),0.000) Temperature,6000 MaximumInputTokens,1200 MaximumOutputTokens,20 TimeoutSeconds,CONVERT(decimal(5,4),0.0000) MinimumConfidence
		UNION ALL
		SELECT N'INTELLIGENCE_WIDE_HIERARCHY_STEP',CONVERT(decimal(4,3),0.000),10000,1500,25,CONVERT(decimal(5,4),0.0000)
		UNION ALL
		SELECT N'INTELLIGENCE_WIDE_ANSWER',CONVERT(decimal(4,3),0.200),14000,2000,60,CONVERT(decimal(5,4),0.0000)
	),
	SourcePolicy AS
	(
		SELECT route.TenantId,feature.FeatureCode,N'Intelligence' ModuleCode,route.PrimaryModelDeploymentId,
			CASE WHEN route.FallbackModelDeploymentId=route.PrimaryModelDeploymentId THEN NULL ELSE route.FallbackModelDeploymentId END FallbackModelDeploymentId,
			feature.Temperature,feature.MaximumInputTokens,feature.MaximumOutputTokens,feature.TimeoutSeconds,feature.MinimumConfidence
		FROM TenantChatRoute route
		CROSS JOIN Features feature
		WHERE route.PrimaryModelDeploymentId IS NOT NULL
	)
	MERGE AI.FeaturePolicy AS target
	USING SourcePolicy AS source
	   ON target.TenantId=source.TenantId
	  AND target.FeatureCode=source.FeatureCode
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=source.ModuleCode,
			target.PrimaryModelDeploymentId=COALESCE(target.PrimaryModelDeploymentId,source.PrimaryModelDeploymentId),
			target.FallbackModelDeploymentId=COALESCE(target.FallbackModelDeploymentId,source.FallbackModelDeploymentId),
			target.Temperature=source.Temperature,
			target.MaximumInputTokens=source.MaximumInputTokens,
			target.MaximumOutputTokens=source.MaximumOutputTokens,
			target.TimeoutSeconds=source.TimeoutSeconds,
			target.MinimumConfidence=source.MinimumConfidence,
			target.IsEnabled=1,
			target.ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			FeaturePolicyId,TenantId,FeatureCode,ModuleCode,PrimaryModelDeploymentId,FallbackModelDeploymentId,
			Temperature,MaximumInputTokens,MaximumOutputTokens,TimeoutSeconds,DailyCostLimit,MonthlyCostLimit,
			MinimumConfidence,RequiresHumanReview,IsEnabled,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),source.TenantId,source.FeatureCode,source.ModuleCode,source.PrimaryModelDeploymentId,source.FallbackModelDeploymentId,
			source.Temperature,source.MaximumInputTokens,source.MaximumOutputTokens,source.TimeoutSeconds,NULL,NULL,
			source.MinimumConfidence,0,1,SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
