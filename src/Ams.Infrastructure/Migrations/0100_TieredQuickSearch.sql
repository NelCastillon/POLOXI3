SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(2000),DataTypeCode NVARCHAR(50),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Intelligence.QuickSearch.EnableIntelligentFallback',N'true',N'Boolean',N'Enables semantic, ontology, relationship, and optional LLM fallback when top-bar Quick Search fast-path results are poor.'),
	(N'Intelligence.QuickSearch.FastPathMinimumResults',N'3',N'Integer',N'Minimum number of fast-path results required to avoid intelligent fallback.'),
	(N'Intelligence.QuickSearch.FastPathMinimumScore',N'0.70',N'Decimal',N'Minimum score required on the best fast-path result to avoid intelligent fallback.');
	MERGE Core.ConfigurationSetting target USING @Config source
	ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.SettingValue,DataTypeCode=source.DataTypeCode,Description=source.Description,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc)
	VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,0,SYSUTCDATETIME());
END;
