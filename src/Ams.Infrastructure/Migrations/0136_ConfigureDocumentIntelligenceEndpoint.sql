SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'https://ams-document-intelligence-dev.cognitiveservices.azure.com/',
		DefaultValue=N'https://ams-document-intelligence-dev.cognitiveservices.azure.com/',
		Description=N'Azure Document Intelligence endpoint URI. This non-secret platform default can be overridden by a tenant-specific database setting.',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'DocumentIntelligence.Endpoint'
	  AND IsDeleted=0
	  AND (SettingValue IS NULL OR SettingValue=N'' OR SettingValue=N'env://AMS_DOCUMENT_INTELLIGENCE_ENDPOINT');
END;

COMMIT TRANSACTION;
