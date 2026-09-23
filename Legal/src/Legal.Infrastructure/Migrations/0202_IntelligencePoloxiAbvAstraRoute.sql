SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Pin the INTELLIGENCE_POLOXI_ABV feature policy PRIMARY route to gpt-6-astra.
-- 0201 restored ABV routing via the generic best-CHAT selection, which resolved to gpt-4.1-mini;
-- per the "Astra all the way" direction this pins ABV explicitly to gpt-6-astra (as 0196 does for
-- INTELLIGENCE_WIDE_ANSWER_ASTRA) with the best non-Astra CHAT deployment kept as fallback.
-- Budgets are raised for reasoning-model headroom: hidden reasoning tokens consume
-- max_completion_tokens, and Astra stage latency regularly exceeds 60 seconds.
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
		SELECT N'INTELLIGENCE_POLOXI_ABV' FeatureCode,CONVERT(decimal(4,3),0.000) Temperature,14000 MaximumInputTokens,16000 MaximumOutputTokens,300 TimeoutSeconds,CONVERT(decimal(5,4),0.0000) MinimumConfidence
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
			target.FallbackModelDeploymentId=source.FallbackModelDeploymentId,
			target.MaximumInputTokens=source.MaximumInputTokens,
			target.MaximumOutputTokens=source.MaximumOutputTokens,
			target.TimeoutSeconds=source.TimeoutSeconds,
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
