SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- Re-requeue the Legal document search-projection outbox after the GUID-cast
-- fix shipped.
--
-- Why a NEW migration (0371) is required even though 0370 already requeued:
-- the Legal migrator records every applied migration in dbo._LegalMigrations
-- and skips any name already present. If 0370 ran BEFORE the dispatcher fix
-- (CONVERT(nvarchar(32), ...) -> CONVERT(nvarchar(36), ...)) was deployed, the
-- rows it reset to PENDING simply re-failed and returned to FAILED/DEAD_LETTER,
-- and 0370 can never run again to reset them a second time. This migration has a
-- fresh name, so it executes once on the next startup now that the corrected
-- nvarchar(36) projection is in place and can actually succeed.
--
-- Resets failed / dead-lettered / stuck-processing outbox rows (AttemptCount
-- back to 0, StatusCode PENDING, cleared lease/error) so the background
-- projection worker reprocesses the already-extracted passages into
-- AI.Legal_SearchDocument without requiring a document re-upload. COMPLETED rows
-- are left untouched. Idempotent: safe to re-run (only non-completed rows are
-- affected).
-- ============================================================================

IF OBJECT_ID(N'POLOXI.Legal_DocumentSearchProjectionOutbox',N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_DocumentSearchProjectionOutbox
	SET StatusCode=N'PENDING',
		AttemptCount=0,
		NextAttemptDateUtc=SYSUTCDATETIME(),
		ProcessingStartedDateUtc=NULL,
		ProcessedDateUtc=NULL,
		LastError=NULL
	WHERE IsDeleted=0
	  AND StatusCode IN (N'FAILED',N'DEAD_LETTER',N'PROCESSING');
END

COMMIT;

