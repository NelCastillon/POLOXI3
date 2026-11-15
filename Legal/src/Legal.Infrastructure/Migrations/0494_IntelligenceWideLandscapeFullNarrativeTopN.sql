SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0494: Candidate Landscape full-narrative cap (presentation-only latency/token dial).
--   The Candidate Landscape composer authors a per-candidate LLM narrative for EVERY competing Decision
--   Outcome, sharded into one metered LLM call per shard (migration 0462). For large candidate sets this
--   is the SYNTHESIS wall-clock floor and the dominant output-token cost of the presentation stage.
--   This dial lets an operator cap how many TOP-RANKED candidates receive the expensive LLM narrative.
--   The remaining candidates keep the deterministic narrative that is already built from persisted scores
--   + interpretive results and merged in, so:
--     * ALL candidates stay fully scored and ranked by the POLOXI Candidate x Branch competition - the
--       ranking/narrowing logic is NOT touched;
--     * the full candidate set still forms the comparative context sent to the composer;
--     * only the LLM-authored narrative output (tokens, shards, wall-clock) shrinks.
--   Default 0 = every candidate receives the LLM narrative (byte-for-byte unchanged behavior). Set to a
--   positive N (e.g. 3) to author only the top-N and let the rest render deterministically.
--   Idempotent and additive. Nothing is deleted; existing tenant overrides are untouched.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL, Value NVARCHAR(100) NOT NULL, DataTypeCode NVARCHAR(40) NOT NULL, Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,Value,DataTypeCode,Description) VALUES
	(N'Intelligence.SearchWide.LandscapeFullNarrativeTopN',N'0',N'Integer',
	 N'Candidate Landscape full-narrative cap. When greater than 0, only the top-N ranked Decision Outcome candidates receive the LLM-authored narrative; every remaining candidate uses the deterministic narrative built from persisted scores and interpretive results. All candidates stay fully scored and ranked by the POLOXI competition; only presentation output tokens and composer shards shrink. 0 = all candidates get the LLM narrative (unchanged behavior). Clamped 0..100.');

	UPDATE c SET c.DefaultValue=s.Value, c.Description=s.Description, c.ModifiedDateUtc=@Now
	  FROM Core.ConfigurationSetting c JOIN @Settings s ON s.SettingKey=c.SettingKey
	 WHERE c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0;

	INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),NULL,N'Platform',N'Intelligence',s.SettingKey,s.Value,s.Value,s.DataTypeCode,s.Description,0,0,@Now,0
	  FROM @Settings s
	 WHERE NOT EXISTS(SELECT 1 FROM Core.ConfigurationSetting c WHERE c.SettingKey=s.SettingKey AND c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0);
END;

COMMIT TRANSACTION;
