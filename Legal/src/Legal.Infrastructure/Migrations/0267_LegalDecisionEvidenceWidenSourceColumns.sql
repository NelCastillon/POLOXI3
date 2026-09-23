SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ──────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Widen DecisionEvidence source columns (§14)
--
-- Real retrieved sources carry full legal case captions (e.g. "State of Texas, Acting by and Through
-- the Texas Facilities Commission, for and on Behalf of the Texas ...") that exceed the original
-- NVARCHAR(500) width, causing SQL error 2628 "String or binary data would be truncated" on persist.
-- Widen SourceTitle and SourceRef to NVARCHAR(MAX) so no legitimate source metadata is rejected.
-- Idempotent: only alters when the column is not already MAX (max_length = -1). No data is altered.
-- ──────────────────────────────────────────────────────────────────────────────────────────────────

IF EXISTS (
	SELECT 1
	FROM sys.columns
	WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionEvidence')
	  AND name = N'SourceTitle'
	  AND max_length <> -1)
	ALTER TABLE POLOXI.Legal_DecisionEvidence ALTER COLUMN SourceTitle NVARCHAR(MAX) NULL;

IF EXISTS (
	SELECT 1
	FROM sys.columns
	WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionEvidence')
	  AND name = N'SourceRef'
	  AND max_length <> -1)
	ALTER TABLE POLOXI.Legal_DecisionEvidence ALTER COLUMN SourceRef NVARCHAR(MAX) NULL;

COMMIT TRANSACTION;
