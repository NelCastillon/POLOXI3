SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────
-- 0374: POLOXI Legal — ChannelContribution placement magnitude (Human Intelligence).
--
-- EXTENDS migration 0367 (Legal_ChannelContribution). Adds a single nullable column,
-- PlacementMagnitude, to carry the normalized [0,1] RELATIVE-POSITION intelligence a
-- channel encodes WITHIN the frame it decided in — specifically the Human Intelligence
-- (Attorney Decision Input) channel, where it is the attorney's relative position inside
-- the sibling band they chose to insert/overwrite the proposition at
-- ((ConfirmedValue - lower) / (upper - lower)).
--
-- THIS IS NOT A SCORE. POLOXI Wide2 remains the SOLE owner of candidate competition,
-- uncertainty, IV, convergence, and outcome. PlacementMagnitude is a qualitative
-- relative-position qualifier (like ApplicabilityCode / DirectnessCode, but numeric)
-- that the signal adapter MAY use to scale a support δ when present; when NULL the
-- adapter falls back to its fixed magnitude and behavior is byte-identical to pre-0374.
-- Additive, fail-soft, idempotent (safe to re-run).
-- ────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_ChannelContribution', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_ChannelContribution', N'PlacementMagnitude') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_ChannelContribution
		ADD PlacementMagnitude FLOAT NULL;
END

GO

COMMIT TRANSACTION;
GO
