SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name=N'AI') EXEC(N'CREATE SCHEMA AI');

IF OBJECT_ID(N'AI.SearchIntentInterpretationLog',N'U') IS NULL
BEGIN
	CREATE TABLE AI.SearchIntentInterpretationLog
	(
		SearchIntentInterpretationLogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_SearchIntentInterpretationLog PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		UserId UNIQUEIDENTIFIER NOT NULL,
		QueryText NVARCHAR(1000) NOT NULL,
		EntityTypeCode NVARCHAR(100) NULL,
		ModuleCode NVARCHAR(100) NULL,
		SearchText NVARCHAR(1000) NULL,
		SourceEngineCode NVARCHAR(50) NOT NULL,
		Confidence DECIMAL(9,6) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_SearchIntentInterpretationLog_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_SearchIntentInterpretationLog_Deleted DEFAULT 0
	);
END;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'AI.SearchIntentInterpretationLog') AND name=N'IX_AI_SearchIntentInterpretationLog_TenantDate')
	CREATE INDEX IX_AI_SearchIntentInterpretationLog_TenantDate ON AI.SearchIntentInterpretationLog(TenantId,CreatedDateUtc DESC) INCLUDE(SourceEngineCode,StatusCode,EntityTypeCode,ModuleCode,Confidence) WHERE IsDeleted=0;

IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL AND OBJECT_ID(N'AI.ModelDeployment',N'U') IS NOT NULL AND OBJECT_ID(N'Core.Tenant',N'U') IS NOT NULL
BEGIN
	;WITH TenantChatRoute AS
	(
		SELECT tenant.TenantId,
			(SELECT TOP(1) model.ModelDeploymentId FROM AI.ModelDeployment model JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT' AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL) ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,model.IsFallback,model.Priority,model.CreatedDateUtc) PrimaryModelDeploymentId,
			(SELECT TOP(1) model.ModelDeploymentId FROM AI.ModelDeployment model JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT' AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL) ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,CASE WHEN model.IsFallback=1 THEN 0 ELSE 1 END,model.Priority,model.CreatedDateUtc) FallbackModelDeploymentId
		FROM Core.Tenant tenant
		WHERE tenant.IsDeleted=0
	), SourcePolicy AS
	(
		SELECT TenantId,N'INTELLIGENCE_SEARCH_INTENT' FeatureCode,N'Intelligence' ModuleCode,PrimaryModelDeploymentId,CASE WHEN FallbackModelDeploymentId=PrimaryModelDeploymentId THEN NULL ELSE FallbackModelDeploymentId END FallbackModelDeploymentId,CONVERT(decimal(4,3),0.100) Temperature,4000 MaximumInputTokens,500 MaximumOutputTokens,8 TimeoutSeconds,CONVERT(decimal(5,4),0.7000) MinimumConfidence
		FROM TenantChatRoute WHERE PrimaryModelDeploymentId IS NOT NULL
	)
	MERGE AI.FeaturePolicy target USING SourcePolicy source
	ON target.TenantId=source.TenantId AND target.FeatureCode=source.FeatureCode AND target.IsDeleted=0
	WHEN MATCHED AND target.PrimaryModelDeploymentId IS NULL THEN UPDATE SET ModuleCode=source.ModuleCode,PrimaryModelDeploymentId=source.PrimaryModelDeploymentId,FallbackModelDeploymentId=COALESCE(target.FallbackModelDeploymentId,source.FallbackModelDeploymentId),Temperature=source.Temperature,MaximumInputTokens=source.MaximumInputTokens,MaximumOutputTokens=source.MaximumOutputTokens,TimeoutSeconds=source.TimeoutSeconds,MinimumConfidence=source.MinimumConfidence,IsEnabled=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(FeaturePolicyId,TenantId,FeatureCode,ModuleCode,PrimaryModelDeploymentId,FallbackModelDeploymentId,Temperature,MaximumInputTokens,MaximumOutputTokens,TimeoutSeconds,DailyCostLimit,MonthlyCostLimit,MinimumConfidence,RequiresHumanReview,IsEnabled,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),source.TenantId,source.FeatureCode,source.ModuleCode,source.PrimaryModelDeploymentId,source.FallbackModelDeploymentId,source.Temperature,source.MaximumInputTokens,source.MaximumOutputTokens,source.TimeoutSeconds,NULL,NULL,source.MinimumConfidence,0,1,SYSUTCDATETIME(),0);
END;
