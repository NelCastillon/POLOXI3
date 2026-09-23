SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'AI.Legal_SearchDocument',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'AI.Legal_SearchDocument',N'SourceCreatedDateUtc') IS NULL
		ALTER TABLE AI.Legal_SearchDocument ADD SourceCreatedDateUtc DATETIME2 NULL;

	IF NOT EXISTS
	(
		SELECT 1
		FROM sys.indexes
		WHERE object_id=OBJECT_ID(N'AI.Legal_SearchDocument')
		  AND name=N'IX_Legal_AI_SearchDocument_Recency'
	)
		CREATE INDEX IX_Legal_AI_SearchDocument_Recency
			ON AI.Legal_SearchDocument(TenantId,EntityTypeCode,SourceCreatedDateUtc DESC)
			INCLUDE(ModuleCode,Title,EntityId)
			WHERE IsDeleted=0;
END;
