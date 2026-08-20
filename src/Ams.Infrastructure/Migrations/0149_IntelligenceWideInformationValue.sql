SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- EPH V2.2: Information-Directed Exploration.
-- Deterministic Shannon entropy + one batched LLM Information Value estimate
-- (with falsifiable candidate ranking predictions) + measured Actual
-- Information Gain + calibration data capture.
-- Terminology: EstimatedInformationValue (LLM prediction) is NEVER called
-- Information Gain; ActualInformationGain is the measured entropy reduction.
-- ============================================================================

-- ── EPH.WideInformationRound: one row per information-directed exploration round ──
IF OBJECT_ID(N'EPH.WideInformationRound',N'U') IS NULL
BEGIN
	CREATE TABLE EPH.WideInformationRound
	(
		WideInformationRoundId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EphWideInformationRound PRIMARY KEY,
		WideExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EphWideInformationRound_Execution REFERENCES EPH.WideExecution(WideExecutionId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		RoundNumber INT NOT NULL,
		EntropyBefore DECIMAL(9,4) NOT NULL,
		NormalizedEntropyBefore DECIMAL(5,4) NOT NULL,
		EntropyAfter DECIMAL(9,4) NULL,
		NormalizedEntropyAfter DECIMAL(5,4) NULL,
		ActualInformationGain DECIMAL(9,4) NULL,
		RawEntropyDelta DECIMAL(9,4) NULL,
		SelectedTargetCount INT NOT NULL CONSTRAINT DF_EphWideInfoRound_Selected DEFAULT 0,
		StartedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EphWideInfoRound_Started DEFAULT SYSUTCDATETIME(),
		CompletedDateUtc DATETIME2(3) NULL,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EphWideInfoRound_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EphWideInfoRound_Deleted DEFAULT 0
	);
	CREATE INDEX IX_EphWideInfoRound_Execution ON EPH.WideInformationRound(WideExecutionId,RoundNumber) WHERE IsDeleted=0;
END;

-- ── EPH.WideInformationTarget: one row per evaluated candidate investigation ──
IF OBJECT_ID(N'EPH.WideInformationTarget',N'U') IS NULL
BEGIN
	CREATE TABLE EPH.WideInformationTarget
	(
		WideInformationTargetId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EphWideInformationTarget PRIMARY KEY,
		WideInformationRoundId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EphWideInfoTarget_Round REFERENCES EPH.WideInformationRound(WideInformationRoundId),
		WideBranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EphWideInfoTarget_Branch REFERENCES EPH.WideBranch(WideBranchId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		UncertaintyCode NVARCHAR(20) NOT NULL,
		RankingImpactCode NVARCHAR(20) NOT NULL,
		CandidateDiscriminationCode NVARCHAR(20) NOT NULL,
		EvidenceAvailabilityCode NVARCHAR(20) NOT NULL,
		NoveltyCode NVARCHAR(20) NOT NULL,
		RedundancyCode NVARCHAR(20) NOT NULL,
		RawEstimatedInformationValue DECIMAL(5,4) NOT NULL,
		AdjustedInformationValue DECIMAL(5,4) NOT NULL,
		CalibrationFactor DECIMAL(5,4) NULL,
		ExpectedRetrievalCost DECIMAL(5,4) NULL,
		InformationValuePerCost DECIMAL(9,4) NULL,
		WasSelected BIT NOT NULL CONSTRAINT DF_EphWideInfoTarget_Selected DEFAULT 0,
		SelectionRank INT NULL,
		EvidenceTarget NVARCHAR(1000) NULL,
		Rationale NVARCHAR(1000) NULL,
		PredictedRankingImpactCount INT NOT NULL CONSTRAINT DF_EphWideInfoTarget_PredCount DEFAULT 0,
		PredictedUpCount INT NOT NULL CONSTRAINT DF_EphWideInfoTarget_PredUp DEFAULT 0,
		PredictedDownCount INT NOT NULL CONSTRAINT DF_EphWideInfoTarget_PredDown DEFAULT 0,
		DirectionAccuracy DECIMAL(5,4) NULL,
		MagnitudeAccuracy DECIMAL(5,4) NULL,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EphWideInfoTarget_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EphWideInfoTarget_Deleted DEFAULT 0
	);
	CREATE INDEX IX_EphWideInfoTarget_Round ON EPH.WideInformationTarget(WideInformationRoundId) WHERE IsDeleted=0;
END;

-- ── EPH.WideInformationPrediction: falsifiable per-candidate ranking predictions ──
IF OBJECT_ID(N'EPH.WideInformationPrediction',N'U') IS NULL
BEGIN
	CREATE TABLE EPH.WideInformationPrediction
	(
		WideInformationPredictionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EphWideInformationPrediction PRIMARY KEY,
		WideInformationTargetId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EphWideInfoPrediction_Target REFERENCES EPH.WideInformationTarget(WideInformationTargetId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CandidateName NVARCHAR(300) NOT NULL,
		PredictedDirection NVARCHAR(10) NOT NULL,
		PredictedMagnitude NVARCHAR(10) NOT NULL,
		ScoreBefore DECIMAL(5,4) NULL,
		RankBefore INT NULL,
		ScoreAfter DECIMAL(5,4) NULL,
		RankAfter INT NULL,
		ActualDirection NVARCHAR(10) NULL,
		ActualMagnitude NVARCHAR(10) NULL,
		DirectionCorrect BIT NULL,
		MagnitudeCorrect BIT NULL,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EphWideInfoPrediction_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EphWideInfoPrediction_Deleted DEFAULT 0
	);
	CREATE INDEX IX_EphWideInfoPrediction_Target ON EPH.WideInformationPrediction(WideInformationTargetId) WHERE IsDeleted=0;
END;

-- ── Execution-level entropy/IG columns ──
IF OBJECT_ID(N'EPH.WideExecution',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'EPH.WideExecution',N'InitialEntropy') IS NULL
		ALTER TABLE EPH.WideExecution ADD InitialEntropy DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'EPH.WideExecution',N'FinalEntropy') IS NULL
		ALTER TABLE EPH.WideExecution ADD FinalEntropy DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'EPH.WideExecution',N'InitialNormalizedEntropy') IS NULL
		ALTER TABLE EPH.WideExecution ADD InitialNormalizedEntropy DECIMAL(5,4) NULL;
	IF COL_LENGTH(N'EPH.WideExecution',N'FinalNormalizedEntropy') IS NULL
		ALTER TABLE EPH.WideExecution ADD FinalNormalizedEntropy DECIMAL(5,4) NULL;
	IF COL_LENGTH(N'EPH.WideExecution',N'TotalActualInformationGain') IS NULL
		ALTER TABLE EPH.WideExecution ADD TotalActualInformationGain DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'EPH.WideExecution',N'InformationRoundCount') IS NULL
		ALTER TABLE EPH.WideExecution ADD InformationRoundCount INT NOT NULL CONSTRAINT DF_EphWideExecution_InfoRounds DEFAULT 0;
	IF COL_LENGTH(N'EPH.WideExecution',N'InformationTargetCount') IS NULL
		ALTER TABLE EPH.WideExecution ADD InformationTargetCount INT NOT NULL CONSTRAINT DF_EphWideExecution_InfoTargets DEFAULT 0;
	IF COL_LENGTH(N'EPH.WideExecution',N'InformationRetrievalCount') IS NULL
		ALTER TABLE EPH.WideExecution ADD InformationRetrievalCount INT NOT NULL CONSTRAINT DF_EphWideExecution_InfoRetrievals DEFAULT 0;
END;

-- ── V2.2 configuration settings (DB is the source of truth) ──
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
		(N'Intelligence.SearchWide.EnableInformationValue',N'true',N'Boolean',N'Enables V2.2 information-directed exploration (entropy + LLM information value + measured actual information gain). Fail-soft: failures degrade to V2.1 narrowing.'),
		(N'Intelligence.SearchWide.InformationValueTriggerEntropy',N'0.45',N'Decimal',N'Normalized entropy threshold above which an information-value round may trigger.'),
		(N'Intelligence.SearchWide.MaximumInformationRounds',N'3',N'Integer',N'Maximum information-directed exploration rounds per execution.'),
		(N'Intelligence.SearchWide.MaximumInformationTargetsPerRound',N'2',N'Integer',N'Maximum evidence targets retrieved (concurrently) per information round.'),
		(N'Intelligence.SearchWide.MinimumInformationValue',N'0.55',N'Decimal',N'Minimum adjusted information value a target needs to be selected for retrieval.'),
		(N'Intelligence.SearchWide.MinimumActualInformationGain',N'0.05',N'Decimal',N'Actual information gain (bits) below which a round counts as no-progress.'),
		(N'Intelligence.SearchWide.InformationNoProgressRounds',N'2',N'Integer',N'Consecutive weak rounds after which exploration stops (INFORMATION_GAIN_STALLED).'),
		(N'Intelligence.SearchWide.InformationValueLlmWeight',N'0.60',N'Decimal',N'Weight of the LLM categorical estimate inside the final information value.'),
		(N'Intelligence.SearchWide.InformationValueEvidenceGapWeight',N'0.15',N'Decimal',N'Weight of (1 - evidence coverage) inside the final information value.'),
		(N'Intelligence.SearchWide.InformationValueBranchWeight',N'0.15',N'Decimal',N'Weight of normalized branch importance (EPH confidence) inside the final information value.'),
		(N'Intelligence.SearchWide.InformationValueCandidateNeedWeight',N'0.10',N'Decimal',N'Weight of candidate discrimination need (ranking closeness) inside the final information value.'),
		(N'Intelligence.SearchWide.VeryLowInformationValue',N'0.20',N'Decimal',N'Deterministic numeric value for the VERY_LOW LLM category.'),
		(N'Intelligence.SearchWide.LowInformationValue',N'0.40',N'Decimal',N'Deterministic numeric value for the LOW LLM category.'),
		(N'Intelligence.SearchWide.MediumInformationValue',N'0.60',N'Decimal',N'Deterministic numeric value for the MEDIUM LLM category.'),
		(N'Intelligence.SearchWide.HighInformationValue',N'0.80',N'Decimal',N'Deterministic numeric value for the HIGH LLM category.'),
		(N'Intelligence.SearchWide.VeryHighInformationValue',N'1.00',N'Decimal',N'Deterministic numeric value for the VERY_HIGH LLM category.');

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

-- ── Governed CHAT feature policy for the batched Information Value estimator ──
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
		SELECT N'INTELLIGENCE_WIDE_INFORMATION_VALUE' FeatureCode,CONVERT(decimal(4,3),0.000) Temperature,14000 MaximumInputTokens,4000 MaximumOutputTokens,45 TimeoutSeconds,CONVERT(decimal(5,4),0.0000) MinimumConfidence
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
