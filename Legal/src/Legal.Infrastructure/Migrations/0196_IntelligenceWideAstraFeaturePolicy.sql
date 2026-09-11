SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Dedicated governed CHAT feature policy for the gpt-6-astra model.
-- This adds a separate configuration surface (routing/limits/timeout) that pins its PRIMARY
-- model deployment explicitly to gpt-6-astra (seeded in 0195_IntelligenceWideModelGpt6Astra.sql),
-- rather than the generic best-CHAT selection used by the shared Wide feature policies.
-- The row appears automatically under /legal/configuration ("Feature policies" section),
-- since that page is fully database-backed via AI.Legal_FeaturePolicy.
-- Mirrors the MERGE pattern in 0149_IntelligenceWideInformationValue.sql.
IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_ModelDeployment',N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_Provider',N'U') IS NOT NULL
   AND OBJECT_ID(N'Core.Tenant',N'U') IS NOT NULL
BEGIN
	;WITH AstraRoute AS
	(
		SELECT tenant.TenantId,
			(SELECT TOP(1) model.ModelDeploymentId
			 FROM AI.Legal_ModelDeployment model
			 JOIN AI.Legal_Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
			 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT'
			   AND model.ModelCode=N'gpt-6-astra'
			   AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL)
			 ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,model.IsFallback,model.Priority,model.CreatedDateUtc)
			 PrimaryModelDeploymentId,
			(SELECT TOP(1) model.ModelDeploymentId
			 FROM AI.Legal_ModelDeployment model
			 JOIN AI.Legal_Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
			 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT'
			   AND model.ModelCode<>N'gpt-6-astra'
			   AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL)
			 ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,CASE WHEN model.IsFallback=1 THEN 0 ELSE 1 END,model.Priority,model.CreatedDateUtc)
			 FallbackModelDeploymentId
		FROM Core.Tenant tenant
		WHERE tenant.IsDeleted=0
	),
	Features AS
	(
		SELECT N'INTELLIGENCE_WIDE_ANSWER_ASTRA' FeatureCode,CONVERT(decimal(4,3),0.000) Temperature,60000 MaximumInputTokens,65536 MaximumOutputTokens,600 TimeoutSeconds,CONVERT(decimal(5,4),0.0000) MinimumConfidence
	),
	SourcePolicy AS
	(
		SELECT route.TenantId,feature.FeatureCode,N'Intelligence' ModuleCode,route.PrimaryModelDeploymentId,
			CASE WHEN route.FallbackModelDeploymentId=route.PrimaryModelDeploymentId THEN NULL ELSE route.FallbackModelDeploymentId END FallbackModelDeploymentId,
			feature.Temperature,feature.MaximumInputTokens,feature.MaximumOutputTokens,feature.TimeoutSeconds,feature.MinimumConfidence
		FROM AstraRoute route
		CROSS JOIN Features feature
		WHERE route.PrimaryModelDeploymentId IS NOT NULL
	)
	MERGE AI.Legal_FeaturePolicy AS target
	USING SourcePolicy AS source
	   ON target.TenantId=source.TenantId
	  AND target.FeatureCode=source.FeatureCode
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=source.ModuleCode,
			target.PrimaryModelDeploymentId=source.PrimaryModelDeploymentId,
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
