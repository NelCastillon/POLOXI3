-- ============================================================================
-- 0403: Disable all law.justia.com authority-source descriptors (again).
--
-- WHY: Migration 0402 re-enabled the Justia-backed descriptors; this migration
-- disables them again because Justia still returns HTTP 403 to this host's egress
-- IP, so every Justia lookup just wastes a round-trip and logs ACCESS_DENIED.
-- Because 0401/0402 already ran, editing them would not re-apply; a new migration
-- is the correct idempotent way to flip the state.
--
-- Scope mirrors 0401 exactly: only rows whose BaseUrl resolves to the Justia host
-- are affected, and only rows currently enabled-and-not-deleted are flipped, so
-- the migration is idempotent / re-runnable. Rows are disabled (IsEnabled=0), not
-- deleted, so they can be re-enabled later if Justia becomes reachable.
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
