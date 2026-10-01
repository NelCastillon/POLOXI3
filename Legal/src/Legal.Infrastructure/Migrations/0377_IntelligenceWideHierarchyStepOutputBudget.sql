-- ============================================================================
-- 0377: Raise the INTELLIGENCE_WIDE_HIERARCHY_STEP completion budget.
--
-- WHY: ProposeNextLevelAsync (the dynamic-hierarchy expansion step) emits one
-- structured JSON payload describing EVERY child node proposed for a level. On
-- wide/deep matters that payload now routinely exceeds the previous 16000-token
-- ceiling, so Azure OpenAI returns finish_reason=length and
-- AzureOpenAiProvider throws:
--   "Azure OpenAI output was truncated because the completion hit the configured
--    maximum output tokens (16000); increase MaximumOutputTokens for feature
--    'INTELLIGENCE_WIDE_HIERARCHY_STEP' in AI.Legal_FeaturePolicy."
-- The router treats the truncation as a failed route, exhausts its fallbacks,
-- and the whole Wide background operation fails with
-- AiProviderUnavailableException.
--
-- WHAT: Raise MaximumOutputTokens to 32000 (matching the landscape-answer budget
-- granted in 0376) and give the step proportional completion headroom with a
-- 180s timeout so a single generation finishes without the truncation retry.
--
-- Raise-only: never lowers a tenant-tuned higher value and never shrinks a
-- timeout an operator already raised. Idempotent and safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.Legal_FeaturePolicy
	SET MaximumOutputTokens=CASE WHEN MaximumOutputTokens>32000 THEN MaximumOutputTokens ELSE 32000 END,
		TimeoutSeconds=CASE WHEN TimeoutSeconds>180 THEN TimeoutSeconds ELSE 180 END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode=N'INTELLIGENCE_WIDE_HIERARCHY_STEP'
	  AND IsDeleted=0
	  AND (MaximumOutputTokens<32000 OR TimeoutSeconds<180);
END;

COMMIT TRANSACTION;
GO
