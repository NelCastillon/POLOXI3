-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0337: Retire any obsolete standalone Civil-Code (CIV) alias that routes the California wrongful-death
--       "§ 377.x" series to lawCode=CIV.
--
-- Background / root cause:
--   Migration 0336 corrected the canonical CA_LEGINFO_CIV descriptor so it REFUSES the 377-series and
--   routes those citations to CA_LEGINFO_CCP. But a separate, obsolete CIV alias row can still survive
--   in the live inventory — e.g. an auto-bootstrapped descriptor (DiscoveryMethodCode present) or a
--   duplicate whose DocumentUrlTemplate hardcodes lawCode=CIV&sectionNum=377.x. Such a row keeps a
--   second, contradictory authority identity for the SAME statute and can be reused from stale retrieval
--   results, so the report still shows CCP § 377.60 as "Cal. Civ. Code". There must be exactly ONE
--   authority identity per statute (CA · CCP · 377.60), so any CIV-worded 377-series alias is retired.
--
-- Fix (idempotent, forward-only):
--   Soft-delete (IsDeleted = 1) any GLOBAL (TenantId NULL) California STATUTE authority row that pins the
--   377-series to the Civil Code, i.e. a row whose DocumentUrlTemplate contains "lawCode=CIV" AND whose
--   pattern/template hardcodes a 377 section, OR any auto-discovered (DiscoveryMethodCode IS NOT NULL)
--   CIV-provider row that admits the 377-series. The canonical CA_LEGINFO_CIV descriptor (whose 0336
--   pattern already EXCLUDES 377 via negative lookahead and does NOT hardcode a 377 section) is preserved
--   so ordinary Civil Code citations (1714, 3294, …) keep resolving. Only rows still active are touched.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_AuthoritySource
	SET IsDeleted        = 1,
		ModifiedDateUtc   = SYSUTCDATETIME()
	WHERE JurisdictionCode = N'NAME:CALIFORNIA'
	  AND AuthorityKindCode = N'STATUTE'
	  AND TenantId IS NULL
	  AND IsDeleted = 0
	  -- Never retire the canonical, corrected CIV descriptor (0336 already excludes the 377-series there).
	  AND ProviderCode <> N'CA_LEGINFO_CIV'
	  AND (
			-- A duplicate/alias that pins the 377-series to the Civil Code law code.
			(DocumentUrlTemplate LIKE N'%lawCode=CIV%' AND (CitationPattern LIKE N'%377%' OR DocumentUrlTemplate LIKE N'%377%'))
			-- An auto-discovered CIV-provider alias that still admits the 377-series.
			OR (DiscoveryMethodCode IS NOT NULL AND ProviderCode LIKE N'%CIV%' AND CitationPattern LIKE N'%377%')
		  );
END

COMMIT TRANSACTION;
