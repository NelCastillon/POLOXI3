-- V3.4: Externalize evidence-support calibration constants (previously hardcoded in
-- IntelligenceWideService.ComputeEvidenceSupport) into DB-backed platform settings.
-- Defaults preserve the exact prior behavior: enterprise = min(.9, .5 + .2*(count-1));
-- external = maxScore * min(1, .6 + .1*matchedCount).
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnterpriseSupportBase',N'0.50',N'Decimal',N'Evidence support contributed by the first matching enterprise evidence item for a branch.'),
		(N'Intelligence.SearchWide.EnterpriseSupportIncrement',N'0.20',N'Decimal',N'Additional evidence support contributed by each enterprise evidence item after the first.'),
		(N'Intelligence.SearchWide.EnterpriseSupportCeiling',N'0.90',N'Decimal',N'Maximum evidence support attainable from enterprise evidence alone (saturation ceiling).'),
		(N'Intelligence.SearchWide.ExternalSupportBase',N'0.60',N'Decimal',N'Base multiplier applied to the best external snippet relevance score when one snippet matches a branch.'),
		(N'Intelligence.SearchWide.ExternalSupportIncrement',N'0.10',N'Decimal',N'Additional multiplier per matched external snippet (capped at 1.0) applied to the best snippet relevance score.');

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
			source.SettingValue,source.DataTypeCode,source.Description,0,0,
			SYSUTCDATETIME(),0
		);
END;
