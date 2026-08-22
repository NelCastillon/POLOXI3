SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Wide-search model selection: seed the gpt-5.6-sol CHAT deployment under the shared
-- Azure OpenAI provider so the /intelligence/search/poloxi_wide model dropdown is fully
-- database-backed (Auto = feature-policy routing; explicit models come from AI.ModelDeployment).
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
		WHERE ProviderId=@ProviderId AND ModelCode=N'gpt-5.6-sol' AND IsDeleted=0
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
			NEWID(),NULL,@ProviderId,N'gpt-5.6-sol',N'gpt-5.6-sol',N'GPT-5.6',N'CHAT',
			1000000,65536,1.250000,10.000000,
			N'USD',1,0,1,SYSUTCDATETIME(),0
		);
	END;
END;

COMMIT TRANSACTION;
