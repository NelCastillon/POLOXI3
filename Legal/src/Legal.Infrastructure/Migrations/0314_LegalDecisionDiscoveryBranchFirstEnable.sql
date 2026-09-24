SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Activate POLOXI Legal branch-first discovery (v2) by default. Flips the global default of
-- Decision.Discovery.BranchFirst.Enabled from 'false' to 'true' so decisions run DECISION_DISCOVERY_V2
-- (shared semantic root/branch forest + scoreless global candidates, mirroring the /legal/search Wide
-- pipeline; POLOXI Core derives all scoring from retrieved evidence). Idempotent and rollback-safe:
-- it ONLY updates the GLOBAL default row that still carries the seeded 'false' value, so a tenant that
-- has deliberately set its own value (true or false) is never overwritten. Also inserts the row if a
-- database somehow predates migration 0313.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.Discovery.BranchFirst.Enabled', N'true', N'Boolean', N'When true, the legal discovery stage uses DECISION_DISCOVERY_V2 (a shared semantic root/branch forest + a scoreless global candidate universe, mirroring the /legal/search Wide pipeline; POLOXI Core derives all scoring from retrieved evidence). When false, the legacy candidate-first DECISION_DISCOVERY path runs. Enabled by default as of migration 0314.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN MATCHED AND target.SettingValue = N'false' THEN
	UPDATE SET target.SettingValue = source.SettingValue, target.Description = source.Description
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

COMMIT TRANSACTION;
