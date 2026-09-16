SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — DEFAULT Dependency Graph (V2) to ON (idempotent).
--
-- Flips Decision.V2.UseDependencyGraph.Default from 'false' to 'true' so the closed-loop
-- dependency-graph pipeline is enabled by default for new decision sessions. A per-session request
-- can still override this for ablation. Complements the repository fallback and the cockpit toggle
-- default, keeping the DB the source of truth.
--
-- If the setting row does not yet exist (fresh DB where 0214 has not run), it is inserted as 'true'.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionSetting', N'U') IS NOT NULL
BEGIN
	IF EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSetting WHERE SettingKey = N'Decision.V2.UseDependencyGraph.Default')
		UPDATE POLOXI.Legal_DecisionSetting
		SET SettingValue = N'true'
		WHERE SettingKey = N'Decision.V2.UseDependencyGraph.Default'
		  AND SettingValue <> N'true';
	ELSE
		INSERT POLOXI.Legal_DecisionSetting (SettingKey, SettingValue, DataTypeCode, Description)
		VALUES (N'Decision.V2.UseDependencyGraph.Default', N'true', N'Boolean',
			N'Default for the per-session V2 dependency-graph toggle. Session request can override for ablation.');
END

COMMIT TRANSACTION;
