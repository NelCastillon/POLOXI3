SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Enable the bounded research loop (§34, §35)
--
-- The closed-loop research stage (LegalDecisionService → RunResearchLoop) is gated on the DB-backed
-- flag Decision.ResearchLoop.Enabled, which defaults OFF in code so the shadow baseline stays intact
-- until validated. This migration turns the loop ON and pins its conservative budgets so the loop can
-- actually execute rounds instead of reporting NOT RUN · NO RESEARCH EXECUTION. Budgets remain small
-- so an accidental enable can never run away; they mirror the code fallbacks in
-- LegalDecisionRepository.GetResearchLoopSettingsAsync.
--
-- Idempotent: each setting is upserted by SettingKey and un-deleted if a prior tombstoned row exists.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionSetting', N'U') IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_DecisionSetting AS target
	USING (VALUES
		(N'Decision.ResearchLoop.Enabled',                  N'true', N'Boolean', N'Master switch for the bounded research loop. §34/§35.'),
		(N'Decision.ResearchLoop.MaxRounds',                N'4',    N'Integer', N'Max research rounds per session (round budget). §34.'),
		(N'Decision.ResearchLoop.MaxRetrievals',            N'12',   N'Integer', N'Cumulative retrieval cap across all rounds. §34.'),
		(N'Decision.ResearchLoop.MinFrontierInformationValue', N'0.15', N'Decimal', N'Minimum frontier IV worth researching before stopping. §11.'),
		(N'Decision.ResearchLoop.NoStateChangeEpsilon',     N'0.01', N'Decimal', N'ε below which a round is treated as producing no material movement. §34.'),
		(N'Decision.ResearchLoop.UseSeedRetriever',         N'false',N'Boolean', N'Use the deterministic seed retriever instead of the live retriever.')
	) AS source (SettingKey, SettingValue, DataTypeCode, Description)
	ON target.SettingKey = source.SettingKey
	WHEN MATCHED THEN
		UPDATE SET
			SettingValue    = source.SettingValue,
			DataTypeCode    = source.DataTypeCode,
			Description     = source.Description,
			IsDeleted       = 0,
			ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (SettingKey, SettingValue, DataTypeCode, Description)
		VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);
END

COMMIT TRANSACTION;
