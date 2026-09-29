SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Embedding routing seed. Passage embedding generation calls IAiProviderRouter.CreateEmbeddingAsync,
-- which resolves a route via AI.Legal_FeaturePolicy + an EMBEDDING-capability AI.Legal_ModelDeployment.
-- Only CHAT deployments/policies were previously seeded, so embedding calls would throw
-- AiProviderUnavailableException. This migration idempotently seeds a text-embedding deployment under
-- the shared Azure OpenAI provider and a per-tenant LEGAL_DOCUMENT_PASSAGE_EMBEDDING feature policy.
-- Idempotent — safe to re-run.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

-- ── 1. EMBEDDING model deployment (platform-level, TenantId NULL) ──────────────────────────────────
IF OBJECT_ID(N'AI.Legal_ModelDeployment', N'U') IS NOT NULL AND OBJECT_ID(N'AI.Legal_Provider', N'U') IS NOT NULL
BEGIN
	DECLARE @ProviderId UNIQUEIDENTIFIER =
	(
		SELECT TOP(1) ProviderId FROM AI.Legal_Provider
		WHERE ProviderCode=N'AZURE_OPENAI' AND IsActive=1 AND IsDeleted=0 AND TenantId IS NULL
		ORDER BY CreatedDateUtc
	);

	IF @ProviderId IS NOT NULL AND NOT EXISTS
	(
		SELECT 1 FROM AI.Legal_ModelDeployment
		WHERE ProviderId=@ProviderId AND ModelCode=N'text-embedding-3-small' AND IsDeleted=0
	)
	BEGIN
		INSERT AI.Legal_ModelDeployment
		(
			ModelDeploymentId,TenantId,ProviderId,ModelCode,DeploymentName,ModelFamily,CapabilityCode,
			ContextWindowTokens,MaximumOutputTokens,InputCostPerMillionTokens,OutputCostPerMillionTokens,
			CurrencyCode,Priority,IsFallback,IsActive,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			-- MaximumOutputTokens is set to 1 (not 0) because CK_Legal_AI_Model_Tokens requires
			-- MaximumOutputTokens > 0. Embedding models produce no output tokens, so this is a benign
			-- non-zero placeholder that is never consumed on the EMBEDDING path.
			NEWID(),NULL,@ProviderId,N'text-embedding-3-small',N'text-embedding-3-small',N'TEXT-EMBEDDING-3',N'EMBEDDING',
			8191,1,0.020000,0.000000,
			N'USD',1,0,1,SYSUTCDATETIME(),0
		);
	END;
END;

-- ── 2. Per-tenant embedding feature policy ────────────────────────────────────────────────────────
IF OBJECT_ID(N'AI.Legal_FeaturePolicy', N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_ModelDeployment', N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_Provider', N'U') IS NOT NULL
   AND OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
BEGIN
	;WITH TenantEmbeddingRoute AS
	(
		SELECT tenant.TenantId,
			(SELECT TOP(1) model.ModelDeploymentId
			 FROM AI.Legal_ModelDeployment model
			 JOIN AI.Legal_Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
			 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'EMBEDDING'
			   AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL)
			 ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,model.IsFallback,model.Priority,model.CreatedDateUtc)
			 PrimaryModelDeploymentId
		FROM Core.Tenant tenant
		WHERE tenant.IsDeleted=0
	),
	SourcePolicy AS
	(
		SELECT route.TenantId,N'LEGAL_DOCUMENT_PASSAGE_EMBEDDING' FeatureCode,N'Intelligence' ModuleCode,route.PrimaryModelDeploymentId,
			-- MaximumOutputTokens is 1 (not 0) because CK_Legal_AI_FeaturePolicy_Values requires
			-- MaximumOutputTokens > 0. Embeddings produce no output tokens, so this is a benign
			-- non-zero placeholder that is never consumed on the EMBEDDING path.
			CONVERT(decimal(4,3),0.000) Temperature,8000 MaximumInputTokens,1 MaximumOutputTokens,60 TimeoutSeconds,CONVERT(decimal(5,4),0.0000) MinimumConfidence
		FROM TenantEmbeddingRoute route
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
			NEWID(),source.TenantId,source.FeatureCode,source.ModuleCode,source.PrimaryModelDeploymentId,NULL,
			source.Temperature,source.MaximumInputTokens,source.MaximumOutputTokens,source.TimeoutSeconds,NULL,NULL,
			source.MinimumConfidence,0,1,SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;

