SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'SubjectMatterJurisdiction') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD SubjectMatterJurisdiction NVARCHAR(300) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'PersonalTerritorialJurisdiction') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD PersonalTerritorialJurisdiction NVARCHAR(300) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'ProceduralLaw') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD ProceduralLaw NVARCHAR(300) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'AuthorityCutoffDate') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD AuthorityCutoffDate DATE NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'AuthorityScopeJson') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD AuthorityScopeJson NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'AuthorityScopeJson') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD AuthorityScopeJson NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'POLOXI.Legal_AuthoritySearchPlan', N'AuthorityScopeJson') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySearchPlan ADD AuthorityScopeJson NVARCHAR(MAX) NULL;

EXEC(N'
UPDATE decisionSession
SET AuthorityScopeJson = JSON_MODIFY(
	JSON_MODIFY(
	JSON_MODIFY(
	JSON_MODIFY(
	JSON_MODIFY(N''{}'', N''$.governingLaw'', decisionSession.GoverningLaw),
		N''$.courtOrForum'', decisionSession.CourtOrForum),
		N''$.authorityCutoffDate'', CONVERT(NVARCHAR(10), decisionSession.AuthorityCutoffDate, 23)),
		N''$.statusCode'', CASE WHEN decisionSession.GoverningLaw IS NULL AND decisionSession.CourtOrForum IS NULL THEN N''UNRESOLVED'' ELSE N''PARTIALLY_RESOLVED'' END),
		N''$.provenanceCode'', N''SESSION_SNAPSHOT'')
FROM POLOXI.Legal_DecisionSession AS decisionSession
WHERE decisionSession.IsDeleted = 0 AND decisionSession.AuthorityScopeJson IS NULL;

UPDATE researchNeed
SET AuthorityScopeJson = decisionSession.AuthorityScopeJson
FROM POLOXI.Legal_DecisionResearchNeed AS researchNeed
INNER JOIN POLOXI.Legal_DecisionSession AS decisionSession ON decisionSession.DecisionSessionId = researchNeed.DecisionSessionId
WHERE researchNeed.IsDeleted = 0 AND researchNeed.AuthorityScopeJson IS NULL;

UPDATE searchPlan
SET AuthorityScopeJson = researchNeed.AuthorityScopeJson
FROM POLOXI.Legal_AuthoritySearchPlan AS searchPlan
INNER JOIN POLOXI.Legal_DecisionResearchNeed AS researchNeed ON researchNeed.DecisionResearchNeedId = searchPlan.DecisionResearchNeedId
WHERE searchPlan.IsDeleted = 0 AND searchPlan.AuthorityScopeJson IS NULL;
');

COMMIT TRANSACTION;
