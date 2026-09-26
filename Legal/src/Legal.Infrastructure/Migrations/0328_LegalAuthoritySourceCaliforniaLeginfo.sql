-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0328: California (leginfo) statutory authority source descriptors for the DB-backed legal grounding
-- registry — completes the "scale by DATA, not code" migration by moving California out of hardcoded C#.
--
-- Background: California's 13 most-cited statutory codes were previously hardcoded in
-- OfficialLegalAuthorityRetriever.BuildCaliforniaSources() and applied as an in-code fallback in
-- SearchAsync(). Every other state (0325 Washington, 0326 Nevada, 0327 all states) lives in
-- POLOXI.Legal_AuthoritySource as DATA. This migration removes that inconsistency by seeding the SAME 13
-- California descriptors as global registry rows so California resolves through the exact same registry
-- path as all other states. The companion C# change deletes BuildCaliforniaSources() and the fallback,
-- leaving the registry as the single source of truth for all 50 states — zero hardcoded jurisdictions.
--
-- Values are reproduced VERBATIM from the former C# seeds so California continues to resolve identically:
--   ProviderCode           CA_LEGINFO_{lawCode}
--   JurisdictionCode       NAME:CALIFORNIA
--   AuthorityKindCode      STATUTE
--   CitationPattern        \bCal(?:ifornia|\.)?\s+{CodeName}\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)
--   BaseUrl                https://leginfo.legislature.ca.gov
--   DocumentUrlTemplate    faces/codes_displaySection.xhtml?lawCode={lawCode}&sectionNum={section}
--   ExtractionStrategyCode FULL_PAGE_TEXT
--   Priority               500
--   IsEnabled              1   (verified: California passes the JudzRetrievalDiagnostic smoke test)
--
-- Global (TenantId NULL) seed. Idempotent via the ProviderCode/JurisdictionCode/AuthorityKind existence
-- guard (UX_Legal_AuthoritySource_ProviderJurisdictionKind, migration 0295). Pipeline mechanics,
-- admission gates, POLOXI Core scoring, and the Blazor UI are UNCHANGED.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';

	DECLARE @Seed TABLE
	(
		ProviderCode        NVARCHAR(80)   NOT NULL,
		JurisdictionCode    NVARCHAR(40)   NOT NULL,
		AuthorityKindCode   NVARCHAR(40)   NOT NULL,
		CitationPattern     NVARCHAR(1000) NOT NULL,
		BaseUrl             NVARCHAR(1000) NOT NULL,
		DocumentUrlTemplate NVARCHAR(2000) NOT NULL,
		SectionAnchorTemplate NVARCHAR(500) NULL,
		ExtractionStrategyCode NVARCHAR(40) NOT NULL,
		IsEnabled           BIT            NOT NULL
	);

	-- The 13 California codes formerly hardcoded in C#. CodeName is embedded (already regex-escaped where
	-- needed — only "Business and Professions", "Health and Safety", and "Code of Civil Procedure" contain
	-- spaces, which are matched literally). lawCode drives the leginfo DocumentUrlTemplate.
	INSERT @Seed VALUES
	(N'CA_LEGINFO_VEH',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Vehicle\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=VEH&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_CIV',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Civil\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=CIV&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_PEN',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Penal\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=PEN&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_PROB',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Probate\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=PROB&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_EVID',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Evidence\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=EVID&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_BPC',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Business\ and\ Professions\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=BPC&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_CORP',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Corporations\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=CORP&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_FAM',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Family\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=FAM&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_GOV',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Government\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=GOV&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_HSC',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Health\ and\ Safety\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=HSC&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_INS',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Insurance\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=INS&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_LAB',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Labor\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=LAB&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_LEGINFO_CCP',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Code\ of\ Civil\ Procedure\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://leginfo.legislature.ca.gov',N'faces/codes_displaySection.xhtml?lawCode=CCP&sectionNum={section}',NULL,N'FULL_PAGE_TEXT',1);

	-- Idempotent set-based insert; skips any (Provider,Jurisdiction,Kind) already present from a prior run.
	INSERT POLOXI.Legal_AuthoritySource
	(
		LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,CitationPattern,
		BaseUrl,DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,
		DiscoveryMethodCode,Priority,IsEnabled,TenantId,CreatedByUserId
	)
	SELECT
		NEWID(),s.ProviderCode,s.JurisdictionCode,s.AuthorityKindCode,s.CitationPattern,
		s.BaseUrl,s.DocumentUrlTemplate,s.SectionAnchorTemplate,s.ExtractionStrategyCode,
		N'MANUAL_SEED',500,s.IsEnabled,NULL,@SystemUserId
	FROM @Seed s
	WHERE NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource t
		WHERE t.ProviderCode=s.ProviderCode
		  AND t.JurisdictionCode=s.JurisdictionCode
		  AND t.AuthorityKindCode=s.AuthorityKindCode
		  AND t.TenantId IS NULL
		  AND t.IsDeleted=0
	);
END

COMMIT TRANSACTION;
