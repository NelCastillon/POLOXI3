-- 0431_IntelligenceSafetyOutputBudget280k
-- Raise the governed AI output character guard (Intelligence.Safety.MaximumOutputCharacters) from
-- 80000 to 280000. Migration 0167 sized 80000 for a 16000-token output budget, but the Wide feature
-- policies now grant 32000 tokens (INTELLIGENCE_WIDE_ANSWER / HIERARCHY_STEP / LANDSCAPE_ANSWER) and
-- 65536 tokens (INTELLIGENCE_WIDE_ANSWER_ASTRA). A legitimate structured Candidate x Branch matrix
-- response reached 84226 chars and was REJECTED post-execution (AI.Legal_SafetyEvent
-- OUTPUT_LENGTH_EXCEEDED); every matrix chunk degraded fail-soft to zero scores, the competition
-- reported NO_COMPETING_CANDIDATES and the cockpit showed 0% support / Assessment pending.
-- 280000 chars (~70000 tokens) covers the largest configured output budget. Raise-only; DB remains the
-- source of truth for the safety limit.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=CASE WHEN TRY_CONVERT(INT,SettingValue)<280000 THEN N'280000' ELSE SettingValue END,
		DefaultValue=CASE WHEN TRY_CONVERT(INT,DefaultValue)<280000 THEN N'280000' ELSE DefaultValue END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'Intelligence.Safety.MaximumOutputCharacters'
	  AND IsDeleted=0;
END;
