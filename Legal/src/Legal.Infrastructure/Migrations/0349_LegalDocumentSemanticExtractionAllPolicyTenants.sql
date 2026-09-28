SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Corrective seed (supersedes the tenant coverage of 0348 on already-migrated databases).
--
-- Symptom: Matter Corpus uploads report "Stored -- no propositions were derived yet" and the API log
-- (and AI.Legal_Execution) shows AiProviderUnavailableException: "No active CHAT model route is
-- configured for this tenant and feature." for LEGAL_DOCUMENT_SEMANTIC_EXTRACTION -- even though the
-- feature policy, models, provider, quota and Azure deployment are all valid.
--
-- Root cause: 0348 seeded the LEGAL_DOCUMENT_SEMANTIC_EXTRACTION feature policy only for tenants in
-- Core.Tenant. The tenants that actually run uploads live in SaaS.SaaS_Tenant (e.g. the active dev
-- tenant DA988708-62B2-4C14-956E-0BAA43C9E902). Those SaaS tenants already own ~12 other
-- AI.Legal_FeaturePolicy rows but were left WITHOUT a semantic-extraction row, so
-- AiProviderRouteRepository.GetRoutesAsync -- which filters strictly on policy.TenantId=@TenantId with
-- no default-tenant fallback -- returns zero routes and the intake service silently skips enrichment.
--
-- Fix (idempotent, authoritative tenant set): seed/repair an enabled LEGAL_DOCUMENT_SEMANTIC_EXTRACTION
-- feature policy for EVERY tenant that already has at least one AI.Legal_FeaturePolicy row. This covers
-- the SaaS tenants that Core.Tenant does not, using 'gpt-6-astra' as the primary model and
-- 'gpt-4.1-mini' as the fallback (also the Auto default resolved by the route repository), guaranteeing
-- a healthy CHAT route.
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
		-- Authoritative tenant set: every tenant that already owns any feature policy. This is broader
		-- than Core.Tenant and includes the SaaS tenants that actually perform uploads.
		;WITH SourcePolicy AS
		(
			SELECT DISTINCT existing.TenantId,
				N'LEGAL_DOCUMENT_SEMANTIC_EXTRACTION' FeatureCode,
				N'Intelligence' ModuleCode,
				@PrimaryDeploymentId PrimaryModelDeploymentId,
				@FallbackDeploymentId FallbackModelDeploymentId,
				CONVERT(decimal(4,3),0.000) Temperature,
				32000 MaximumInputTokens,
				8000 MaximumOutputTokens,
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
	END
END

COMMIT TRANSACTION;
