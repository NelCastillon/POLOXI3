SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionModelRoute', N'U') IS NOT NULL
BEGIN
	DECLARE @Routes TABLE
	(
		FeatureCode NVARCHAR(120) NOT NULL,
		ModelCode NVARCHAR(100) NOT NULL,
		DeploymentName NVARCHAR(100) NOT NULL,
		TimeoutSeconds INT NOT NULL,
		MaxOutputTokens INT NOT NULL,
		Temperature DECIMAL(4,2) NOT NULL,
		Priority INT NOT NULL
	);

	INSERT @Routes (FeatureCode, ModelCode, DeploymentName, TimeoutSeconds, MaxOutputTokens, Temperature, Priority)
	VALUES
		(N'DECISION_DISCOVERY', N'gpt-6-astra', N'gpt-6-astra', 600, 65536, CAST(0.00 AS DECIMAL(4,2)), 10),
		(N'DECISION_ANSWER', N'gpt-6-astra', N'gpt-6-astra', 600, 65536, CAST(0.00 AS DECIMAL(4,2)), 10),
		(N'DECISION_GRAPH', N'gpt-6-astra', N'gpt-6-astra', 600, 65536, CAST(0.00 AS DECIMAL(4,2)), 10),
		(N'DECISION_VERIFY', N'gpt-6-astra', N'gpt-6-astra', 600, 65536, CAST(0.00 AS DECIMAL(4,2)), 10),
		(N'DECISION_RESEARCH_NEED', N'gpt-6-astra', N'gpt-6-astra', 600, 65536, CAST(0.00 AS DECIMAL(4,2)), 10),
		(N'EVIDENCE_SEMANTIC_VERIFY', N'gpt-6-astra', N'gpt-6-astra', 600, 65536, CAST(0.00 AS DECIMAL(4,2)), 10),
		(N'DECISION_SCHEMA_REPAIR', N'gpt-4.1-mini', N'gpt-4.1-mini', 120, 8000, CAST(0.00 AS DECIMAL(4,2)), 10);

	MERGE POLOXI.Legal_DecisionModelRoute AS target
	USING @Routes AS source
	ON target.FeatureCode = source.FeatureCode
		AND target.ModelCode = source.ModelCode
		AND target.TenantId IS NULL
		AND target.IsDeleted = 0
	WHEN MATCHED THEN
		UPDATE SET
			target.ProviderTypeCode = N'AZURE_OPENAI',
			target.DeploymentName = source.DeploymentName,
			target.EndpointReference = N'env://AMS_AZURE_OPENAI_ENDPOINT',
			target.CredentialReference = N'env://AMS_AZURE_OPENAI_KEY',
			target.ApiVersion = N'2024-10-21',
			target.TimeoutSeconds = source.TimeoutSeconds,
			target.MaxOutputTokens = source.MaxOutputTokens,
			target.Temperature = source.Temperature,
			target.Priority = source.Priority,
			target.IsActive = 1,
			target.ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			FeatureCode, ProviderTypeCode, ModelCode, DeploymentName, EndpointReference,
			CredentialReference, ApiVersion, TimeoutSeconds, MaxOutputTokens, Temperature, Priority
		)
		VALUES
		(
			source.FeatureCode, N'AZURE_OPENAI', source.ModelCode, source.DeploymentName,
			N'env://AMS_AZURE_OPENAI_ENDPOINT', N'env://AMS_AZURE_OPENAI_KEY', N'2024-10-21',
			source.TimeoutSeconds, source.MaxOutputTokens, source.Temperature, source.Priority
		);
END

COMMIT TRANSACTION;
