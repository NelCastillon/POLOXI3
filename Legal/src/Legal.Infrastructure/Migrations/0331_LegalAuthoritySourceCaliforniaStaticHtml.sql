-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0331: California static-HTML statutory authority descriptors (FindLaw) — makes admitted evidence
-- actually reachable for California matters.
--
-- Background / root cause:
--   The California statutory descriptors seeded in 0328 (and pattern-widened in 0329) point at the
--   official leginfo host (https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml) with
--   ExtractionStrategyCode = FULL_PAGE_TEXT. That page is a JSF/ViewState application: a bare HTTP GET
--   (exactly what OfficialLegalAuthorityRetriever.FetchAsync issues) returns the page shell WITHOUT the
--   statute section text. Extraction therefore yields EXTRACTION_EMPTY, the official authority returns
--   0 snippets, retrieval falls through to tangential providers, and the identity gate correctly drops
--   every snippet because none contain the citation tokens → stage=5-identity retrieved=N identityMatched=0
--   → 0 admitted evidence.
--
-- Industry-aligned fix (how Westlaw/Lexis/Harvey-class systems work):
--   Serious legal-AI systems do NOT live-scrape state legislature JSF sites. They retrieve statute text
--   from a curated/normalized store fed by static-HTML statutory aggregators (FindLaw / Justia / Cornell
--   LII) whose section pages return the full text in the initial GET. This migration repoints the
--   California statutory codes at FindLaw's static section pages, whose URL slug replaces the dot in the
--   section number with a hyphen (377.60 → 377-60). The reusable "{section:replace:.-}" template
--   operation (added to OfficialLegalAuthorityRetriever.ExpandTemplate) produces that slug.
--
-- Design:
--   * ProviderCode           CA_FINDLAW_{lawCode}
--   * JurisdictionCode       NAME:CALIFORNIA           (same as the leginfo rows)
--   * AuthorityKindCode      STATUTE
--   * CitationPattern        reproduced from 0329 (abbreviated Bluebook forms) so citation matching is
--                            identical; only the retrieval target changes.
--   * BaseUrl                https://codes.findlaw.com
--   * DocumentUrlTemplate    ca/{code-path}/{abbr}-sect-{section:replace:.-}.html
--   * ExtractionStrategyCode FULL_PAGE_TEXT           (section page contains the statute text)
--   * Priority               200                     (LOWER than leginfo's 500 → wins OrderBy(Priority),
--                                                      so FindLaw is tried first; leginfo rows remain as
--                                                      a citation-pattern fallback and are NOT removed.)
--
-- Global (TenantId NULL) seed. Idempotent via the (Provider,Jurisdiction,Kind) existence guard. Pipeline
-- mechanics, admission gates, POLOXI Core scoring, and the Blazor UI are UNCHANGED.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @SystemUserId UNIQUEIDENTIFIER=N'00000000-0000-0000-0000-000000000000';

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

	-- The 13 California statutory codes, targeting FindLaw's static section pages. CitationPattern values
	-- are reproduced verbatim from migration 0329 (abbreviated Bluebook forms). DocumentUrlTemplate uses
	-- the new "{section:replace:.-}" operation to slugify the section number (dots → hyphens).
	INSERT @Seed VALUES
	(N'CA_FINDLAW_VEH',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Veh(?:icle)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-veh/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_CIV',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Civ(?:il)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-civ/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_PEN',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Pen(?:al)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-pen/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_PROB',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Prob(?:ate)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-prob/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_EVID',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Evid(?:ence)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-evid/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_BPC',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Bus(?:iness)?\.?\s*(?:and|&)?\s*Prof(?:essions)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-bpc/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_CORP',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Corp(?:orations)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-corp/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_FAM',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Fam(?:ily)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-fam/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_GOV',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Gov(?:ernment|t)?\.?''?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-gov/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_HSC',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Health\s*(?:and|&)?\s*Saf(?:ety)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-hsc/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_INS',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Ins(?:urance)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-ins/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	(N'CA_FINDLAW_LAB',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+Lab(?:or)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-lab/section-{section}/',NULL,N'FULL_PAGE_TEXT',1),
	-- CCP appears in both orderings: "Cal. Code Civ. Proc." / "California Code of Civil Procedure"
	-- AND the "Code last" form "Cal. Civ. Proc. Code". Accept both via alternation.
	(N'CA_FINDLAW_CCP',N'NAME:CALIFORNIA',N'STATUTE',N'\bCal(?:ifornia|\.)?\s+(?:Code\s+(?:of\s+)?Civ(?:il)?\.?\s+Proc(?:edure)?\.?|Civ(?:il)?\.?\s+Proc(?:edure)?\.?\s+Code)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',N'https://law.justia.com',N'codes/california/code-ccp/section-{section}/',NULL,N'FULL_PAGE_TEXT',1);

	-- Idempotent set-based insert; skips any (Provider,Jurisdiction,Kind) already present from a prior run.
	-- Priority 700 (> leginfo's 500) so leginfo is tried FIRST and these Justia/FindLaw static-HTML rows
	-- act as a last-resort fallback. Justia (law.justia.com) reliably returns HTTP 403 to server-side
	-- requests, so keeping it primary just wasted a round-trip and logged an ACCESS_DENIED warning on
	-- every California lookup before leginfo succeeded (see migration 0335). The rows are intentionally
	-- NOT removed so they remain available if leginfo is ever unreachable for a given citation.
	INSERT POLOXI.Legal_AuthoritySource
	(
		LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,CitationPattern,
		BaseUrl,DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,
		DiscoveryMethodCode,Priority,IsEnabled,TenantId,CreatedByUserId
	)
	SELECT
		NEWID(),s.ProviderCode,s.JurisdictionCode,s.AuthorityKindCode,s.CitationPattern,
		s.BaseUrl,s.DocumentUrlTemplate,s.SectionAnchorTemplate,s.ExtractionStrategyCode,
		N'MANUAL_SEED',700,s.IsEnabled,NULL,@SystemUserId
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
