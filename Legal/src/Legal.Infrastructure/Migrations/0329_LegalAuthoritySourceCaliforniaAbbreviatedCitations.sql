-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0329: Broaden California (leginfo) CitationPattern regexes to accept ABBREVIATED reporter forms
-- so resolved authorities stop showing as UNVERIFIED and admitted evidence rises above 0.
--
-- Symptom: The PI decision cockpit listed California sources (e.g. "Cal. Civ. Code § 377.60",
-- "Cal. Civ. Code § 3333.2", "Cal. Code Civ. Proc. § 377.32") but every source was UNVERIFIED and
-- "Admitted evidence" stayed at 0. Root cause is DATA, not code: the CitationPattern regexes seeded by
-- migration 0328 (reproduced verbatim from the former hardcoded C# seeds) only match the fully
-- spelled-out code names — "California Civil Code", "California Vehicle Code", etc. Real legal answers
-- almost always use Bluebook abbreviations — "Cal. Civ. Code", "Cal. Veh. Code",
-- "Cal. Code Civ. Proc." — so OfficialLegalAuthorityRetriever.MatchDescriptors() never matched, no
-- leginfo document was fetched, and no snippet was admitted as verified evidence.
--
-- Two defects fixed here:
--   1. Every pattern now accepts BOTH the full word and its standard abbreviation (Civil|Civ.,
--      Vehicle|Veh., Evidence|Evid., Business and Professions|Bus. & Prof., Health and Safety|
--      Health & Saf., Government|Gov't, etc.).
--   2. The CCP pattern was malformed ("...Code of Civil Procedure Code" — a stray trailing "Code" that
--      can never appear) and is rewritten to match "Cal. Code Civ. Proc." / "California Code of Civil
--      Procedure" with NO trailing "Code".
--
-- This is a pure UPDATE of the 13 existing CA_LEGINFO_* registry rows (migration 0328 is idempotent on
-- Provider/Jurisdiction/Kind, so re-seeding would not touch them). Provider codes, jurisdiction, URLs,
-- extraction strategy, priority, and IsEnabled are UNCHANGED — only CitationPattern is widened. Global
-- (TenantId NULL) rows only. Idempotent: re-running simply reasserts the same broadened patterns.
-- Pipeline mechanics, admission gates, POLOXI Core scoring, and the Blazor UI are UNCHANGED.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @Pat TABLE
	(
		ProviderCode    NVARCHAR(80)   NOT NULL,
		CitationPattern NVARCHAR(1000) NOT NULL
	);

	-- Section tail shared by every code: optional "§"/"section"/"sec." then the captured section number.
	-- Name fragments accept the full word OR its Bluebook abbreviation (with optional trailing period).
	INSERT @Pat VALUES
	(N'CA_LEGINFO_VEH', N'\bCal(?:ifornia|\.)?\s+Veh(?:icle)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_CIV', N'\bCal(?:ifornia|\.)?\s+Civ(?:il)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_PEN', N'\bCal(?:ifornia|\.)?\s+Pen(?:al)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_PROB', N'\bCal(?:ifornia|\.)?\s+Prob(?:ate)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_EVID', N'\bCal(?:ifornia|\.)?\s+Evid(?:ence)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_BPC', N'\bCal(?:ifornia|\.)?\s+Bus(?:iness)?\.?\s*(?:and|&)?\s*Prof(?:essions)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_CORP', N'\bCal(?:ifornia|\.)?\s+Corp(?:orations)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_FAM', N'\bCal(?:ifornia|\.)?\s+Fam(?:ily)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_GOV', N'\bCal(?:ifornia|\.)?\s+Gov(?:ernment|t)?\.?''?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_HSC', N'\bCal(?:ifornia|\.)?\s+Health\s*(?:and|&)?\s*Saf(?:ety)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_INS', N'\bCal(?:ifornia|\.)?\s+Ins(?:urance)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_LAB', N'\bCal(?:ifornia|\.)?\s+Lab(?:or)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	-- CCP appears in both orderings: "Cal. Code Civ. Proc." / "California Code of Civil Procedure"
	-- AND the "Code last" form "Cal. Civ. Proc. Code". Accept both via alternation.
	(N'CA_LEGINFO_CCP', N'\bCal(?:ifornia|\.)?\s+(?:Code\s+(?:of\s+)?Civ(?:il)?\.?\s+Proc(?:edure)?\.?|Civ(?:il)?\.?\s+Proc(?:edure)?\.?\s+Code)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)');

	UPDATE t
	SET t.CitationPattern = p.CitationPattern,
		t.ModifiedDateUtc  = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @Pat p ON p.ProviderCode = t.ProviderCode
	WHERE t.JurisdictionCode = N'NAME:CALIFORNIA'
	  AND t.AuthorityKindCode = N'STATUTE'
	  AND t.TenantId IS NULL
	  AND t.IsDeleted = 0
	  AND t.CitationPattern <> p.CitationPattern;
END

COMMIT TRANSACTION;
