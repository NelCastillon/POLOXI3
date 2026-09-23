SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Explicit research/retrieval outcome persistence (§13)
--
-- Retrieval failure must become explicit, durable decision state rather than a log-only event. These
-- additive, nullable columns let a rehydrated decision distinguish the epistemic cases:
--   NOT_NEEDED | RETRIEVED | SEARCH_NO_RESULTS | RETRIEVAL_FAILED
-- ResearchFailureDetail captures the failure reason (retrieval exception message) when applicable.
-- Additive only; no existing data is altered. Idempotent via COL_LENGTH guards.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'ResearchStatusCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD ResearchStatusCode NVARCHAR(40) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'ResearchFailureDetail') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD ResearchFailureDetail NVARCHAR(MAX) NULL;

COMMIT TRANSACTION;
