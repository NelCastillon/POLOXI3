SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Phase E2 — Proposition-first retrieval & dual-direction evidence search (feature gates only).
--
-- Stage 1 (candidate passage retrieval) remains the production path. Phase E2 layers two new,
-- opt-in capabilities on top of it:
--   • PropositionQueryRetrieval — treat the atomic proposition as a first-class retrieval query,
--     deriving the embedding query from the proposition rather than the free-text SearchQuery only.
--   • DualDirectionRetrieval — additionally retrieve counter-oriented passages (evidence that could
--     rebut the proposition) alongside support-oriented passages.
--
-- TESTING: both flags are seeded 'true' so Phase E2 proposition-first retrieval (and dual-direction
-- scaffold) are exercised end-to-end. The WHEN MATCHED update below also flips any previously-seeded
-- 'false' rows to 'true' on re-run so an existing database picks up the test configuration. Set these
-- back to 'false' (or override via the settings surface) before shipping to production.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.Retrieval.PropositionQueryRetrieval.Enabled', N'true', N'Boolean', N'When enabled, matter-corpus retrieval derives its semantic query from the atomic proposition (Stage 2 groundwork). When disabled, retrieval uses the existing Stage 1 SearchQuery/keyword path unchanged.'),
	(N'Decision.Retrieval.DualDirectionRetrieval.Enabled',    N'true', N'Boolean', N'When enabled, retrieval additionally requests counter-oriented passages (evidence that could rebut the proposition) alongside support-oriented passages. Requires PropositionQueryRetrieval. Disabled by default.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN MATCHED AND target.SettingValue <> source.SettingValue THEN
	UPDATE SET target.SettingValue = source.SettingValue
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

COMMIT TRANSACTION;
