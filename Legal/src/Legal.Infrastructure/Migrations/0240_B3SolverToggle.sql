SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal B3 — Hallucination Solver activation toggle (idempotent).
--
-- The B3 solver ALWAYS recompetes on verified evidence and surfaces the result as a non-destructive
-- shadow "what-if" snapshot, so the cockpit can show BOTH the original decision and the recomputed
-- one side by side. This toggle only decides which of the two is AUTHORITATIVE:
--
--   false = Advisory  → the original (ASPEN_B2) decision stays authoritative/returned; the recomputed
--                       ranking is annotation-only. Default: you get BOTH, original + what-if.
--   true  = Enforced  → the recomputed ranking replaces the returned/persisted decision (the shadow
--                       equals the returned result); unsupported propositions earn zero support.
--
-- Defaults to Advisory ('false') so both rankings are always visible without changing the returned
-- decision. Set this SettingValue to 'true' to enforce the recomputed ranking as authoritative.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.V2.ApplyVerifiedSignalsToRanking', N'false', N'Boolean', N'B3 Hallucination Solver authority mode. false=Advisory (original decision stays authoritative; recomputed ranking shown as a non-destructive what-if shadow so both are visible). true=Enforced (recomputed ranking replaces the returned decision).')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN MATCHED THEN
	UPDATE SET target.SettingValue = source.SettingValue, target.Description = source.Description
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

COMMIT TRANSACTION;
