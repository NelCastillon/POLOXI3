SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'Search.MatchProfile',N'SemanticMaximumConcepts') IS NULL
BEGIN
	ALTER TABLE Search.MatchProfile ADD SemanticMaximumConcepts INT NOT NULL CONSTRAINT DF_Search_MatchProfile_SemanticConcepts DEFAULT 12;
END;
GO

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_Search_MatchProfile_SemanticConcepts' AND parent_object_id=OBJECT_ID(N'Search.MatchProfile'))
	ALTER TABLE Search.MatchProfile ADD CONSTRAINT CK_Search_MatchProfile_SemanticConcepts CHECK(SemanticMaximumConcepts BETWEEN 1 AND 50);
GO
