SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Human Intelligence (Attorney Decision Input) default ON.
--
-- The ADI master switch AttorneyDecisionInput.Enabled was seeded OFF in 0359. Product direction is now
-- that Human Intelligence is available by default, so this migration flips the stored master switch to
-- 'true'. Only the master switch is enabled here; the finer-grained ADI capability flags remain as
-- previously seeded so governance can still opt into individual write capabilities deliberately.
--
-- Idempotent + data-only. No schema change. POLOXI remains the single authoritative evaluator.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSetting WHERE SettingKey = N'AttorneyDecisionInput.Enabled' AND IsDeleted = 0)
	UPDATE POLOXI.Legal_DecisionSetting
	SET SettingValue = N'true', ModifiedDateUtc = SYSUTCDATETIME()
	WHERE SettingKey = N'AttorneyDecisionInput.Enabled' AND IsDeleted = 0;
ELSE
	INSERT INTO POLOXI.Legal_DecisionSetting (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (N'AttorneyDecisionInput.Enabled', N'true', N'Boolean', N'Master switch for Attorney Decision Input (Human Intelligence). Enabled by default.');
GO

COMMIT TRANSACTION;
