SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- POLOXI V2.1: Query Contract, Branch States, Three-Score Model, Candidate Engine.
-- LLM branch percentages become Interpretation Priors (retrieval allocation),
-- evidence produces Evidence Support, and POLOXI Confidence is the post-evidence
-- conclusion. Branches move through ACTIVE / SECONDARY / DORMANT / PRUNED
-- instead of hard elimination; PRUNED is reserved for constraint violations.
-- ============================================================================

-- Execution: persist the extracted query contract and evidence coverage metrics.
IF COL_LENGTH(N'POLOXI.WideExecution',N'QueryContractJson') IS NULL
	ALTER TABLE POLOXI.WideExecution ADD QueryContractJson NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'POLOXI.WideExecution',N'EvidenceCoverage') IS NULL
	ALTER TABLE POLOXI.WideExecution ADD EvidenceCoverage DECIMAL(5,4) NULL;
IF COL_LENGTH(N'POLOXI.WideExecution',N'ExternalEvidenceCount') IS NULL
	ALTER TABLE POLOXI.WideExecution ADD ExternalEvidenceCount INT NOT NULL CONSTRAINT DF_PoloxiWideExecution_ExternalEvidence DEFAULT 0;
IF COL_LENGTH(N'POLOXI.WideExecution',N'EnterpriseEvidenceCount') IS NULL
	ALTER TABLE POLOXI.WideExecution ADD EnterpriseEvidenceCount INT NOT NULL CONSTRAINT DF_PoloxiWideExecution_EnterpriseEvidence DEFAULT 0;
IF COL_LENGTH(N'POLOXI.WideExecution',N'CandidateCount') IS NULL
	ALTER TABLE POLOXI.WideExecution ADD CandidateCount INT NOT NULL CONSTRAINT DF_PoloxiWideExecution_Candidates DEFAULT 0;

-- Branch: three-score model + branch lifecycle state.
IF COL_LENGTH(N'POLOXI.WideBranch',N'BranchStateCode') IS NULL
	ALTER TABLE POLOXI.WideBranch ADD BranchStateCode NVARCHAR(20) NOT NULL CONSTRAINT DF_PoloxiWideBranch_State DEFAULT N'ACTIVE';
IF COL_LENGTH(N'POLOXI.WideBranch',N'InterpretationPrior') IS NULL
	ALTER TABLE POLOXI.WideBranch ADD InterpretationPrior DECIMAL(5,4) NULL;
IF COL_LENGTH(N'POLOXI.WideBranch',N'EvidenceSupport') IS NULL
	ALTER TABLE POLOXI.WideBranch ADD EvidenceSupport DECIMAL(5,4) NULL;
IF COL_LENGTH(N'POLOXI.WideBranch',N'PoloxiConfidence') IS NULL
	ALTER TABLE POLOXI.WideBranch ADD PoloxiConfidence DECIMAL(5,4) NULL;

-- Candidate universe surviving the hard-constraint filter (never deleted).
IF OBJECT_ID(N'POLOXI.WideCandidate',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.WideCandidate
	(
		WideCandidateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PoloxiWideCandidate PRIMARY KEY,
		WideExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_PoloxiWideCandidate_Execution REFERENCES POLOXI.WideExecution(WideExecutionId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		DisplayName NVARCHAR(300) NOT NULL,
		Detail NVARCHAR(1000) NULL,
		CompositeScore DECIMAL(5,4) NOT NULL CONSTRAINT DF_PoloxiWideCandidate_Composite DEFAULT 0,
		RankNumber INT NOT NULL,
		IsConstraintViolation BIT NOT NULL CONSTRAINT DF_PoloxiWideCandidate_Violation DEFAULT 0,
		ConstraintViolationReason NVARCHAR(400) NULL,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_PoloxiWideCandidate_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PoloxiWideCandidate_Deleted DEFAULT 0
	);
	CREATE INDEX IX_PoloxiWideCandidate_Execution ON POLOXI.WideCandidate(WideExecutionId,RankNumber) WHERE IsDeleted=0;
END;

-- Candidate x Branch evidence score matrix.
IF OBJECT_ID(N'POLOXI.WideCandidateBranchScore',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.WideCandidateBranchScore
	(
		WideCandidateBranchScoreId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PoloxiWideCandidateBranchScore PRIMARY KEY,
		WideCandidateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_PoloxiWideCandidateBranchScore_Candidate REFERENCES POLOXI.WideCandidate(WideCandidateId),
		WideBranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_PoloxiWideCandidateBranchScore_Branch REFERENCES POLOXI.WideBranch(WideBranchId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		BranchDisplayName NVARCHAR(300) NOT NULL,
		EvidenceScore DECIMAL(5,4) NOT NULL CONSTRAINT DF_PoloxiWideCandidateBranchScore_Score DEFAULT 0,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_PoloxiWideCandidateBranchScore_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PoloxiWideCandidateBranchScore_Deleted DEFAULT 0
	);
	CREATE INDEX IX_PoloxiWideCandidateBranchScore_Candidate ON POLOXI.WideCandidateBranchScore(WideCandidateId) WHERE IsDeleted=0;
END;

-- V2.1 configuration settings (Platform scope, tenant-overridable).
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
		(N'Intelligence.SearchWide.SecondaryBranchThreshold',N'0.35',N'Decimal',N'Interpretation prior below this makes a branch SECONDARY (smaller retrieval budget) instead of eliminated.'),
		(N'Intelligence.SearchWide.DormantBranchThreshold',N'0.20',N'Decimal',N'Interpretation prior below this makes a branch DORMANT (not searched deeper, reactivatable) instead of eliminated.'),
		(N'Intelligence.SearchWide.PriorWeight',N'0.30',N'Decimal',N'Weight of the LLM interpretation prior when computing branch POLOXI confidence.'),
		(N'Intelligence.SearchWide.EvidenceWeight',N'0.70',N'Decimal',N'Weight of evidence support when computing branch POLOXI confidence.'),
		(N'Intelligence.SearchWide.MaximumCandidates',N'10',N'Integer',N'Maximum candidates ranked in the candidate-by-branch competition matrix.'),
		(N'Intelligence.SearchWide.EnableQueryContract',N'true',N'Boolean',N'Extract a query contract (hard constraints, ambiguous concepts, output requirements) before hierarchy generation.');

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

-- Governed CHAT feature policies for the new V2.1 stages.
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
		SELECT N'INTELLIGENCE_WIDE_QUERY_CONTRACT' FeatureCode,CONVERT(decimal(4,3),0.000) Temperature,4000 MaximumInputTokens,800 MaximumOutputTokens,20 TimeoutSeconds,CONVERT(decimal(5,4),0.0000) MinimumConfidence
		UNION ALL
		SELECT N'INTELLIGENCE_WIDE_CANDIDATE_SCORING',CONVERT(decimal(4,3),0.000),14000,2000,45,CONVERT(decimal(5,4),0.0000)
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
