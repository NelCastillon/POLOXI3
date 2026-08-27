SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0167: Reasoning-model completion headroom for the Wide pipeline.
-- Reasoning-family models (gpt-5*, o-series - e.g. gpt-5.6-sol selected via the
-- Wide model override) burn hidden reasoning tokens against max_completion_tokens.
-- With the previous 8000-token budget the visible answer was truncated
-- (finish_reason=length), which made AzureOpenAiProvider silently RE-RUN the
-- entire generation with a doubled budget; the doubled cycle exceeded the
-- 270-second reasoning timeout, the override route failed, and the router fell
-- back to the primary model - measured as a 308-second INTELLIGENCE_WIDE_ANSWER
-- call. Granting the full budget up front eliminates the truncation retry so a
-- reasoning override completes in a single generation.
-- Raise-only: never lowers a tenant-tuned higher value. Standard models are
-- unaffected (the budget is a cap, not a spend).
-- ============================================================================

IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.FeaturePolicy
	SET MaximumOutputTokens=16000,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode IN (N'INTELLIGENCE_WIDE_ANSWER',N'INTELLIGENCE_WIDE_INTENT',N'INTELLIGENCE_WIDE_HIERARCHY_STEP',N'INTELLIGENCE_WIDE_INFORMATION_VALUE')
	  AND IsDeleted=0
	  AND MaximumOutputTokens<16000;
END;

-- The output-character safety guard must cover the largest configured output
-- token budget (16000 tokens can exceed 60000 characters). Raise-only, same
-- pattern as migration 0154.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'80000',
		DefaultValue=N'80000',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE SettingKey=N'Intelligence.Safety.MaximumOutputCharacters'
	  AND ScopeCode=N'Platform'
	  AND IsDeleted=0
	  AND TRY_CONVERT(int,SettingValue)<80000;
END;

COMMIT TRANSACTION;
