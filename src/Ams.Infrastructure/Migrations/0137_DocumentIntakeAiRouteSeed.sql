SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Configure Azure OpenAI endpoint/credential references so provider routes can resolve connection settings.
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
		(N'AI:AzureOpenAI:Endpoint',N'env://AMS_AZURE_OPENAI_ENDPOINT',N'String',N'Azure OpenAI endpoint URI or env:// environment reference. Configure AMS_AZURE_OPENAI_ENDPOINT with the resource endpoint.',0),
		(N'AI:AzureOpenAI:Credential',N'env://AMS_AZURE_OPENAI_KEY',N'String',N'Optional env:// environment reference containing an Azure OpenAI API key. Blank uses DefaultAzureCredential and managed identity.',1);

	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL
	  AND target.ScopeCode=N'Platform'
	  AND target.SettingKey=source.SettingKey
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=N'Intelligence',
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
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,
			source.SettingValue,source.DataTypeCode,source.Description,source.IsEncrypted,0,
			SYSUTCDATETIME(),0
		);
END;

-- Seed a platform-wide CHAT model deployment under the existing Azure OpenAI provider.
IF OBJECT_ID(N'AI.ModelDeployment',N'U') IS NOT NULL AND OBJECT_ID(N'AI.Provider',N'U') IS NOT NULL
BEGIN
	DECLARE @ProviderId UNIQUEIDENTIFIER=
	(
		SELECT TOP(1) ProviderId FROM AI.Provider
		WHERE ProviderCode=N'AZURE_OPENAI' AND IsActive=1 AND IsDeleted=0 AND TenantId IS NULL
		ORDER BY CreatedDateUtc
	);

	IF @ProviderId IS NOT NULL AND NOT EXISTS
	(
		SELECT 1 FROM AI.ModelDeployment
		WHERE ProviderId=@ProviderId AND CapabilityCode=N'CHAT' AND IsDeleted=0
	)
	BEGIN
		INSERT AI.ModelDeployment
		(
			ModelDeploymentId,TenantId,ProviderId,ModelCode,DeploymentName,ModelFamily,CapabilityCode,
			ContextWindowTokens,MaximumOutputTokens,InputCostPerMillionTokens,OutputCostPerMillionTokens,
			CurrencyCode,Priority,IsFallback,IsActive,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),NULL,@ProviderId,N'gpt-4.1-mini',N'gpt-4.1-mini',N'GPT-4.1',N'CHAT',
			1000000,32768,0.400000,1.600000,
			N'USD',0,0,1,SYSUTCDATETIME(),0
		);
	END;
END;

-- Seed feature policies for every tenant and every approved document-intake prompt code that has no policy yet.
IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL AND OBJECT_ID(N'AI.ModelDeployment',N'U') IS NOT NULL
   AND OBJECT_ID(N'Core.Tenant',N'U') IS NOT NULL AND OBJECT_ID(N'DMS.AiPromptDefinition',N'U') IS NOT NULL
BEGIN
	WITH TenantChatRoute AS
	(
		SELECT tenant.TenantId,
			(SELECT TOP(1) model.ModelDeploymentId FROM AI.ModelDeployment model JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT' AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL) ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,model.IsFallback,model.Priority,model.CreatedDateUtc) PrimaryModelDeploymentId,
			(SELECT TOP(1) model.ModelDeploymentId FROM AI.ModelDeployment model JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT' AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL) ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,CASE WHEN model.IsFallback=1 THEN 0 ELSE 1 END,model.Priority,model.CreatedDateUtc) FallbackModelDeploymentId
		FROM Core.Tenant tenant
	),
	SourcePolicy AS
	(
		SELECT route.TenantId,prompt.PromptCode FeatureCode,N'DocumentIntake' ModuleCode,route.PrimaryModelDeploymentId,
			   CASE WHEN route.FallbackModelDeploymentId=route.PrimaryModelDeploymentId THEN NULL ELSE route.FallbackModelDeploymentId END FallbackModelDeploymentId,
			   CONVERT(decimal(4,3),0.000) Temperature,16000 MaximumInputTokens,4000 MaximumOutputTokens,120 TimeoutSeconds,CONVERT(decimal(5,4),0.7000) MinimumConfidence
		FROM TenantChatRoute route
		CROSS JOIN (SELECT DISTINCT PromptCode FROM DMS.AiPromptDefinition) prompt
		WHERE route.PrimaryModelDeploymentId IS NOT NULL
	)
	MERGE AI.FeaturePolicy target USING SourcePolicy source
	   ON target.TenantId=source.TenantId AND target.FeatureCode=source.FeatureCode AND target.IsDeleted=0
	WHEN MATCHED AND target.PrimaryModelDeploymentId IS NULL THEN UPDATE SET ModuleCode=source.ModuleCode,PrimaryModelDeploymentId=source.PrimaryModelDeploymentId,FallbackModelDeploymentId=COALESCE(target.FallbackModelDeploymentId,source.FallbackModelDeploymentId),Temperature=source.Temperature,MaximumInputTokens=source.MaximumInputTokens,MaximumOutputTokens=source.MaximumOutputTokens,TimeoutSeconds=source.TimeoutSeconds,MinimumConfidence=source.MinimumConfidence,IsEnabled=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(FeaturePolicyId,TenantId,FeatureCode,ModuleCode,PrimaryModelDeploymentId,FallbackModelDeploymentId,Temperature,MaximumInputTokens,MaximumOutputTokens,TimeoutSeconds,DailyCostLimit,MonthlyCostLimit,MinimumConfidence,RequiresHumanReview,IsEnabled,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),source.TenantId,source.FeatureCode,source.ModuleCode,source.PrimaryModelDeploymentId,source.FallbackModelDeploymentId,source.Temperature,source.MaximumInputTokens,source.MaximumOutputTokens,source.TimeoutSeconds,NULL,NULL,source.MinimumConfidence,0,1,SYSUTCDATETIME(),0);
END;

COMMIT TRANSACTION;
