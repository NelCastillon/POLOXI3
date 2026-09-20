SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- AI feature-policy migrations before SaaS tenancy enumerated Core.Tenant. New
-- authenticated workspaces are created in SaaS.SaaS_Tenant, so they otherwise
-- have no governed model routes and Wide Search fails at its first CHAT call.
IF OBJECT_ID(N'AI.Legal_FeaturePolicy', N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_ModelDeployment', N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_Provider', N'U') IS NOT NULL
   AND OBJECT_ID(N'SaaS.SaaS_Tenant', N'U') IS NOT NULL
BEGIN
	;WITH ActiveChatModels AS
	(
		SELECT model.ModelDeploymentId, model.TenantId, model.ModelCode,
			model.IsFallback, model.Priority, model.CreatedDateUtc
		FROM AI.Legal_ModelDeployment model
		JOIN AI.Legal_Provider provider
		  ON provider.ProviderId = model.ProviderId
		 AND provider.IsActive = 1
		 AND provider.IsDeleted = 0
		WHERE model.CapabilityCode = N'CHAT'
		  AND model.IsActive = 1
		  AND model.IsDeleted = 0
	),
	TemplatePolicies AS
	(
		SELECT policy.*,
			ROW_NUMBER() OVER
			(
				PARTITION BY policy.FeatureCode
				ORDER BY policy.ModifiedDateUtc DESC, policy.CreatedDateUtc DESC
			) AS Choice
		FROM AI.Legal_FeaturePolicy policy
		WHERE policy.IsEnabled = 1
		  AND policy.IsDeleted = 0
	),
	SourcePolicies AS
	(
		SELECT tenant.TenantId, template.FeatureCode, template.ModuleCode,
			COALESCE
			(
				(SELECT TOP (1) model.ModelDeploymentId
				 FROM ActiveChatModels model
				 WHERE model.ModelCode = primaryModel.ModelCode
				   AND (model.TenantId = tenant.TenantId OR model.TenantId IS NULL)
				 ORDER BY CASE WHEN model.TenantId = tenant.TenantId THEN 0 ELSE 1 END,
						  model.Priority, model.CreatedDateUtc),
				(SELECT TOP (1) model.ModelDeploymentId
				 FROM ActiveChatModels model
				 WHERE model.TenantId = tenant.TenantId OR model.TenantId IS NULL
				 ORDER BY CASE WHEN model.TenantId = tenant.TenantId THEN 0 ELSE 1 END,
						  model.IsFallback, model.Priority, model.CreatedDateUtc)
			) AS PrimaryModelDeploymentId,
			COALESCE
			(
				(SELECT TOP (1) model.ModelDeploymentId
				 FROM ActiveChatModels model
				 WHERE model.ModelCode = fallbackModel.ModelCode
				   AND (model.TenantId = tenant.TenantId OR model.TenantId IS NULL)
				 ORDER BY CASE WHEN model.TenantId = tenant.TenantId THEN 0 ELSE 1 END,
						  model.Priority, model.CreatedDateUtc),
				(SELECT TOP (1) model.ModelDeploymentId
				 FROM ActiveChatModels model
				 WHERE model.TenantId = tenant.TenantId OR model.TenantId IS NULL
				 ORDER BY CASE WHEN model.TenantId = tenant.TenantId THEN 0 ELSE 1 END,
						  CASE WHEN model.IsFallback = 1 THEN 0 ELSE 1 END,
						  model.Priority, model.CreatedDateUtc)
			) AS FallbackModelDeploymentId,
			template.Temperature, template.MaximumInputTokens,
			template.MaximumOutputTokens, template.TimeoutSeconds,
			template.DailyCostLimit, template.MonthlyCostLimit,
			template.MinimumConfidence, template.RequiresHumanReview
		FROM SaaS.SaaS_Tenant tenant
		CROSS JOIN TemplatePolicies template
		LEFT JOIN AI.Legal_ModelDeployment primaryModel
		  ON primaryModel.ModelDeploymentId = template.PrimaryModelDeploymentId
		LEFT JOIN AI.Legal_ModelDeployment fallbackModel
		  ON fallbackModel.ModelDeploymentId = template.FallbackModelDeploymentId
		WHERE tenant.IsDeleted = 0
		  AND tenant.StatusCode = N'Active'
		  AND template.Choice = 1
	)
	MERGE AI.Legal_FeaturePolicy AS target
	USING
	(
		SELECT TenantId, FeatureCode, ModuleCode, PrimaryModelDeploymentId,
			CASE WHEN FallbackModelDeploymentId = PrimaryModelDeploymentId
				 THEN NULL ELSE FallbackModelDeploymentId END AS FallbackModelDeploymentId,
			Temperature, MaximumInputTokens, MaximumOutputTokens, TimeoutSeconds,
			DailyCostLimit, MonthlyCostLimit, MinimumConfidence, RequiresHumanReview
		FROM SourcePolicies
		WHERE PrimaryModelDeploymentId IS NOT NULL
	) AS source
	   ON target.TenantId = source.TenantId
	  AND target.FeatureCode = source.FeatureCode
	  AND target.IsDeleted = 0
	WHEN MATCHED THEN UPDATE SET
		target.ModuleCode = source.ModuleCode,
		target.PrimaryModelDeploymentId = source.PrimaryModelDeploymentId,
		target.FallbackModelDeploymentId = source.FallbackModelDeploymentId,
		target.Temperature = source.Temperature,
		target.MaximumInputTokens = source.MaximumInputTokens,
		target.MaximumOutputTokens = source.MaximumOutputTokens,
		target.TimeoutSeconds = source.TimeoutSeconds,
		target.DailyCostLimit = source.DailyCostLimit,
		target.MonthlyCostLimit = source.MonthlyCostLimit,
		target.MinimumConfidence = source.MinimumConfidence,
		target.RequiresHumanReview = source.RequiresHumanReview,
		target.IsEnabled = 1,
		target.ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT
	(
		FeaturePolicyId, TenantId, FeatureCode, ModuleCode,
		PrimaryModelDeploymentId, FallbackModelDeploymentId,
		Temperature, MaximumInputTokens, MaximumOutputTokens, TimeoutSeconds,
		DailyCostLimit, MonthlyCostLimit, MinimumConfidence,
		RequiresHumanReview, IsEnabled, CreatedDateUtc, IsDeleted
	)
	VALUES
	(
		NEWID(), source.TenantId, source.FeatureCode, source.ModuleCode,
		source.PrimaryModelDeploymentId, source.FallbackModelDeploymentId,
		source.Temperature, source.MaximumInputTokens, source.MaximumOutputTokens,
		source.TimeoutSeconds, source.DailyCostLimit, source.MonthlyCostLimit,
		source.MinimumConfidence, source.RequiresHumanReview, 1,
		SYSUTCDATETIME(), 0
	);
END;

COMMIT TRANSACTION;
