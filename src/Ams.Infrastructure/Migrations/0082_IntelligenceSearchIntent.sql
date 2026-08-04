SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'AI.SearchDocument',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'AI.SearchDocument',N'SourceCreatedDateUtc') IS NULL
		ALTER TABLE AI.SearchDocument ADD SourceCreatedDateUtc DATETIME2 NULL;

	IF NOT EXISTS
	(
		SELECT 1
		FROM sys.indexes
		WHERE object_id=OBJECT_ID(N'AI.SearchDocument')
		  AND name=N'IX_AI_SearchDocument_Recency'
	)
		CREATE INDEX IX_AI_SearchDocument_Recency
			ON AI.SearchDocument(TenantId,EntityTypeCode,SourceCreatedDateUtc DESC)
			INCLUDE(ModuleCode,Title,EntityId)
			WHERE IsDeleted=0;
END;
