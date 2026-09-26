-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0327: All-US-state statutory authority source descriptors for the DB-backed legal grounding registry.
--
-- Purpose: complete the "scale by DATA, not code" coverage started by 0325 (Washington) and 0326
-- (Nevada) by seeding a statutory descriptor for EVERY US state into POLOXI.Legal_AuthoritySource, so a
-- matter in any state can ground statutory evidence with no C# change and no redeploy. California is
-- already covered by the built-in CA_LEGINFO_* seeds; Washington/Nevada by 0325/0326. This migration is
-- idempotent per (ProviderCode,JurisdictionCode,AuthorityKind) and coexists with those rows.
--
-- HONEST VERIFICATION GUARDRAIL (important): we do NOT enable 50 unverified URLs. The retriever selects
-- only rows WHERE IsEnabled=1, so a seeded-but-disabled row is INERT — it never issues a fetch and never
-- returns a silent COVERAGE_GAP/PROVIDER_FAILURE that would masquerade as coverage. Each row's IsEnabled
-- reflects verification confidence:
--   * IsEnabled=1  -> official state publisher with a stable, template-expressible URL scheme that maps
--                     a plain citation to a fetchable HTML section (FULL_PAGE_TEXT / HTML_ID_SECTION).
--   * IsEnabled=0  -> seeded with DiscoveryMethodCode='PENDING_VERIFICATION'. The descriptor (regex +
--                     best-known URL template) is present and ready, but the publisher is JS-rendered,
--                     uses opaque/session URLs, or splits into many named codes that still need per-code
--                     rows. Flip IsEnabled=1 after JudzRetrievalDiagnostic confirms a live fetch.
--
-- This staged model gives complete DATA coverage now (every state has a row) while keeping runtime
-- behavior truthful (only verified rows fetch). Enabling a pending state later is a one-line UPDATE, not
-- a code change. Named-code states (e.g. TX, NY) get one broad best-effort row here and can be expanded
-- into per-code rows (like California's 13) as needed.
--
-- Pipeline mechanics, admission gates, POLOXI Core scoring, and the Blazor UI are UNCHANGED.
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

	-- ─────────────────────────────────────────────────────────────────────────────────────────────
	-- ENABLED: official publishers with stable, template-expressible URL schemes.
	-- ─────────────────────────────────────────────────────────────────────────────────────────────

	-- Alaska: AS 09.65.290 -> touchngo akstats. Flat title.chapter.section, FULL_PAGE_TEXT chapter page.
	INSERT @Seed VALUES
	(N'AK_LEG_STATUTES',N'NAME:ALASKA',N'STATUTE',
	 N'\bAS\s*(?:§+\s*)?(?<section>\d+\.\d+\.\d[\dA-Za-z.]*)',
	 N'https://www.akleg.gov',N'basis/statutes.asp#{section}',NULL,N'FULL_PAGE_TEXT',0);

	-- Colorado: C.R.S. § 13-21-111 (LexisNexis-hosted; opaque URLs -> pending).
	INSERT @Seed VALUES
	(N'CO_CRS',N'NAME:COLORADO',N'STATUTE',
	 N'\b(?:C\.R\.S\.|Colo(?:rado)?\.?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',
	 N'https://leg.colorado.gov',N'colorado-revised-statutes',NULL,N'FULL_PAGE_TEXT',0);

	-- Georgia: O.C.G.A. § 51-3-1 (LexisNexis-hosted -> pending).
	INSERT @Seed VALUES
	(N'GA_OCGA',N'NAME:GEORGIA',N'STATUTE',
	 N'\bO\.C\.G\.A\.\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',
	 N'https://law.justia.com',N'codes/georgia',NULL,N'FULL_PAGE_TEXT',0);

	-- Hawaii: HRS § 663-1 -> capitol.hawaii.gov (FULL_PAGE_TEXT).
	INSERT @Seed VALUES
	(N'HI_HRS',N'NAME:HAWAII',N'STATUTE',
	 N'\b(?:HRS|Haw(?:aii)?\.?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',
	 N'https://www.capitol.hawaii.gov',N'hrscurrent/Vol13_Ch0601-0676/HRS0663/HRS_0663-{section}.htm',NULL,N'FULL_PAGE_TEXT',0);

	-- Idaho: Idaho Code § 6-1601 -> legislature.idaho.gov statutesrules (FULL_PAGE_TEXT).
	INSERT @Seed VALUES
	(N'ID_STATUTES',N'NAME:IDAHO',N'STATUTE',
	 N'\bIdaho\s+Code\s*(?:§+\s*|Ann\.?\s*§?\s*)?(?<section>\d[\dA-Za-z.\-]*)',
	 N'https://legislature.idaho.gov',N'statutesrules/idstat/Title{section}',NULL,N'FULL_PAGE_TEXT',0);

	-- Michigan: MCL 600.2912 -> legislature.mi.gov (FULL_PAGE_TEXT).
	INSERT @Seed VALUES
	(N'MI_MCL',N'NAME:MICHIGAN',N'STATUTE',
	 N'\b(?:MCL|Mich(?:igan)?\.?\s+Comp(?:iled)?\.?\s+Laws)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',
	 N'https://www.legislature.mi.gov',N'Laws/MCL?objectName=mcl-{section}',NULL,N'FULL_PAGE_TEXT',0);

	-- Minnesota: Minn. Stat. § 604.01 -> revisor.mn.gov (FULL_PAGE_TEXT).
	INSERT @Seed VALUES
	(N'MN_STATUTES',N'NAME:MINNESOTA',N'STATUTE',
	 N'\bMinn(?:esota)?\.?\s+Stat(?:utes)?\.?\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',
	 N'https://www.revisor.mn.gov',N'statutes/cite/{section}',NULL,N'FULL_PAGE_TEXT',1);

	-- Oregon: ORS 30.010 -> oregonlegislature.gov (FULL_PAGE_TEXT).
	INSERT @Seed VALUES
	(N'OR_ORS',N'NAME:OREGON',N'STATUTE',
	 N'\b(?:ORS|Or(?:egon)?\.?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',
	 N'https://www.oregonlegislature.gov',N'bills_laws/ors/ors{section}.html',NULL,N'FULL_PAGE_TEXT',0);

	-- Wisconsin: Wis. Stat. § 895.045 -> docs.legis.wisconsin.gov (FULL_PAGE_TEXT).
	INSERT @Seed VALUES
	(N'WI_STATUTES',N'NAME:WISCONSIN',N'STATUTE',
	 N'\bWis(?:consin)?\.?\s+Stat(?:utes)?\.?\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',
	 N'https://docs.legis.wisconsin.gov',N'statutes/statutes/{section}',NULL,N'FULL_PAGE_TEXT',1);

	-- ─────────────────────────────────────────────────────────────────────────────────────────────
	-- PENDING_VERIFICATION (IsEnabled=0): descriptor present and ready; publisher is JS-rendered,
	-- uses opaque/session URLs, or needs per-named-code rows. Flip to 1 after diagnostic confirmation.
	-- Regex uses a generic <section> capture; BaseUrl points at the official publisher root.
	-- ─────────────────────────────────────────────────────────────────────────────────────────────
	INSERT @Seed VALUES
	(N'AL_CODE',N'NAME:ALABAMA',N'STATUTE',N'\bAla(?:bama)?\.?\s+Code\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://alison.legislature.state.al.us',N'code-of-alabama/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'AZ_ARS',N'NAME:ARIZONA',N'STATUTE',N'\b(?:A\.R\.S\.|Ariz(?:ona)?\.?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://www.azleg.gov',N'ars/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'AR_CODE',N'NAME:ARKANSAS',N'STATUTE',N'\bArk(?:ansas)?\.?\s+Code\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://law.justia.com',N'codes/arkansas',NULL,N'FULL_PAGE_TEXT',0),
	(N'CT_GEN_STAT',N'NAME:CONNECTICUT',N'STATUTE',N'\bConn(?:ecticut)?\.?\s+Gen(?:eral)?\.?\s+Stat(?:utes)?\.?\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://www.cga.ct.gov',N'current/pub/chap.htm#{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'DE_CODE',N'NAME:DELAWARE',N'STATUTE',N'\bDel(?:aware)?\.?\s+Code\s*(?:Ann\.?\s*)?(?:tit\.?\s*)?(?<section>\d[\dA-Za-z.,\s§\-]*)',N'https://delcode.delaware.gov',N'title{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'FL_STATUTES',N'NAME:FLORIDA',N'STATUTE',N'\bFla(?:\.|orida)?\s+Stat(?:utes)?\.?\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://www.leg.state.fl.us',N'Statutes/index.cfm?App_mode=Display_Statute&Search_String={section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'IL_COMP_STAT',N'NAME:ILLINOIS',N'STATUTE',N'\b(?:\d+\s+ILCS\s+\d[\dA-Za-z./\-]*|Ill(?:inois)?\.?\s+Comp(?:iled)?\.?\s+Stat(?:utes)?\.?\s*(?<section>\d[\dA-Za-z.\-]*))',N'https://www.ilga.gov',N'legislation/ilcs/ilcs.asp',NULL,N'FULL_PAGE_TEXT',0),
	(N'IN_CODE',N'NAME:INDIANA',N'STATUTE',N'\bInd(?:iana)?\.?\s+Code\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://iga.in.gov',N'laws/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'IA_CODE',N'NAME:IOWA',N'STATUTE',N'\bIowa\s+Code\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://www.legis.iowa.gov',N'docs/code/{section}.pdf',NULL,N'FULL_PAGE_TEXT',0),
	(N'KS_STATUTES',N'NAME:KANSAS',N'STATUTE',N'\b(?:K\.S\.A\.|Kan(?:sas)?\.?\s+Stat(?:utes)?\.?\s+Ann\.?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://www.ksrevisor.org',N'statutes/chapters/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'KY_REV_STAT',N'NAME:KENTUCKY',N'STATUTE',N'\b(?:KRS|Ky(?:\.|entucky)?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://apps.legislature.ky.gov',N'law/statutes/statute.aspx?id={section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'LA_RS',N'NAME:LOUISIANA',N'STATUTE',N'\bLa(?:\.|ouisiana)?\s+(?:Rev(?:ised)?\.?\s+Stat(?:utes)?\.?|R\.S\.)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.:\-]*)',N'https://www.legis.la.gov',N'legis/Law.aspx?d={section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'ME_REV_STAT',N'NAME:MAINE',N'STATUTE',N'\bMe(?:\.|aine)?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?\s*(?:Ann\.?\s*)?(?:tit\.?\s*)?(?<section>\d[\dA-Za-z.,\s§\-]*)',N'https://legislature.maine.gov',N'legis/statutes/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'MD_CODE',N'NAME:MARYLAND',N'STATUTE',N'\bMd(?:\.|aryland)?\s+Code\s*(?:Ann\.?\s*)?(?<section>[\dA-Za-z.,\s§\-]*)',N'https://mgaleg.maryland.gov',N'mgawebsite/Laws/StatuteText?article={section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'MA_GEN_LAWS',N'NAME:MASSACHUSETTS',N'STATUTE',N'\bMass(?:achusetts)?\.?\s+Gen(?:eral)?\.?\s+Laws\s*(?:ch\.?\s*)?(?<section>\d[\dA-Za-z.,\s§\-]*)',N'https://malegislature.gov',N'Laws/GeneralLaws/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'MS_CODE',N'NAME:MISSISSIPPI',N'STATUTE',N'\bMiss(?:issippi)?\.?\s+Code\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://law.justia.com',N'codes/mississippi',NULL,N'FULL_PAGE_TEXT',0),
	(N'MO_REV_STAT',N'NAME:MISSOURI',N'STATUTE',N'\b(?:Mo(?:\.|issouri)?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?|RSMo)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://revisor.mo.gov',N'main/OneSection.aspx?section={section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'MT_CODE',N'NAME:MONTANA',N'STATUTE',N'\b(?:MCA|Mont(?:ana)?\.?\s+Code\s+Ann\.?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://leg.mt.gov',N'bills/mca/title_{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'NE_REV_STAT',N'NAME:NEBRASKA',N'STATUTE',N'\bNeb(?:raska)?\.?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://nebraskalegislature.gov',N'laws/statutes.php?statute={section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'NH_REV_STAT',N'NAME:NEW HAMPSHIRE',N'STATUTE',N'\b(?:RSA|N\.H\.\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?\s+Ann\.?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.:\-]*)',N'https://www.gencourt.state.nh.us',N'rsa/html/{section}.htm',NULL,N'FULL_PAGE_TEXT',0),
	(N'NJ_STATUTES',N'NAME:NEW JERSEY',N'STATUTE',N'\bN\.J\.\s+Stat(?:utes)?\.?\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.:\-]*)',N'https://law.justia.com',N'codes/new-jersey',NULL,N'FULL_PAGE_TEXT',0),
	(N'NM_STATUTES',N'NAME:NEW MEXICO',N'STATUTE',N'\bN\.M\.\s+Stat(?:utes)?\.?\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://law.justia.com',N'codes/new-mexico',NULL,N'FULL_PAGE_TEXT',0),
	(N'NY_CONS_LAWS',N'NAME:NEW YORK',N'STATUTE',N'\bN\.Y\.\s+(?<section>[A-Za-z.\s]+(?:Law|C\.P\.L\.R\.)\s*(?:§+\s*)?\d[\dA-Za-z.\-]*)',N'https://www.nysenate.gov',N'legislation/laws/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'NC_GEN_STAT',N'NAME:NORTH CAROLINA',N'STATUTE',N'\bN\.C\.\s+Gen(?:eral)?\.?\s+Stat(?:utes)?\.?\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://www.ncleg.gov',N'EnactedLegislation/Statutes/HTML/BySection/Chapter_{section}.html',NULL,N'FULL_PAGE_TEXT',0),
	(N'ND_CENT_CODE',N'NAME:NORTH DAKOTA',N'STATUTE',N'\bN\.D\.\s+Cent(?:ury)?\.?\s+Code\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://ndlegis.gov',N'cencode/t{section}.pdf',NULL,N'FULL_PAGE_TEXT',0),
	(N'OH_REV_CODE',N'NAME:OHIO',N'STATUTE',N'\b(?:R\.C\.|Ohio\s+Rev(?:ised)?\.?\s+Code(?:\s+Ann\.?)?)\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://codes.ohio.gov',N'ohio-revised-code/section-{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'OK_STATUTES',N'NAME:OKLAHOMA',N'STATUTE',N'\bOkla(?:homa)?\.?\s+Stat(?:utes)?\.?\s*(?:tit\.?\s*)?(?<section>\d[\dA-Za-z.,\s§\-]*)',N'https://law.justia.com',N'codes/oklahoma',NULL,N'FULL_PAGE_TEXT',0),
	(N'PA_CONS_STAT',N'NAME:PENNSYLVANIA',N'STATUTE',N'\b(?<section>\d+)\s+Pa(?:\.|nnsylvania)?\s+(?:Cons(?:olidated)?\.?\s+)?Stat(?:utes)?\.?\s*(?:§+\s*)?(?<pasection>\d[\dA-Za-z.\-]*)',N'https://www.legis.state.pa.us',N'cfdocs/legis/LI/consCheck.cfm?txtType=HTM&title={section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'RI_GEN_LAWS',N'NAME:RHODE ISLAND',N'STATUTE',N'\bR\.I\.\s+Gen(?:eral)?\.?\s+Laws\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'http://webserver.rilegislature.gov',N'Statutes/TITLE{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'SC_CODE',N'NAME:SOUTH CAROLINA',N'STATUTE',N'\bS\.C\.\s+Code\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://www.scstatehouse.gov',N'code/t{section}.php',NULL,N'FULL_PAGE_TEXT',0),
	(N'SD_COD_LAWS',N'NAME:SOUTH DAKOTA',N'STATUTE',N'\bS\.D\.\s+Cod(?:ified)?\.?\s+Laws\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://sdlegislature.gov',N'Statutes/Codified_Laws/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'TN_CODE',N'NAME:TENNESSEE',N'STATUTE',N'\bTenn(?:essee)?\.?\s+Code\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://law.justia.com',N'codes/tennessee',NULL,N'FULL_PAGE_TEXT',0),
	(N'TX_CODE',N'NAME:TEXAS',N'STATUTE',N'\bTex(?:as)?\.?\s+(?<code>[A-Za-z.&\s]+?)\s+Code\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://statutes.capitol.texas.gov',N'Docs/{code}/htm/{code}.{section}.htm',NULL,N'FULL_PAGE_TEXT',0),
	(N'UT_CODE',N'NAME:UTAH',N'STATUTE',N'\bUtah\s+Code\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://le.utah.gov',N'xcode/Title{section}.html',NULL,N'FULL_PAGE_TEXT',0),
	(N'VT_STATUTES',N'NAME:VERMONT',N'STATUTE',N'\bVt(?:\.|ermont)?\s+Stat(?:utes)?\.?\s*(?:Ann\.?\s*)?(?:tit\.?\s*)?(?<section>\d[\dA-Za-z.,\s§\-]*)',N'https://legislature.vermont.gov',N'statutes/section/{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'VA_CODE',N'NAME:VIRGINIA',N'STATUTE',N'\bVa(?:\.|irginia)?\s+Code\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-:]*)',N'https://law.lis.virginia.gov',N'vacode/{section}/',NULL,N'FULL_PAGE_TEXT',0),
	(N'WV_CODE',N'NAME:WEST VIRGINIA',N'STATUTE',N'\bW\.\s*Va\.\s+Code\s*(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://code.wvlegislature.gov',N'{section}',NULL,N'FULL_PAGE_TEXT',0),
	(N'WY_STATUTES',N'NAME:WYOMING',N'STATUTE',N'\bWyo(?:ming)?\.?\s+Stat(?:utes)?\.?\s*(?:Ann\.?\s*)?(?:§+\s*)?(?<section>\d[\dA-Za-z.\-]*)',N'https://law.justia.com',N'codes/wyoming',NULL,N'FULL_PAGE_TEXT',0);

	-- Idempotent set-based insert; skips any (Provider,Jurisdiction,Kind) already present (CA/WA/NV or a prior run of this migration).
	INSERT POLOXI.Legal_AuthoritySource
	(
		LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,CitationPattern,
		BaseUrl,DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,
		DiscoveryMethodCode,Priority,IsEnabled,TenantId,CreatedByUserId
	)
	SELECT
		NEWID(),s.ProviderCode,s.JurisdictionCode,s.AuthorityKindCode,s.CitationPattern,
		s.BaseUrl,s.DocumentUrlTemplate,s.SectionAnchorTemplate,s.ExtractionStrategyCode,
		CASE WHEN s.IsEnabled=1 THEN N'MANUAL_SEED' ELSE N'PENDING_VERIFICATION' END,
		500,s.IsEnabled,NULL,@SystemUserId
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
