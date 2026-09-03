SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Disable the POLOXI Wide user-clarification gate globally.
-- POLOXI no longer finishes with USER_CLARIFICATION_REQUIRED; it always returns the best available
-- answer (or the grouped competing interpretations) instead of interrupting the user with a question.
-- This is the fail-soft path the gate was designed for: EnableClarificationGate=false short-circuits
-- BOTH clarification code paths in IntelligenceWideService (the intent-gap gate and the
-- retrieval-stalled gate) because they share the single `configuration.EnableClarificationGate` guard.
--
-- Fully reversible and configuration-driven: set the value back to N'true' to re-enable clarification.
-- Tenant-specific overrides are intentionally preserved; only the global (TenantId IS NULL) default
-- is changed, and only when it still holds the prior default value of N'true'.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'false',
		DefaultValue=N'false',
		Description=N'When true, POLOXI may finish with USER_CLARIFICATION_REQUIRED and a clarification question instead of an answer when the compound uncertainty gate fires. Disabled globally: POLOXI always returns the best available answer (or grouped competing interpretations) rather than interrupting the user. Fail-soft; set back to true to re-enable.',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE SettingKey=N'Intelligence.SearchWide.EnableClarificationGate'
		AND TenantId IS NULL
		AND ScopeCode=N'Platform'
		AND IsDeleted=0
		AND SettingValue=N'true';
END;

COMMIT TRANSACTION;
