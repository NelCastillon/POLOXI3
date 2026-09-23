-- V3.18 Resolution Deepening (R+1 Decisive Sub-Resolution) for the wide POLOXI pipeline.
-- DB-backed gate + thresholds (all default OFF / current behavior unchanged):
--   EnableResolutionDeepening          (default false) - master gate. When ON, a bounded one-pass
--                                                        deepening runs for EVERY substantive RESOLUTION
--                                                        (it challenges the resolution rather than waiting
--                                                        for the resolution to recognize its own weakness).
--   ResolutionDeepeningMinConditions   (default 2)     - minimum decisive discriminators to surface.
--   ResolutionDeepeningMaxConditions   (default 4)     - cap on discriminators (latency guard).
-- The pass is RESOLUTION-only, single-depth, reuses the existing Candidate x Branch competition, and is
-- fully fail-soft: any failure leaves today's deliverable unchanged. It never changes the winner,
-- ranking, or confidence, and must not be promoted to default-on until A/B benchmarks show lift.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableResolutionDeepening',N'false',N'Boolean',N'V3.18 RESOLUTION-only bounded one-pass deepening (R+1 Decisive Sub-Resolution). Default false = current behavior unchanged. When ON, runs for every substantive RESOLUTION and challenges the resolution by surfacing outcome-determining discriminators; fully fail-soft; never changes the winner or ranking.'),
		(N'Intelligence.SearchWide.ResolutionDeepeningMinConditions',N'2',N'Integer',N'Minimum number of decisive discriminators required to surface a resolution-deepening result.'),
		(N'Intelligence.SearchWide.ResolutionDeepeningMaxConditions',N'4',N'Integer',N'Maximum number of decisive discriminators surfaced per resolution-deepening pass (latency / branch-explosion guard).');

	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL
	  AND target.ScopeCode=N'Platform'
	  AND target.SettingKey=source.SettingKey
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=N'Intelligence',
			target.SettingValue=COALESCE(NULLIF(target.SettingValue,N''),source.SettingValue),
			target.DefaultValue=source.SettingValue,
			target.DataTypeCode=source.DataTypeCode,
			target.Description=source.Description,
			target.IsEncrypted=0,
			target.IsReadOnly=0,
			target.ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,
			DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,
			source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0
		);
END
