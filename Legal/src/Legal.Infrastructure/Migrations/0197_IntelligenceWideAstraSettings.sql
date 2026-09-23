SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Separate DB-backed configuration set for the gpt-6-astra Wide pipeline variant.
-- The existing Wide (Sol) settings live in Core.ConfigurationSetting under the
-- 'Intelligence.SearchWide.*' platform keys and are the source of truth for IntelligenceWideService.
-- This migration clones every current Sol Wide setting into an '_Astra' suffixed key
-- (e.g. Intelligence.SearchWide.TargetConfidence -> Intelligence.SearchWide.TargetConfidence_Astra),
-- seeding the Astra values FROM the current Sol values. It is purely additive and idempotent:
-- existing '_Astra' rows are left untouched so operator edits survive re-runs.
-- (Wiring IntelligenceWideService to read these '_Astra' keys is intentionally deferred;
--  for now they only mirror Sol as requested.)
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	INSERT Core.ConfigurationSetting
	(
		SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,
		DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted
	)
	SELECT
		NEWID(),
		sol.TenantId,
		sol.ScopeCode,
		sol.ModuleCode,
		sol.SettingKey + N'_Astra',
		sol.SettingValue,
		sol.DefaultValue,
		sol.DataTypeCode,
		N'[Astra variant] ' + COALESCE(sol.Description,N''),
		sol.IsEncrypted,
		sol.IsReadOnly,
		SYSUTCDATETIME(),
		0
	FROM Core.ConfigurationSetting sol
	WHERE sol.TenantId IS NULL
	  AND sol.ScopeCode=N'Platform'
	  AND sol.IsDeleted=0
	  AND sol.SettingKey LIKE N'Intelligence.SearchWide.%'
	  AND sol.SettingKey NOT LIKE N'%\_Astra' ESCAPE N'\'
	  AND NOT EXISTS
	  (
		SELECT 1 FROM Core.ConfigurationSetting astra
		WHERE astra.TenantId IS NULL
		  AND astra.ScopeCode=N'Platform'
		  AND astra.IsDeleted=0
		  AND astra.SettingKey=sol.SettingKey + N'_Astra'
	  );
END;

COMMIT TRANSACTION;
