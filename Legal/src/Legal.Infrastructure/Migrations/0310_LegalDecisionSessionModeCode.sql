SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Persist the resolved execution MODE on each decision session.
--
-- The execution mode (DEV / PROD) is resolved ONCE at session creation and frozen into the session's
-- immutable configuration snapshot. Storing ModeCode here lets a rehydrated / continued decision keep
-- its original execution settings (a clarification continuation must not silently switch modes) and
-- lets the UI label development output distinctly.
--
-- Idempotent: guarded by COL_LENGTH.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionSession', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'ModeCode') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionSession
		ADD ModeCode NVARCHAR(20) NULL;
END

COMMIT TRANSACTION;
