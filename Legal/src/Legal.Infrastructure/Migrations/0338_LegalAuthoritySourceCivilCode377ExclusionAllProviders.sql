-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0338: Extend the California wrongful-death "§ 377.x" Civil-Code exclusion to EVERY CIV-worded
--       Civil Code provider (not just CA_LEGINFO_CIV).
--
-- Background / root cause:
--   Migration 0336 added a negative lookahead so the CA_LEGINFO_CIV descriptor REFUSES the 377-series
--   and routes those citations to CA_LEGINFO_CCP. But the authority inventory contains MORE THAN ONE
--   California Civil-Code provider. In particular CA_FINDLAW_CIV carries the same greedy pattern
--       \bCal(?:ifornia|\.)?\s+Civ(?:il)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)
--   with NO 377 exclusion, so it still captures "Cal. Civ. Code § 377.60" and produces a SECOND,
--   contradictory Civil-Code authority identity for a statute that actually lives in the Code of Civil
--   Procedure (CCP § 377.60). 0337 did not retire it because FindLaw's DocumentUrlTemplate does not
--   contain "lawCode=CIV" and its CitationPattern contains no literal "377" — so both of 0337's arms
--   miss it. The result: the report still shows CCP § 377.60 as a "Cal. Civ. Code" source.
--
-- Fix (idempotent, forward-only):
--   For every GLOBAL (TenantId NULL) California STATUTE provider whose ProviderCode denotes a Civil-Code
--   source (LIKE '%CIV%') and whose CitationPattern is a Civil-Code-worded pattern that does NOT already
--   exclude the 377-series, inject the same negative lookahead 0336 used, immediately before the
--   (?<section>…) capture:
--       (?!377\b|377[.:])
--   This makes ALL Civil-Code providers (leginfo, FindLaw, and any future CIV provider) refuse the
--   377-series so the citation resolves ONLY through the CCP descriptor — a single authority identity
--   per statute. Ordinary Civil Code citations (1714, 3294, …) are unaffected. Providers whose pattern
--   already contains the lookahead are skipped, so re-running is a no-op.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_AuthoritySource
	SET CitationPattern = REPLACE(
			CitationPattern,
			N'(?<section>\d[\dA-Za-z.:-]*)',
			N'(?!377\b|377[.:])(?<section>\d[\dA-Za-z.:-]*)'),
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE JurisdictionCode = N'NAME:CALIFORNIA'
	  AND AuthorityKindCode = N'STATUTE'
	  AND TenantId IS NULL
	  AND IsDeleted = 0
	  AND ProviderCode LIKE N'%CIV%'
	  -- Only rows that actually contain the exact generic section capture we replace. CHARINDEX treats
	  -- the needle as a pure literal (no LIKE wildcard/character-class interpretation of the brackets).
	  AND CHARINDEX(N'(?<section>\d[\dA-Za-z.:-]*)', CitationPattern) > 0
	  -- … and do NOT already exclude the 377-series (so re-running is a no-op and the CCP-worded
	  -- 377 alternation added in 0336 is never touched).
	  AND CHARINDEX(N'(?!377', CitationPattern) = 0;
END

COMMIT TRANSACTION;
