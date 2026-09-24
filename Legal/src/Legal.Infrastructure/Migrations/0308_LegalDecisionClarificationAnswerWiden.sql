SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Widen the clarification Answer column.
--
-- The decision clarification loop persists the user's answer into
-- POLOXI.Legal_DecisionClarification.Answer, originally sized NVARCHAR(500). Real clarification
-- answers (procedural posture, motion target, burden of proof, party details, etc.) routinely
-- exceed 500 characters, producing a 400 validation error and, at the DB layer, a truncation risk.
-- Widen the column to NVARCHAR(2000) to match the DecisionSearchRequest.ClarificationAnswer
-- contract [StringLength(2000)]. The NOT NULL constraint and the non-empty CHECK are preserved.
--
-- Idempotent: guarded on current column length.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionClarification', N'U') IS NOT NULL
   AND EXISTS (
		SELECT 1
		FROM INFORMATION_SCHEMA.COLUMNS
		WHERE TABLE_SCHEMA = N'POLOXI'
		  AND TABLE_NAME = N'Legal_DecisionClarification'
		  AND COLUMN_NAME = N'Answer'
		  AND CHARACTER_MAXIMUM_LENGTH = 500)
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionClarification
		ALTER COLUMN Answer NVARCHAR(2000) NOT NULL;
END

COMMIT TRANSACTION;
