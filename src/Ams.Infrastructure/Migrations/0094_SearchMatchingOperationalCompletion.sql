SET XACT_ABORT ON;
GO

UPDATE Search.SearchCapability
SET ConfigurationJson=JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(ConfigurationJson,N'$.maximumTokens',COALESCE(TRY_CAST(JSON_VALUE(ConfigurationJson,N'$.maximumTokens') AS INT),12)),N'$.maximumPhraseLength',COALESCE(TRY_CAST(JSON_VALUE(ConfigurationJson,N'$.maximumPhraseLength') AS INT),3)),N'$.maximumPhrases',COALESCE(TRY_CAST(JSON_VALUE(ConfigurationJson,N'$.maximumPhrases') AS INT),30)),
	ModifiedDateUtc=SYSUTCDATETIME()
WHERE TenantId IS NULL AND CapabilityCode=N'SEMANTIC' AND IsDeleted=0;
GO
