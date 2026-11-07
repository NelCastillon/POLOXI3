-- 0480_IntelligenceWideModelGpt55.sql
-- Adds the gpt-5.5 deployment under the shared tenant-global Azure OpenAI provider so it appears
-- as a selectable model option in /legal/search (IntelligenceWide) and Decision2 (IntelligenceWide2).
-- Both pages read active CHAT rows from AI.Legal_ModelDeployment; no code change is required.
-- AzureOpenAiProvider.IsReasoningModel already recognizes the gpt-5 family (max_completion_tokens,
-- reasoning_effort, no temperature). Idempotent: skips when an active gpt-5.5 row already exists.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'AI.Legal_ModelDeployment', N'U') IS NOT NULL AND OBJECT_ID(N'AI.Legal_Provider', N'U') IS NOT NULL
BEGIN
	BEGIN TRANSACTION;

	DECLARE @ProviderId UNIQUEIDENTIFIER =
	(
		SELECT TOP (1) ProviderId
		FROM AI.Legal_Provider
		WHERE ProviderCode = N'AZURE_OPENAI' AND IsActive = 1 AND IsDeleted = 0 AND TenantId IS NULL
		ORDER BY CreatedDateUtc
	);

	IF @ProviderId IS NOT NULL
	   AND NOT EXISTS (SELECT 1 FROM AI.Legal_ModelDeployment WHERE ProviderId = @ProviderId AND ModelCode = N'gpt-5.5' AND IsDeleted = 0)
	BEGIN
		INSERT INTO AI.Legal_ModelDeployment
			(ModelDeploymentId, TenantId, ProviderId, ModelCode, DeploymentName, ModelFamily, CapabilityCode,
			 ContextWindowTokens, MaximumOutputTokens, InputCostPerMillionTokens, OutputCostPerMillionTokens,
			 CurrencyCode, Priority, IsFallback, IsActive, CreatedDateUtc, IsDeleted)
		VALUES
			(NEWID(), NULL, @ProviderId, N'gpt-5.5', N'gpt-5.5', N'GPT-5', N'CHAT',
			 400000, 128000, 1.250000, 10.000000,
			 N'USD', 1, 0, 1, SYSUTCDATETIME(), 0);
	END;

	COMMIT TRANSACTION;
END;
