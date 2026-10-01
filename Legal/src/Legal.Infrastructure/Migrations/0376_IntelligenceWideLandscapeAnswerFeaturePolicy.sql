-- 0376: Dedicated feature policy for the POLOXI Wide Candidate Landscape Narrative Composer.
--
-- WHY: The Candidate Landscape composer (WIDE_CANDIDATE_LANDSCAPE prompt) emits a per-candidate
-- narrative for the WHOLE candidate set plus the landscape overview in a single call. Its output is
-- fundamentally larger than the single INTELLIGENCE_WIDE_ANSWER call it previously shared. Sharing the
-- 16000-token answer budget caused the completion to hit finish_reason=length and truncate, so the
-- composer failed fail-soft and the Decision Outcomes cards degraded to the raw interpretation text
-- with no composed narrative.
--
-- WHAT: Register a dedicated feature policy INTELLIGENCE_WIDE_LANDSCAPE_ANSWER per tenant by CLONING
-- each tenant's existing INTELLIGENCE_WIDE_ANSWER policy row (same primary/fallback model deployments,
-- so routing cannot fail), then raising MaximumOutputTokens to 32000 and TimeoutSeconds to 180. The
-- "_ANSWER" suffix is intentional: AzureOpenAiProvider keeps medium reasoning effort and the full
-- (uncapped) output budget for ANSWER-suffixed features.
--
-- Additive, idempotent (safe to re-run via MERGE), tenant-scoped, fail-soft.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_FeaturePolicy', N'U') IS NOT NULL
BEGIN
	DECLARE @NewOutputTokens INT = 32000;
	DECLARE @NewTimeoutSeconds INT = 180;

	;WITH SourcePolicy AS
	(
		SELECT
			answer.TenantId,
			N'INTELLIGENCE_WIDE_LANDSCAPE_ANSWER' FeatureCode,
			answer.ModuleCode,
			answer.PrimaryModelDeploymentId,
			answer.FallbackModelDeploymentId,
			answer.Temperature,
			answer.MaximumInputTokens,
			-- Never shrink the budget below the current answer policy; raise to the landscape target.
			CASE WHEN answer.MaximumOutputTokens > @NewOutputTokens THEN answer.MaximumOutputTokens ELSE @NewOutputTokens END MaximumOutputTokens,
			CASE WHEN answer.TimeoutSeconds > @NewTimeoutSeconds THEN answer.TimeoutSeconds ELSE @NewTimeoutSeconds END TimeoutSeconds,
			answer.MinimumConfidence
		FROM AI.Legal_FeaturePolicy answer
		WHERE answer.FeatureCode = N'INTELLIGENCE_WIDE_ANSWER'
		  AND answer.IsDeleted = 0
		  AND answer.PrimaryModelDeploymentId IS NOT NULL
	)
	MERGE AI.Legal_FeaturePolicy AS target
	USING SourcePolicy AS source
	   ON target.TenantId = source.TenantId
	  AND target.FeatureCode = source.FeatureCode
	  AND target.IsDeleted = 0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode = source.ModuleCode,
			target.PrimaryModelDeploymentId = COALESCE(target.PrimaryModelDeploymentId, source.PrimaryModelDeploymentId),
			target.FallbackModelDeploymentId = COALESCE(target.FallbackModelDeploymentId, source.FallbackModelDeploymentId),
			target.Temperature = source.Temperature,
			target.MaximumInputTokens = source.MaximumInputTokens,
			-- Only ever raise the output budget / timeout on re-run; never regress an operator override upward.
			target.MaximumOutputTokens = CASE WHEN target.MaximumOutputTokens > source.MaximumOutputTokens THEN target.MaximumOutputTokens ELSE source.MaximumOutputTokens END,
			target.TimeoutSeconds = CASE WHEN target.TimeoutSeconds > source.TimeoutSeconds THEN target.TimeoutSeconds ELSE source.TimeoutSeconds END,
			target.MinimumConfidence = source.MinimumConfidence,
			target.IsEnabled = 1,
			target.ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			FeaturePolicyId, TenantId, FeatureCode, ModuleCode, PrimaryModelDeploymentId, FallbackModelDeploymentId,
			Temperature, MaximumInputTokens, MaximumOutputTokens, TimeoutSeconds, DailyCostLimit, MonthlyCostLimit,
			MinimumConfidence, RequiresHumanReview, IsEnabled, CreatedDateUtc, IsDeleted
		)
		VALUES
		(
			NEWID(), source.TenantId, source.FeatureCode, source.ModuleCode, source.PrimaryModelDeploymentId, source.FallbackModelDeploymentId,
			source.Temperature, source.MaximumInputTokens, source.MaximumOutputTokens, source.TimeoutSeconds, NULL, NULL,
			source.MinimumConfidence, 0, 1, SYSUTCDATETIME(), 0
		);
END;

COMMIT TRANSACTION;
GO
