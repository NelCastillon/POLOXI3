SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0476: Remove obsolete Search Quality Indicator LLM-refinement settings.
--
-- The Enterprise Search Quality Indicator (0475) was made fully deterministic:
-- grade, composite score, factors, and model recommendations are all computed
-- in code from DB-backed configuration, and the optional LLM pass that only
-- rephrased suggestion MESSAGES was removed. Migration 0475 originally seeded
-- two Core.ConfigurationSetting rows for that pass; they are now unused and are
-- soft-deleted here so already-migrated databases no longer carry dead config.
-- ============================================================================

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET IsDeleted = 1,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE IsDeleted = 0
	  AND SettingKey IN (
			N'Intelligence.SearchWide.QualityIndicator.EnableLlmSuggestions',
			N'Intelligence.SearchWide.QualityIndicator.SuggestionModelCode');
END;

COMMIT TRANSACTION;
