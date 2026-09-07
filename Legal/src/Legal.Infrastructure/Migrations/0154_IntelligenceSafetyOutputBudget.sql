SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- Intelligence AI safety output budget.
-- The INTELLIGENCE_WIDE_ANSWER feature policy allows up to 8000 output tokens
-- (roughly 32k+ characters once interpretive result sets are returned for every
-- narrowing path), but the platform safety guard
-- Intelligence.Safety.MaximumOutputCharacters was seeded at 20000, so valid,
-- non-truncated model responses were rejected post-execution with
-- "The AI response exceeded the configured maximum output length."
-- Raise the guard to cover the largest configured output token budget with
-- headroom. Only raises; never lowers a tenant-tuned higher value.
-- ============================================================================

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'60000',
		DefaultValue=N'60000',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE SettingKey=N'Intelligence.Safety.MaximumOutputCharacters'
	  AND ScopeCode=N'Platform'
	  AND IsDeleted=0
	  AND TRY_CONVERT(int,SettingValue)<60000;
END;

COMMIT TRANSACTION;
