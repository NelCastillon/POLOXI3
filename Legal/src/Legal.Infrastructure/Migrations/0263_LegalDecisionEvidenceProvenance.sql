SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Evidence verification provenance (§14)
--
-- Records WHICH proposition (objective) a retrieved source was verified against and WHICH passage
-- provided the support, so a VERIFIED result is traceable: claim ↔ source ↔ passage ↔ verification.
-- SupportedObjective  : the frontier dependency objective the source was verified against.
-- SupportingPassage   : the exact passage text used to establish claim↔passage support.
-- Additive, nullable; no existing data is altered. Idempotent via COL_LENGTH guards.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidence', N'SupportedObjective') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidence ADD SupportedObjective NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidence', N'SupportingPassage') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidence ADD SupportingPassage NVARCHAR(MAX) NULL;

COMMIT TRANSACTION;
