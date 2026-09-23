SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Corrective seed: the Legal module omitted the early Azure OpenAI route seed (AMS 0137), so the
-- INTELLIGENCE_WIDE_* / INTELLIGENCE_POLOXI_* feature-policy MERGEs in 0139/0142/0146/0149 ran
-- before any CHAT model deployment existed and produced zero rows. The Legal CHAT deployment is
-- created later (0161), so this migration idempotently (re)seeds AI.Legal_FeaturePolicy for every
-- tenant against the resolved CHAT route with the current AMS token/timeout budgets. Without it the
-- router throws "No active CHAT model route is configured for this tenant and feature."
IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_ModelDeployment',N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_Provider',N'U') IS NOT NULL
   AND OBJECT_ID(N'Core.Tenant',N'U') IS NOT NULL
BEGIN
	;WITH TenantChatRoute AS
	(
		SELECT tenant.TenantId,
			(SELECT TOP(1) model.ModelDeploymentId
			 FROM AI.Legal_ModelDeployment model
			 JOIN AI.Legal_Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
			 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT'
			   AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL)
			 ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,model.IsFallback,model.Priority,model.CreatedDateUtc)
			 PrimaryModelDeploymentId,
			(SELECT TOP(1) model.ModelDeploymentId
			 FROM AI.Legal_ModelDeployment model
			 JOIN AI.Legal_Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
			 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT'
			   AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL)
			 ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,CASE WHEN model.IsFallback=1 THEN 0 ELSE 1 END,model.Priority,model.CreatedDateUtc)
			 FallbackModelDeploymentId
		FROM Core.Tenant tenant
		WHERE tenant.IsDeleted=0
	),
	Features AS
	(
		SELECT N'INTELLIGENCE_POLOXI_HIERARCHY' FeatureCode,CONVERT(decimal(4,3),0.000) Temperature,8000 MaximumInputTokens,3000 MaximumOutputTokens,45 TimeoutSeconds,CONVERT(decimal(5,4),0.6500) MinimumConfidence
		UNION ALL SELECT N'INTELLIGENCE_POLOXI_EXPLANATION',CONVERT(decimal(4,3),0.200),16000,2000,60,CONVERT(decimal(5,4),0.0000)
		UNION ALL SELECT N'INTELLIGENCE_WIDE_INTENT',CONVERT(decimal(4,3),0.000),14000,16000,90,CONVERT(decimal(5,4),0.0000)
		UNION ALL SELECT N'INTELLIGENCE_WIDE_HIERARCHY_STEP',CONVERT(decimal(4,3),0.000),14000,16000,90,CONVERT(decimal(5,4),0.0000)
		UNION ALL SELECT N'INTELLIGENCE_WIDE_ANSWER',CONVERT(decimal(4,3),0.200),20000,16000,90,CONVERT(decimal(5,4),0.0000)
		UNION ALL SELECT N'INTELLIGENCE_WIDE_QUERY_CONTRACT',CONVERT(decimal(4,3),0.000),4000,800,20,CONVERT(decimal(5,4),0.0000)
		UNION ALL SELECT N'INTELLIGENCE_WIDE_CANDIDATE_SCORING',CONVERT(decimal(4,3),0.000),14000,2000,45,CONVERT(decimal(5,4),0.0000)
		UNION ALL SELECT N'INTELLIGENCE_WIDE_CANDIDATE_DISCOVERY',CONVERT(decimal(4,3),0.000),10000,3000,60,CONVERT(decimal(5,4),0.0000)
		UNION ALL SELECT N'INTELLIGENCE_WIDE_INFORMATION_VALUE',CONVERT(decimal(4,3),0.000),14000,16000,120,CONVERT(decimal(5,4),0.0000)
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
	MERGE AI.Legal_FeaturePolicy AS target
	USING SourcePolicy AS source
	   ON target.TenantId=source.TenantId
	  AND target.FeatureCode=source.FeatureCode
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=source.ModuleCode,
			target.PrimaryModelDeploymentId=COALESCE(target.PrimaryModelDeploymentId,source.PrimaryModelDeploymentId),
			target.FallbackModelDeploymentId=COALESCE(target.FallbackModelDeploymentId,source.FallbackModelDeploymentId),
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
