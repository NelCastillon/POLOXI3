SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ──────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Make LEGAL the default decision context (§13)
--
-- The decision Context dropdown pre-selects the row flagged IsDefault = 1. Originally GENERAL was the
-- default, but real matters need candidates grounded in authoritative legal sources (case law and
-- statutes/regulations) so the verification/promotion pipeline has evidence to work with. Switch the
-- default to LEGAL. GENERAL remains selectable for ungrounded/LLM-only reasoning.
--
-- Idempotent + self-correcting: clears any existing default, then sets LEGAL as the sole default so a
-- single, unambiguous default is guaranteed regardless of prior state.
-- ──────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionContext', N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_DecisionContext
	SET IsDefault = 0
	WHERE IsDefault <> 0;

	UPDATE POLOXI.Legal_DecisionContext
	SET IsDefault = 1
	WHERE ContextCode = N'LEGAL';
END

COMMIT TRANSACTION;
