-- ============================================================================
-- 0388: Repair the LEGAL_DOCUMENT_SEMANTIC_EXTRACTION CHAT route for ALL tenants.
--
-- SYMPTOM (Evidence cockpit): "Corpus activation did not complete -- N document
-- version(s) could not be activated into the reasoning engine, so no propositions
-- or evidence were derived for them. This usually means the tenant has no active
-- CHAT model route configured for LEGAL_DOCUMENT_SEMANTIC_EXTRACTION."
-- Admitted evidence shows "0 enterprise -- 0 external -- decision coverage 0%"
-- even though uploads succeeded. This worked before (it used to extract the
-- California-code authorities from the uploaded documents).
--
-- ROOT CAUSE (data drift, not application code): the per-tenant
-- AI.Legal_FeaturePolicy row for LEGAL_DOCUMENT_SEMANTIC_EXTRACTION no longer
-- resolves to a healthy, enabled CHAT deployment. On drifted databases one of
-- these is true for the active tenant:
--   * the policy row was disabled (IsEnabled=0), or
--   * PrimaryModelDeploymentId points at a now-inactive / retired deployment
--     (e.g. the phantom 'gpt-5.6-sol' retired in 0347), or
--   * the output budget is too small for the reasoning model (gpt-6-astra burns
--     hidden reasoning tokens against the same ceiling and truncates), so the
--     extraction fail-softs to empty and nothing is admitted.
-- AiProviderRouteRepository filters strictly on policy.TenantId with no default
-- fallback, so a single bad row yields "no active CHAT model route" and the
-- intake pipeline silently skips enrichment.
--
-- FIX (idempotent, authoritative tenant set): for EVERY tenant that already owns
-- any AI.Legal_FeaturePolicy row, re-point the semantic-extraction policy to the
-- active 'gpt-6-astra' CHAT deployment (primary) with 'gpt-4.1-mini' as fallback,
-- force IsEnabled=1, and raise the completion budget to a reasoning-safe level so
-- the extraction JSON is not truncated. Raise-only on budgets: never lowers an
-- operator-tuned higher value. Safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_ModelDeployment',N'U') IS NOT NULL
   AND OBJECT_ID(N'AI.Legal_Provider',N'U') IS NOT NULL
BEGIN
	DECLARE @ProviderId UNIQUEIDENTIFIER=
	(
		SELECT TOP(1) ProviderId FROM AI.Legal_Provider
		WHERE ProviderCode=N'AZURE_OPENAI' AND IsActive=1 AND IsDeleted=0 AND TenantId IS NULL
		ORDER BY CreatedDateUtc
	);

	DECLARE @AstraDeploymentId UNIQUEIDENTIFIER=
	(
		SELECT TOP(1) ModelDeploymentId FROM AI.Legal_ModelDeployment
		WHERE ProviderId=@ProviderId AND ModelCode=N'gpt-6-astra' AND CapabilityCode=N'CHAT'
		  AND IsActive=1 AND IsDeleted=0
		ORDER BY CreatedDateUtc
	);

	DECLARE @MiniDeploymentId UNIQUEIDENTIFIER=
	(
		SELECT TOP(1) ModelDeploymentId FROM AI.Legal_ModelDeployment
		WHERE ProviderId=@ProviderId AND ModelCode=N'gpt-4.1-mini' AND CapabilityCode=N'CHAT'
		  AND IsActive=1 AND IsDeleted=0
		ORDER BY CreatedDateUtc
	);

	-- Primary must be resolvable; if gpt-6-astra is somehow absent, fall back to gpt-4.1-mini as primary.
	DECLARE @PrimaryDeploymentId UNIQUEIDENTIFIER=COALESCE(@AstraDeploymentId,@MiniDeploymentId);
	DECLARE @FallbackDeploymentId UNIQUEIDENTIFIER=
		CASE WHEN @MiniDeploymentId IS NOT NULL AND @MiniDeploymentId<>@PrimaryDeploymentId
			 THEN @MiniDeploymentId ELSE NULL END;

	IF @PrimaryDeploymentId IS NOT NULL
	BEGIN
		-- Authoritative tenant set: every tenant that already owns any feature policy. This covers the
		-- SaaS tenants that actually perform uploads (Core.Tenant alone misses them).
		;WITH SourcePolicy AS
		(
			SELECT DISTINCT existing.TenantId,
				N'LEGAL_DOCUMENT_SEMANTIC_EXTRACTION' FeatureCode,
				N'Intelligence' ModuleCode,
				@PrimaryDeploymentId PrimaryModelDeploymentId,
				@FallbackDeploymentId FallbackModelDeploymentId,
				CONVERT(decimal(4,3),0.000) Temperature,
				32000 MaximumInputTokens,
				16000 MaximumOutputTokens,
				120 TimeoutSeconds,
				CONVERT(decimal(5,4),0.0000) MinimumConfidence
			FROM AI.Legal_FeaturePolicy existing
			WHERE existing.IsDeleted=0
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
				-- Raise-only budgets so a reasoning model (gpt-6-astra) does not truncate the extraction.
				target.MaximumInputTokens=CASE WHEN target.MaximumInputTokens<source.MaximumInputTokens
					THEN source.MaximumInputTokens ELSE target.MaximumInputTokens END,
				target.MaximumOutputTokens=CASE WHEN target.MaximumOutputTokens<source.MaximumOutputTokens
					THEN source.MaximumOutputTokens ELSE target.MaximumOutputTokens END,
				target.TimeoutSeconds=CASE WHEN target.TimeoutSeconds<source.TimeoutSeconds
					THEN source.TimeoutSeconds ELSE target.TimeoutSeconds END,
				-- Force the policy back on; a disabled row yields "no active CHAT model route".
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

		-- Belt-and-braces: if any semantic-extraction policy still points at an inactive/retired
		-- deployment (drifted primary), re-point it to the healthy primary resolved above.
		UPDATE policy
			SET policy.PrimaryModelDeploymentId=@PrimaryDeploymentId,
				policy.FallbackModelDeploymentId=@FallbackDeploymentId,
				policy.IsEnabled=1,
				policy.ModifiedDateUtc=SYSUTCDATETIME()
		FROM AI.Legal_FeaturePolicy policy
		WHERE policy.FeatureCode=N'LEGAL_DOCUMENT_SEMANTIC_EXTRACTION'
		  AND policy.IsDeleted=0
		  AND NOT EXISTS
		  (
			SELECT 1 FROM AI.Legal_ModelDeployment model
			WHERE model.ModelDeploymentId=policy.PrimaryModelDeploymentId
			  AND model.CapabilityCode=N'CHAT' AND model.IsActive=1 AND model.IsDeleted=0
		  );
	END
END

COMMIT TRANSACTION;
GO
