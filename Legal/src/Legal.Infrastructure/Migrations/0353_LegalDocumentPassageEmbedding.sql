SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Passage semantic embeddings. Adds a portable JSON-serialized embedding vector (plus model/provenance
-- columns) to POLOXI.Legal_DocumentPassage so retrieval can match passages by semantic similarity, not
-- only exact-text hashing / keyword overlap. Nullable and additive — existing exact-hash linking and
-- keyword scoring remain intact when hybrid scoring is disabled. Idempotent — safe to re-run.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DocumentPassage', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_DocumentPassage', N'EmbeddingJson') IS NULL
		ALTER TABLE POLOXI.Legal_DocumentPassage ADD EmbeddingJson NVARCHAR(MAX) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DocumentPassage', N'EmbeddingModelCode') IS NULL
		ALTER TABLE POLOXI.Legal_DocumentPassage ADD EmbeddingModelCode NVARCHAR(120) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DocumentPassage', N'EmbeddingGeneratedDateUtc') IS NULL
		ALTER TABLE POLOXI.Legal_DocumentPassage ADD EmbeddingGeneratedDateUtc DATETIME2 NULL;
END;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- DB-backed retrieval flag. Master switch for hybrid semantic scoring (keyword overlap + passage
-- embedding cosine similarity). OFF by default so existing keyword/exact-hash scoring is unchanged
-- until deliberately enabled. Idempotent MERGE — safe to re-run.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.Retrieval.HybridSemanticScoring.Enabled', N'false', N'Boolean', N'Master switch for hybrid semantic scoring (keyword overlap + passage embedding cosine similarity). OFF by default.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

COMMIT TRANSACTION;
