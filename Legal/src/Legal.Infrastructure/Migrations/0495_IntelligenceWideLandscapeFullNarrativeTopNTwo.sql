SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0495: Set the Candidate Landscape full-narrative cap default to 2.
--   Migration 0494 seeded Intelligence.SearchWide.LandscapeFullNarrativeTopN with 0 (every candidate gets
--   the LLM-authored narrative). The Decision Outcome presentation now shows only the two decisive outcomes
--   (the winning outcome and the strongest opposing outcome), so align the LLM narrative work with that:
--   only the top-2 ranked Decision Outcome candidates receive the expensive LLM-authored narrative; every
--   remaining candidate renders the deterministic narrative built from persisted scores + interpretive
--   results. ALL candidates remain fully scored and ranked by the POLOXI Candidate x Branch competition -
--   the ranking/narrowing logic is NOT touched; only presentation output tokens and composer shards shrink.
--   Idempotent and additive: updates the platform default/seed only. Existing tenant overrides are untouched.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	-- Platform default row: set both the active value and the fallback default to 2.
	UPDATE Core.ConfigurationSetting
	   SET SettingValue=N'2', DefaultValue=N'2', ModifiedDateUtc=@Now
	 WHERE SettingKey=N'Intelligence.SearchWide.LandscapeFullNarrativeTopN'
	   AND TenantId IS NULL AND ScopeCode=N'Platform' AND IsDeleted=0;

	-- Seed the platform row if migration 0494 was never applied in this environment.
	INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),NULL,N'Platform',N'Intelligence',N'Intelligence.SearchWide.LandscapeFullNarrativeTopN',N'2',N'2',N'Integer',
		   N'Candidate Landscape full-narrative cap. When greater than 0, only the top-N ranked Decision Outcome candidates receive the LLM-authored narrative; every remaining candidate uses the deterministic narrative built from persisted scores and interpretive results. All candidates stay fully scored and ranked by the POLOXI competition; only presentation output tokens and composer shards shrink. 0 = all candidates get the LLM narrative. Clamped 0..100.',
		   0,0,@Now,0
	 WHERE NOT EXISTS(SELECT 1 FROM Core.ConfigurationSetting c WHERE c.SettingKey=N'Intelligence.SearchWide.LandscapeFullNarrativeTopN' AND c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0);
END;

COMMIT TRANSACTION;
