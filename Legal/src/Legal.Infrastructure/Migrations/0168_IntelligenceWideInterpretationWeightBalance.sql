SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- POLOXI factor-balance correction: interpretation should have meaningful weight in
-- branch confidence while evidence remains slightly dominant for support confidence.
-- Only platform defaults still at the old seeded values are updated; tenant overrides
-- and manually changed platform values are preserved.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'0.45',
		DefaultValue=N'0.45',
		Description=N'Weight of the LLM interpretation prior when computing branch POLOXI confidence.',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'Intelligence.SearchWide.PriorWeight'
	  AND IsDeleted=0
	  AND COALESCE(NULLIF(SettingValue,N''),DefaultValue)=N'0.30';

	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'0.55',
		DefaultValue=N'0.55',
		Description=N'Weight of evidence support when computing branch POLOXI confidence.',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'Intelligence.SearchWide.EvidenceWeight'
	  AND IsDeleted=0
	  AND COALESCE(NULLIF(SettingValue,N''),DefaultValue)=N'0.70';
END;

COMMIT TRANSACTION;
