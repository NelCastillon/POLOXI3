-- Phase 2a (VNext): Challenge-the-Winner round for the wide POLOXI pipeline (WATCH MODE ONLY).
-- 1) DB-backed gate + threshold: EnableChallengeRound (default false = current behavior unchanged)
--    and ChallengeMarginThreshold (leader-vs-runner-up composite margin below which the challenge fires).
-- 2) POLOXI.WideExecution.ChallengeOutcomeJson persists the audit-only challenge verdict; the
--    challenge never changes the winner, ranking, confidence, or answer in Phase 2a.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableChallengeRound',N'false',N'Boolean',N'Phase 2a watch-only challenge-the-winner round: when the top two candidates are close, an adversarial LLM assessment is recorded for audit. Never changes the winner, ranking, confidence, or answer.'),
		(N'Intelligence.SearchWide.ChallengeMarginThreshold',N'0.10',N'Decimal',N'Leader-vs-runner-up composite-score margin below which the watch-only challenge round fires (0..1).');

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

IF OBJECT_ID(N'POLOXI.WideExecution',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.WideExecution',N'ChallengeOutcomeJson') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD ChallengeOutcomeJson NVARCHAR(MAX) NULL;
END
