-- ============================================================================
-- align-chat-deployment.sql  (OPTIONAL — run manually only if needed)
--
-- Judz's semantic enrichment (LEGAL_DOCUMENT_SEMANTIC_EXTRACTION) calls Azure
-- OpenAI using the DeploymentName stored in AI.Legal_ModelDeployment for the
-- active CHAT model. Migration 0161 seeds that DeploymentName as 'gpt-5.6-sol'.
--
-- Azure requires DeploymentName to EXACTLY match a model deployment that exists
-- in YOUR Azure OpenAI resource. If your real deployment is named differently
-- (e.g. 'gpt-4o', 'gpt-4.1', 'my-chat'), enrichment will fail with HTTP 404
-- "DeploymentNotFound" and uploads will show "no propositions were derived yet".
--
-- HOW TO USE:
--   1. Set @RealDeploymentName below to the deployment name in the Azure portal
--      (Azure OpenAI resource -> Deployments -> the "Deployment name" column).
--   2. Run this against the same dev database configured in
--      appsettings.Development.json (site4now db_ace4f8_deliver888011).
--   3. Re-run the upload after restarting the API.
--
-- Idempotent: only updates the active CHAT deployment row(s).
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RealDeploymentName NVARCHAR(200) = N'gpt-6-astra';  -- <-- EDIT ME (real Azure OpenAI deployment name)

IF OBJECT_ID(N'AI.Legal_ModelDeployment', N'U') IS NOT NULL
BEGIN
	UPDATE model
		SET model.DeploymentName = @RealDeploymentName,
			model.ModifiedDateUtc = SYSUTCDATETIME()
	FROM AI.Legal_ModelDeployment model
	JOIN AI.Legal_Provider provider
		ON provider.ProviderId = model.ProviderId
	   AND provider.ProviderCode = N'AZURE_OPENAI'
	   AND provider.IsActive = 1
	   AND provider.IsDeleted = 0
	WHERE model.CapabilityCode = N'CHAT'
	  AND model.IsActive = 1
	  AND model.IsDeleted = 0
	  AND model.DeploymentName <> @RealDeploymentName;

	SELECT model.ModelCode, model.DeploymentName, model.CapabilityCode, model.IsActive
	FROM AI.Legal_ModelDeployment model
	JOIN AI.Legal_Provider provider ON provider.ProviderId = model.ProviderId
	WHERE provider.ProviderCode = N'AZURE_OPENAI'
	  AND model.CapabilityCode = N'CHAT'
	  AND model.IsDeleted = 0;
END
