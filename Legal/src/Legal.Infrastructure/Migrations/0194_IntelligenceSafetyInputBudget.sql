-- 0194_IntelligenceSafetyInputBudget
-- Raise the governed AI input character guard (Intelligence.Safety.MaximumInputCharacters).
-- The INTELLIGENCE_WIDE_ANSWER feature policy already permits 20000 input tokens (~80000 chars),
-- so the 20000-character safety guard was the binding constraint that forced aggressive prompt
-- truncation and could still trip when large system prompts, contracts, and live grounding
-- snippets combined. 60000 characters (~15000 tokens) stays well inside the model input budget
-- while giving the Wide answer prompt room to keep its evidence sections. DB remains the source
-- of truth for the safety limit.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=CASE WHEN TRY_CONVERT(INT,SettingValue)<60000 THEN N'60000' ELSE SettingValue END,
		DefaultValue=CASE WHEN TRY_CONVERT(INT,DefaultValue)<60000 THEN N'60000' ELSE DefaultValue END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'Intelligence.Safety.MaximumInputCharacters'
	  AND IsDeleted=0;
END;
