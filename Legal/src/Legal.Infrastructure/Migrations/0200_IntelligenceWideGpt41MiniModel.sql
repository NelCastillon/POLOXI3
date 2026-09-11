SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Seed the gpt-4.1-mini CHAT deployment under the shared Azure OpenAI provider so Auto can route to the exact database-backed model code.
IF OBJECT_ID(N'AI.Legal_ModelDeployment',N'U') IS NOT NULL AND OBJECT_ID(N'AI.Legal_Provider',N'U') IS NOT NULL
BEGIN
	DECLARE @ProviderId UNIQUEIDENTIFIER=
	(
		SELECT TOP(1) ProviderId FROM AI.Legal_Provider
		WHERE ProviderCode=N'AZURE_OPENAI' AND IsActive=1 AND IsDeleted=0 AND TenantId IS NULL
		ORDER BY CreatedDateUtc
	);

	IF @ProviderId IS NOT NULL AND NOT EXISTS
	(
		SELECT 1 FROM AI.Legal_ModelDeployment
		WHERE ProviderId=@ProviderId AND ModelCode=N'gpt-4.1-mini' AND IsDeleted=0
	)
	BEGIN
		INSERT AI.Legal_ModelDeployment
		(
			ModelDeploymentId,TenantId,ProviderId,ModelCode,DeploymentName,ModelFamily,CapabilityCode,
			ContextWindowTokens,MaximumOutputTokens,InputCostPerMillionTokens,OutputCostPerMillionTokens,
			CurrencyCode,Priority,IsFallback,IsActive,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),NULL,@ProviderId,N'gpt-4.1-mini',N'gpt-4.1-mini',N'GPT-4.1 Mini',N'CHAT',
			1000000,16384,0.400000,1.600000,
			N'USD',0,0,1,SYSUTCDATETIME(),0
		);
	END;

	UPDATE AI.Legal_ModelDeployment
	SET IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHERE ProviderId=@ProviderId
	  AND ModelCode IN (N'gpt-5.6-sol',N'gpt-6-astra',N'gpt-4.1-mini')
	  AND IsDeleted=0;
END;

COMMIT TRANSACTION;
