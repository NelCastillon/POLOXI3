SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Corrective seed: the semantic-extraction feature policy (0345) resolves its CHAT route by picking
-- the first active CHAT AI.Legal_ModelDeployment ordered by (IsFallback, Priority, CreatedDateUtc).
-- Migration 0161 seeded 'gpt-5.6-sol' BEFORE 0195 seeded 'gpt-6-astra', so the policy binds to
-- 'gpt-5.6-sol' -- a DeploymentName that does not exist in the deployed Azure OpenAI resource. Every
-- LEGAL_DOCUMENT_SEMANTIC_EXTRACTION call therefore fails with HTTP 404 "DeploymentNotFound" and
-- uploads report "no propositions were derived yet".
--
-- 'gpt-6-astra' is the real deployed Azure OpenAI model. This migration idempotently:
--   1. Deactivates the phantom 'gpt-5.6-sol' CHAT deployment so no route can bind to it.
--   2. Re-points the semantic-extraction feature policy (all tenants) to the active 'gpt-6-astra' CHAT
--      deployment as its primary model, with 'gpt-4.1-mini' as the explicit fallback so route
--      resolution always has a healthy CHAT deployment to fall back to. 'gpt-4.1-mini' is also the
--      Auto default resolved by AiProviderRouteRepository when no model override is supplied.
IF OBJECT_ID(N'AI.Legal_ModelDeployment',N'U') IS NOT NULL
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

	IF @ProviderId IS NOT NULL AND @AstraDeploymentId IS NOT NULL
	BEGIN
		-- 1. Retire the phantom deployment that has no matching Azure deployment.
		UPDATE AI.Legal_ModelDeployment
			SET IsActive=0, ModifiedDateUtc=SYSUTCDATETIME()
		WHERE ProviderId=@ProviderId AND ModelCode=N'gpt-5.6-sol' AND CapabilityCode=N'CHAT'
		  AND IsActive=1 AND IsDeleted=0;

		-- 2. Re-point the semantic-extraction feature policy: gpt-6-astra primary, gpt-4.1-mini fallback.
		IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
		BEGIN
			UPDATE AI.Legal_FeaturePolicy
				SET PrimaryModelDeploymentId=@AstraDeploymentId,
					FallbackModelDeploymentId=CASE
						WHEN @MiniDeploymentId IS NOT NULL AND @MiniDeploymentId<>@AstraDeploymentId THEN @MiniDeploymentId
						WHEN FallbackModelDeploymentId=@AstraDeploymentId THEN NULL
						ELSE FallbackModelDeploymentId
					END,
					ModifiedDateUtc=SYSUTCDATETIME()
			WHERE FeatureCode=N'LEGAL_DOCUMENT_SEMANTIC_EXTRACTION'
			  AND IsDeleted=0;
		END
	END
END

COMMIT;
