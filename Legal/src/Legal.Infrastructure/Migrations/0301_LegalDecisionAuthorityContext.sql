SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'MatterJurisdiction') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD MatterJurisdiction NVARCHAR(120) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'GoverningLaw') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD GoverningLaw NVARCHAR(120) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'CourtOrForum') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD CourtOrForum NVARCHAR(300) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'AuthorityCutoffDate') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD AuthorityCutoffDate DATE NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'MatterJurisdiction') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD MatterJurisdiction NVARCHAR(120) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'GoverningLaw') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD GoverningLaw NVARCHAR(120) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'CourtOrForum') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD CourtOrForum NVARCHAR(300) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'AuthorityCutoffDate') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD AuthorityCutoffDate DATE NULL;

EXEC(N'
UPDATE decisionSession
SET MatterJurisdiction = COALESCE(NULLIF(LTRIM(RTRIM(decisionMatter.GoverningLaw)), N''''), NULLIF(LTRIM(RTRIM(decisionMatter.State)), N''''), NULLIF(LTRIM(RTRIM(decisionMatter.Jurisdiction)), N'''')),
	GoverningLaw = NULLIF(LTRIM(RTRIM(decisionMatter.GoverningLaw)), N''''),
	CourtOrForum = NULLIF(LTRIM(RTRIM(CONCAT_WS(N'' · '', decisionMatter.CourtSystem, decisionMatter.CourtLevel, decisionMatter.County))), N'''')
FROM POLOXI.Legal_DecisionSession AS decisionSession
INNER JOIN POLOXI.Legal_DecisionMatter AS decisionMatter ON decisionMatter.DecisionMatterId = decisionSession.MatterId
WHERE decisionSession.IsDeleted = 0 AND decisionMatter.IsDeleted = 0
	AND (decisionSession.MatterJurisdiction IS NULL OR decisionSession.GoverningLaw IS NULL OR decisionSession.CourtOrForum IS NULL);

UPDATE researchNeed
SET MatterJurisdiction = decisionSession.MatterJurisdiction,
	GoverningLaw = decisionSession.GoverningLaw,
	CourtOrForum = decisionSession.CourtOrForum,
	AuthorityCutoffDate = decisionSession.AuthorityCutoffDate
FROM POLOXI.Legal_DecisionResearchNeed AS researchNeed
INNER JOIN POLOXI.Legal_DecisionSession AS decisionSession ON decisionSession.DecisionSessionId = researchNeed.DecisionSessionId
WHERE researchNeed.IsDeleted = 0
	AND (researchNeed.MatterJurisdiction IS NULL OR researchNeed.GoverningLaw IS NULL OR researchNeed.CourtOrForum IS NULL OR researchNeed.AuthorityCutoffDate IS NULL);
');

IF NOT EXISTS
(
	SELECT 1 FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionResearchNeed', N'U')
		AND name = N'IX_Legal_DecisionResearchNeed_AuthorityContext'
)
	AND NOT EXISTS
	(
		SELECT 1 FROM sys.stats
		WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionResearchNeed', N'U')
			AND name = N'IX_Legal_DecisionResearchNeed_AuthorityContext'
	)
	EXEC(N'CREATE INDEX IX_Legal_DecisionResearchNeed_AuthorityContext
		ON POLOXI.Legal_DecisionResearchNeed (DecisionSessionId, MatterJurisdiction, StatusCode)
		INCLUDE (GoverningLaw, CourtOrForum, AuthorityCutoffDate, AtomicPropositionId)
		WHERE IsDeleted = 0;');

COMMIT TRANSACTION;
