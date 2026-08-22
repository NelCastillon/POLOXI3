-- V3.2 Answer-Kind-Aware Workflow Routing.
-- The Stage 0 query contract classifies AnswerKind (ENTITY_RANKING / CONTENT_ENUMERATION /
-- SINGLE_ANSWER). V3.2 makes the pipeline commit to that classification:
--   1. POLOXI.WideExecution.AnswerKindCode persists the governing classification for audit
--      and misclassification measurement.
--   2. Kind-specific budget settings tune (not fork) the single pipeline: depth ceiling and
--      information-round caps per kind. A value of 0 for a depth setting means "use the full
--      default"; round settings are absolute caps. NULL/unknown kinds always run the full
--      pipeline (fail-safe toward thoroughness, never toward speed).
IF OBJECT_ID(N'POLOXI.WideExecution',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.WideExecution',N'AnswerKindCode') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD AnswerKindCode NVARCHAR(30) NULL;
END;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE
	(
		SettingKey NVARCHAR(200) NOT NULL,
		SettingValue NVARCHAR(2000) NOT NULL,
		DataTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(1000) NOT NULL
	);

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableAnswerKindRouting',N'true',N'Boolean',N'Enables V3.2 answer-kind-aware workflow budgets. When disabled, all kinds run the full pipeline.'),
		(N'Intelligence.SearchWide.ContentEnumerationDepthCeiling',N'2',N'Integer',N'Maximum hierarchy depth for CONTENT_ENUMERATION executions. 0 = use the full default depth ceiling.'),
		(N'Intelligence.SearchWide.ContentEnumerationMaxInformationRounds',N'1',N'Integer',N'Maximum information rounds for CONTENT_ENUMERATION executions (candidate discrimination gains ~0 bits for content queries).'),
		(N'Intelligence.SearchWide.SingleAnswerDepthCeiling',N'2',N'Integer',N'Maximum hierarchy depth for SINGLE_ANSWER executions. 0 = use the full default depth ceiling.'),
		(N'Intelligence.SearchWide.SingleAnswerMaxInformationRounds',N'0',N'Integer',N'Maximum information rounds for SINGLE_ANSWER executions (a direct factual answer needs grounding, not candidate discrimination).');

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
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,
			source.SettingValue,source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0
		);
END;
