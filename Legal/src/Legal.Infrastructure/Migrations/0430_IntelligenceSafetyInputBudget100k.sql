-- 0430_IntelligenceSafetyInputBudget100k
-- Raise the governed AI input character guard (Intelligence.Safety.MaximumInputCharacters) from
-- 60000 to 100000. Use Domain Pack mode appends the full DB-backed pack configuration (28 dimensions,
-- 274 L2 nodes) to the proposal prompts; on a matter with a long Description the Stage 0 query
-- contract prompt reached 63493 chars and tripped the 60000 guard (AI.Legal_SafetyEvent
-- INPUT_LENGTH_EXCEEDED), which silently nulled the query contract and produced an empty final
-- decision (no candidates, no dependencies, empty Overview). 100000 chars (~25000 tokens) stays inside
-- the configured model input budgets. DB remains the source of truth for the safety limit.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=CASE WHEN TRY_CONVERT(INT,SettingValue)<100000 THEN N'100000' ELSE SettingValue END,
		DefaultValue=CASE WHEN TRY_CONVERT(INT,DefaultValue)<100000 THEN N'100000' ELSE DefaultValue END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'Intelligence.Safety.MaximumInputCharacters'
	  AND IsDeleted=0;
END;
