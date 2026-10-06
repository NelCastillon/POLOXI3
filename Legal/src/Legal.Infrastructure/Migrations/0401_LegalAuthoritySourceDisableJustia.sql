-- ============================================================================
-- 0401: Disable all law.justia.com authority-source descriptors.
--
-- WHY: Justia (law.justia.com) reliably returns HTTP 403 (ACCESS_DENIED) to the
-- server-side document GETs this pipeline issues, even with a full browser
-- fingerprint (see OfficialLegalAuthorityRetriever egress hardening and the note
-- in migration 0331). Every Justia-backed lookup therefore wastes a round-trip
-- and logs an ACCESS_DENIED warning before the pipeline falls back to a working
-- provider. For California this is the CA_FINDLAW_* fallback rows repointed to
-- Justia in 0331-0333; the same host also backs the all-states rows from 0327.
--
-- This migration deterministically disables (NOT deletes) every enabled
-- descriptor whose BaseUrl resolves to the Justia host, across all jurisdictions,
-- so the retriever never selects a known-403 source. Rows are preserved
-- (IsEnabled=0, soft state intact) so they can be re-enabled if Justia ever
-- becomes reachable again. Idempotent / re-runnable: only flips rows that are
-- currently enabled.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource', N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_AuthoritySource
	SET IsEnabled = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE IsEnabled = 1
	  AND IsDeleted = 0
	  AND BaseUrl LIKE N'%law.justia.com%';
END;

COMMIT TRANSACTION;
