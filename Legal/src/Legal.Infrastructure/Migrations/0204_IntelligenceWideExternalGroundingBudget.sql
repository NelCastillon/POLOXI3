-- Total wall-clock budget for the Wide external grounding phase.
-- ExternalGroundingBudgetSeconds (default 120): the whole GatherExternalKnowledge phase
-- (all branches x all sources) is bounded by this budget. When it elapses, in-flight
-- retrievals are cancelled fail-soft and the pipeline proceeds with the evidence already
-- gathered. Grounding is advisory; per-call TimeoutSeconds still applies to each source,
-- but without this cap many slow-but-not-failing external calls could stack into a
-- multi-minute stall between LLM stages (observed: ~25 minutes on slow government APIs).
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.ExternalGroundingBudgetSeconds',N'120',N'Integer',N'Total wall-clock budget in seconds for the Wide external grounding phase (all branches and sources combined). When elapsed, in-flight retrievals are cancelled fail-soft and the run proceeds with the evidence already gathered. 0 disables the budget.');

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
END;
