-- ============================================================================
-- 0402: Re-enable all law.justia.com authority-source descriptors.
--
-- WHY: Migration 0401 disabled every law.justia.com descriptor because Justia
-- was returning HTTP 403 to this host's egress IP. This migration reverses that:
-- it re-enables the Justia-backed rows so the retriever can select them again
-- (e.g. when egressing through a proxy/clean IP via Legal:OfficialAuthority:Proxy,
-- or if Justia becomes reachable again). Because 0401 already ran, editing it
-- would not re-apply; a new migration is the correct idempotent reversal.
--
-- Scope mirrors 0401 exactly: only rows whose BaseUrl resolves to the Justia host
-- are affected, and only rows currently disabled-and-not-deleted are flipped, so
-- the migration is idempotent / re-runnable. Descriptors manually disabled for
-- other reasons but not on the Justia host are untouched.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource', N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_AuthoritySource
	SET IsEnabled = 1,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE IsEnabled = 0
	  AND IsDeleted = 0
	  AND BaseUrl LIKE N'%law.justia.com%';
END;

COMMIT TRANSACTION;
