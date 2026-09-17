SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Platform-scoped UI display toggle. When TRUE, the /legal/search result page renders the
-- "End-to-end POLOXI pipeline" diagnostics section. Defaults to FALSE so the section is hidden.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	MERGE Core.ConfigurationSetting AS target
	USING (SELECT N'Intelligence.SearchWide.ShowPipeline' AS SettingKey) AS source
	   ON target.TenantId IS NULL
	  AND target.ScopeCode=N'Platform'
	  AND target.SettingKey=source.SettingKey
	  AND target.IsDeleted=0
	WHEN NOT MATCHED THEN
		INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,N'Platform',N'Intelligence',N'Intelligence.SearchWide.ShowPipeline',N'false',N'false',N'Boolean',N'When enabled, the Wide search result page shows the End-to-end POLOXI pipeline diagnostics section.',0,0,SYSUTCDATETIME(),0);
END;

COMMIT TRANSACTION;
