SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Clarification preflight gate settings (§ clarification eligibility)
--
-- Seeds the four Decision.Preflight.* rows so the gate can be toggled/tuned from the Legal
-- Configuration UI instead of editing the database by hand. Defaults mirror the code fallbacks in
-- LegalDecisionRepository.GetCoreSettingsAsync (the gate stays OFF until an operator enables it):
--
--   Decision.Preflight.Enabled                     = false  (master switch, disabled by default)
--   Decision.Preflight.RequireProceduralInstruction = true  (missing relief/instruction is a gap)
--   Decision.Preflight.RequireFactsWhenProcedureMissing = true (only fire when facts are also thin)
--   Decision.Preflight.MinimumFactsQueryLength     = 40      (fact-thin threshold in characters)
--
-- Idempotent: each setting is upserted by SettingKey and un-deleted if a prior tombstoned row exists.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionSetting', N'U') IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_DecisionSetting AS target
	USING (VALUES
		(N'Decision.Preflight.Enabled',                      N'false', N'Boolean', N'Master switch for the clarification preflight gate. false preserves current behavior (no early clarification); true activates an up-front decision-contract completeness check before the discovery/graph/verification pipeline runs.'),
		(N'Decision.Preflight.RequireProceduralInstruction', N'true',  N'Boolean', N'When true, a missing explicit procedural relief/instruction (Posture and MotionTarget both empty) is treated as an essential-input gap that warrants an up-front clarification.'),
		(N'Decision.Preflight.RequireFactsWhenProcedureMissing', N'true', N'Boolean', N'When true, the gate only fires if the matter/query also supplies no usable facts (so a fact-bearing hypothetical is never blocked). When false, a missing procedural instruction alone is enough.'),
		(N'Decision.Preflight.MinimumFactsQueryLength',      N'40',    N'Integer', N'Minimum effective-query length (characters) below which the query is considered fact-thin for the "facts supplied" test. Raise to demand richer facts; lower to make the gate more permissive.')
	) AS source (SettingKey, SettingValue, DataTypeCode, Description)
	ON target.SettingKey = source.SettingKey
	WHEN MATCHED THEN
		UPDATE SET
			DataTypeCode    = source.DataTypeCode,
			Description     = source.Description,
			IsDeleted       = 0,
			ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (SettingKey, SettingValue, DataTypeCode, Description)
		VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);
END

COMMIT TRANSACTION;
