SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- Requeue the Legal document search-projection outbox after the GUID-cast fix.
--
-- Root cause: LegalDocumentSearchProjectionDispatcher built the Azure Search
-- document Id with CONVERT(nvarchar(32), <uniqueidentifier>). A uniqueidentifier
-- renders as a 36-char hyphenated GUID, so the cast overflowed nvarchar(32) and
-- SQL Server raised 8115 "Arithmetic overflow error converting expression to
-- data type nvarchar". Because that SELECT is the first statement in the
-- projection try-block, EVERY passage failed before any row reached
-- AI.Legal_SearchDocument. Branches therefore grounded to zero enterprise
-- evidence and the decision Evidence coverage KPI was stuck near 2%. Repeated
-- retries pushed rows to AttemptCount=12 / DEAD_LETTER.
--
-- The dispatcher now casts to nvarchar(36). This migration resets the failed and
-- dead-lettered outbox rows (AttemptCount back to 0, StatusCode PENDING, cleared
-- error/lease) so the background projection worker reprocesses the already
-- extracted documents without requiring a re-upload. COMPLETED rows are left
-- untouched. Idempotent: safe to re-run (only non-completed rows are affected).
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
