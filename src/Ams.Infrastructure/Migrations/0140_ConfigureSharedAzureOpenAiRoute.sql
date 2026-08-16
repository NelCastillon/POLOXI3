SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Both Intelligent Search implementations use the same database-backed Azure OpenAI provider.
-- The gpt-4.1-mini deployment is hosted by this Azure AI Services resource.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'https://agencybinder-1226-resource.cognitiveservices.azure.com/',
		DefaultValue=N'https://agencybinder-1226-resource.cognitiveservices.azure.com/',
		Description=N'Shared Azure OpenAI resource endpoint for governed Intelligence features.',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL AND ScopeCode=N'Platform' AND SettingKey=N'AI:AzureOpenAI:Endpoint' AND IsDeleted=0;

	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'',
		DefaultValue=N'',
		Description=N'Optional env:// API key reference. Empty uses DefaultAzureCredential and managed identity or Azure CLI credentials.',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL AND ScopeCode=N'Platform' AND SettingKey=N'AI:AzureOpenAI:Credential' AND IsDeleted=0;
END;

COMMIT TRANSACTION;
