-- ============================================================================
-- 0383: Persisted semantic-activation marker for matter document versions.
--
-- WHY: Corpus activation idempotency was derived purely from the existence of
-- Legal_MatterEvidenceItem rows. That signal is too narrow: a version that was
-- activated but yielded ZERO evidence (a legitimately low-signal document) or
-- whose activation FAILED never persists any evidence, so it is treated as
-- "pending" forever and re-read (real per-document LLM semantic extraction) on
-- every answer. This is the observed "Preparing documents (0 of 30)" re-processing.
--
-- FIX: Record an explicit per-version semantic-activation outcome so already
-- attempted versions are not re-enriched endlessly:
--   NULL         -> never activated (eligible)
--   N'ENRICHED'  -> activated, produced evidence (done)
--   N'NO_EVIDENCE' -> activated, produced no evidence (done; do not re-read)
--   N'FAILED'    -> activation attempt failed (eligible for retry)
--
-- Columns added to POLOXI.Legal_MatterDocumentVersion:
--   SemanticActivationStatusCode NVARCHAR(40) NULL
--   SemanticActivatedDateUtc     DATETIME2    NULL
--
-- Backfill: any version that already has at least one evidence item is marked
-- ENRICHED so existing matters do not re-activate after this migration.
--
-- Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_MatterDocumentVersion', N'SemanticActivationStatusCode') IS NULL
	ALTER TABLE POLOXI.Legal_MatterDocumentVersion ADD SemanticActivationStatusCode NVARCHAR(40) NULL;

IF COL_LENGTH(N'POLOXI.Legal_MatterDocumentVersion', N'SemanticActivatedDateUtc') IS NULL
	ALTER TABLE POLOXI.Legal_MatterDocumentVersion ADD SemanticActivatedDateUtc DATETIME2 NULL;
GO

-- Backfill ENRICHED for versions that already carry derived evidence so prepared
-- matters are not re-activated after deploy. Only touch rows not already marked.
UPDATE version
SET version.SemanticActivationStatusCode = N'ENRICHED',
	version.SemanticActivatedDateUtc = ISNULL(version.SemanticActivatedDateUtc, SYSUTCDATETIME())
FROM POLOXI.Legal_MatterDocumentVersion version
WHERE version.SemanticActivationStatusCode IS NULL
  AND EXISTS (SELECT 1 FROM POLOXI.Legal_MatterEvidenceItem evidence
			  WHERE evidence.LegalDocumentVersionId = version.LegalDocumentVersionId
				AND evidence.IsDeleted = 0);
GO

COMMIT TRANSACTION;
GO
