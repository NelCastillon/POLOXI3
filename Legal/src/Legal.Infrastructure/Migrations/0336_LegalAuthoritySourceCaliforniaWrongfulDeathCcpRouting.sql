-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0336: Route California wrongful-death "§ 377.x" citations to the Code of Civil Procedure (CCP),
--       not the Civil Code (CIV).
--
-- Background / root cause:
--   California's wrongful-death / survival statutes (§§ 377.10–377.62, including § 377.60 which defines
--   who may bring a wrongful-death action) live in the CODE OF CIVIL PROCEDURE, not the Civil Code.
--   Practitioners very frequently miscite them as "Cal. Civ. Code § 377.60". The CA_LEGINFO_CIV
--   descriptor's CitationPattern matches any "Cal. Civ. Code § <section>", so it greedily captured
--   § 377.60 and produced the leginfo URL
--       faces/codes_displaySection.xhtml?lawCode=CIV&sectionNum=377.60
--   That section does NOT exist under lawCode=CIV, so leginfo returns a JSF shell page with no statute
--   body. Meanwhile the correct CCP descriptor (lawCode=CCP&sectionNum=377.60) was never tried because
--   the § 377.x form was consumed by the CIV pattern. (Migration 0335/retriever changes handle the
--   empty-shell rejection; THIS migration fixes the routing so the citation reaches CCP.)
--
--   Observed in the live trace: "Cal. Civ. Code § 1714" resolved correctly (1714 is a real Civil Code
--   section), while "Cal. Civ. Code § 377.60" returned only page chrome.
--
-- Fix (two coordinated pattern edits, both idempotent and forward-only):
--   1. CA_LEGINFO_CIV — add a negative lookahead so the CIV pattern REFUSES to capture the 377-series
--      wrongful-death/survival sections. It continues to match every other Civil Code section (e.g.
--      1714, 3294) exactly as before.
--        (?!377\b|377[.:])   ← after the section anchor, reject "377" and "377.x"/"377:x" forms.
--   2. CA_LEGINFO_CCP — extend its CitationPattern with an alternation that ALSO accepts the common
--      "Cal. Civ. Code § 377.x" miscitation, in addition to the proper "Code of Civil Procedure" forms
--      broadened in 0334. Only the 377-series is admitted via the Civil-Code-worded branch, so ordinary
--      Civil Code citations are unaffected.
--
--   Jurisdiction, kind, BaseUrl, DocumentUrlTemplate, ExtractionStrategyCode, Priority and every other
--   column are left untouched. Global (TenantId NULL) rows only. Idempotent: rows are updated only when
--   the CitationPattern still differs from the corrected value.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @Route TABLE
	(
		ProviderCode    NVARCHAR(80)   NOT NULL,
		CitationPattern NVARCHAR(2000) NOT NULL
	);

	INSERT @Route VALUES
	-- CIV: match any Civil Code section EXCEPT the 377-series wrongful-death/survival sections.
	(N'CA_LEGINFO_CIV', N'\bCal(?:ifornia|\.)?\s+Civ(?:il)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?!377\b|377[.:])(?<section>\d[\dA-Za-z.:-]*)'),
	-- CCP: accept the proper "Code of Civil Procedure" orderings AND the common "Cal. Civ. Code § 377.x"
	-- wrongful-death miscitation (Civil-Code-worded branch is restricted to the 377-series).
	(N'CA_LEGINFO_CCP', N'\bCal(?:ifornia|\.)?\s+(?:(?:Code\s+(?:of\s+)?Civ(?:il)?\.?\s+Proc(?:edure)?\.?|Civ(?:il)?\.?\s+Proc(?:edure)?\.?\s+Code)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)|Civ(?:il)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>377[\dA-Za-z.:-]*))');

	UPDATE t
	SET t.CitationPattern = r.CitationPattern,
		t.ModifiedDateUtc  = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @Route r
		ON r.ProviderCode = t.ProviderCode
	WHERE t.JurisdictionCode = N'NAME:CALIFORNIA'
	  AND t.AuthorityKindCode = N'STATUTE'
	  AND t.TenantId IS NULL
	  AND t.IsDeleted = 0
	  AND t.CitationPattern <> r.CitationPattern;
END

COMMIT TRANSACTION;
