-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0335: Demote the California FindLaw/Justia static-HTML sources below leginfo so the working source
-- is tried first and the Justia 403 stops being the first (and logged) failure on every CA lookup.
--
-- Background / root cause:
--   Migration 0331 seeded 13 CA_FINDLAW_* static-HTML rows at Priority 200 (intended to be "tried
--   first"), leaving the CA_LEGINFO_* rows at Priority 500 as a fallback. Justia (law.justia.com) now
--   reliably returns HTTP 403 to server-side requests (bot/access protection), so
--   OfficialLegalAuthorityRetriever tries Justia FIRST for every California statute, records
--     LEGAL-TRACE stage=4-official-authority provider=CA_FINDLAW_CIV outcome=ACCESS_DENIED
--       url=https://law.justia.com/codes/california/code-civ/section-3333-1/
--   and only THEN falls back to leginfo (which succeeds). The result is still correct, but every CA
--   lookup wastes a round-trip and logs a warning that looks like a hard failure.
--
--   Lower Priority wins in the retriever's ordering (matches.OrderBy(Priority)). Raising the FindLaw
--   rows to Priority 700 (> leginfo's 500) makes leginfo the primary source and keeps Justia as a
--   last-resort fallback should leginfo ever be unavailable for a given citation. No source is removed
--   or disabled — only the ordering changes.
--
--   0331 is guarded by its own idempotency check and recorded in dbo._LegalMigrations after it first
--   runs, so editing it in place is a no-op on any database where it already executed. This separate
--   forward-only migration is required to land the corrected priority.
--
-- Fix:
--   Set Priority = 700 on the 13 global (TenantId NULL) CA_FINDLAW_* California STATUTE rows. Provider
--   codes, citation patterns, URLs, extraction strategy, and IsEnabled are UNCHANGED. Idempotent:
--   only rows that still differ are touched.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	UPDATE t
	SET t.Priority        = 700,
		t.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	WHERE t.ProviderCode LIKE N'CA_FINDLAW[_]%'
	  AND t.JurisdictionCode = N'NAME:CALIFORNIA'
	  AND t.AuthorityKindCode = N'STATUTE'
	  AND t.TenantId IS NULL
	  AND t.IsDeleted = 0
	  AND t.Priority <> 700;
END

COMMIT TRANSACTION;
