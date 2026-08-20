SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE
	(
		SettingKey NVARCHAR(200) NOT NULL,
		SettingValue NVARCHAR(2000) NOT NULL,
		DataTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(1000) NOT NULL,
		IsEncrypted BIT NOT NULL
	);

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description,IsEncrypted)
	VALUES
		(N'DocumentIntelligence.Endpoint',N'env://AMS_DOCUMENT_INTELLIGENCE_ENDPOINT',N'String',N'Azure Document Intelligence endpoint URI or env:// environment reference. Configure AMS_DOCUMENT_INTELLIGENCE_ENDPOINT with the resource endpoint.',0),
		(N'DocumentIntelligence.ModelId',N'prebuilt-layout',N'String',N'Azure Document Intelligence model identifier used for enterprise OCR and layout extraction.',0),
		(N'DocumentIntelligence.ApiVersion',N'2024-11-30',N'String',N'Azure Document Intelligence service API version.',0),
		(N'DocumentIntelligence.CredentialReference',N'',N'String',N'Optional env:// environment reference containing an API key. Blank uses DefaultAzureCredential and managed identity.',1),
		(N'DocumentIntelligence.TimeoutSeconds',N'180',N'Integer',N'Document Intelligence submission and polling timeout in seconds. Runtime limits the effective value to 30 through 900 seconds.',0);

	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL
	  AND target.ScopeCode=N'Platform'
	  AND target.SettingKey=source.SettingKey
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=N'DocumentIntake',
			target.SettingValue=COALESCE(NULLIF(target.SettingValue,N''),source.SettingValue),
			target.DefaultValue=source.SettingValue,
			target.DataTypeCode=source.DataTypeCode,
			target.Description=source.Description,
			target.IsEncrypted=source.IsEncrypted,
			target.IsReadOnly=0,
			target.ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,
			DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),NULL,N'Platform',N'DocumentIntake',source.SettingKey,source.SettingValue,
			source.SettingValue,source.DataTypeCode,source.Description,source.IsEncrypted,0,
			SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
