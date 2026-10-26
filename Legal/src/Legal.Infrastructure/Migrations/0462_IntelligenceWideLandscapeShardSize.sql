SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0462: Wide2 Candidate Landscape sharding dial.
--   Stage telemetry (POLOXI.Legal_WideRunStageTiming) showed the SYNTHESIS phase is floored by ONE
--   Candidate Landscape composer call that emits ~20-23K output tokens on the reasoning model
--   (~105-138 s per AI.Legal_Execution). Output generation is linear in tokens, so no scheduling of
--   the surrounding calls can beat that single-call floor.
--   The per-candidate narrative blocks are independent of one another (each reads only the already
--   scored candidate set), so the composer may be split into concurrent slices of candidates using the
--   SAME model, system prompt, full-set context and schema. Content is unchanged: every candidate still
--   receives its full analysis; only the serial output length per call shrinks.
--   LandscapeShardSize = candidates per concurrent composer call. 0 (or >= candidate count) keeps the
--   original single-call behaviour. Idempotent and additive; tenant overrides untouched.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL, Value NVARCHAR(100) NOT NULL, DataTypeCode NVARCHAR(40) NOT NULL, Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,Value,DataTypeCode,Description) VALUES
	(N'Intelligence.SearchWide.LandscapeShardSize',N'6',N'Integer',
	 N'Wide2 Candidate Landscape composer: number of candidates per concurrent composer call. The authoritative candidate set is partitioned (rank order preserved) into slices of this size and each slice is composed with the same model, system prompt, full-set context and schema, then merged. Reduces SYNTHESIS wall-clock without changing narrative content. 0 or a value at/above the candidate count means a single call (original behaviour).');

	UPDATE c SET c.DefaultValue=s.Value, c.Description=s.Description, c.ModifiedDateUtc=@Now
	  FROM Core.ConfigurationSetting c JOIN @Settings s ON s.SettingKey=c.SettingKey
	 WHERE c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0;

	INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),NULL,N'Platform',N'Intelligence',s.SettingKey,s.Value,s.Value,s.DataTypeCode,s.Description,0,0,@Now,0
	  FROM @Settings s
	 WHERE NOT EXISTS(SELECT 1 FROM Core.ConfigurationSetting c WHERE c.SettingKey=s.SettingKey AND c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0);
END;

COMMIT TRANSACTION;
