SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Widen the Typed Legal Dependency Graph V2 burden-rule text columns.
--
-- The V2 graph persists burden (B-node) metadata into POLOXI.Legal_DecisionBurdenRule. The
-- StandardOfProof and BurdenedParty columns were originally sized NVARCHAR(120), but the
-- DECISION_GRAPH model emits descriptive standard-of-proof text (e.g. a full summary-judgment
-- standard statement) that routinely exceeds 120 characters. This caused SQL error 2628
-- ("String or binary data would be truncated ... column 'StandardOfProof'") which aborted
-- PersistGraphAsync and forced the pipeline to silently fall back from the V2 graph to V1.
--
-- Widen both text columns to NVARCHAR(400) so the full standard-of-proof and burdened-party
-- descriptions persist without truncation. NULL/NOT NULL constraints are preserved.
--
-- Idempotent: guarded on current column length.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionBurdenRule', N'U') IS NOT NULL
   AND EXISTS (
		SELECT 1
		FROM INFORMATION_SCHEMA.COLUMNS
		WHERE TABLE_SCHEMA = N'POLOXI'
		  AND TABLE_NAME = N'Legal_DecisionBurdenRule'
		  AND COLUMN_NAME = N'StandardOfProof'
		  AND CHARACTER_MAXIMUM_LENGTH = 120)
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionBurdenRule
		ALTER COLUMN StandardOfProof NVARCHAR(400) NULL;
END

IF OBJECT_ID(N'POLOXI.Legal_DecisionBurdenRule', N'U') IS NOT NULL
   AND EXISTS (
		SELECT 1
		FROM INFORMATION_SCHEMA.COLUMNS
		WHERE TABLE_SCHEMA = N'POLOXI'
		  AND TABLE_NAME = N'Legal_DecisionBurdenRule'
		  AND COLUMN_NAME = N'BurdenedParty'
		  AND CHARACTER_MAXIMUM_LENGTH = 120)
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionBurdenRule
		ALTER COLUMN BurdenedParty NVARCHAR(400) NOT NULL;
END

COMMIT TRANSACTION;
