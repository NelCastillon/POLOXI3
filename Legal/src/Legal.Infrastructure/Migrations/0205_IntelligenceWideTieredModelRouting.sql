-- Tiered model routing for the Wide pipeline (highest-leverage latency lever).
-- EnableTieredModelRouting (default true): when on, mechanical strict-JSON stages
-- (intent, hierarchy step, query contract, candidate enumeration, legal-authority
-- proposal, information value, challenge round, candidate matrix, ABV) always run on
-- FastModelCode regardless of the requested model, so selecting a reasoning model
-- (e.g. Astra) only pays reasoning latency on the user-facing synthesis calls instead
-- of on the ~9 mechanical calls per run. Their output is a bounded schema a fast model
-- produces reliably, and every seed/score still faces the deterministic filters and
-- evidence gates downstream, so answer quality is unchanged. When off, every stage
-- routes through the requested model (legacy behavior).
-- FastModelCode (default gpt-4.1-mini): the fast-tier CHAT deployment used by mechanical
-- stages. Must have active feature routes for the wide mechanical features (it is also
-- the Auto default, so those routes already exist).
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableTieredModelRouting',N'true',N'Boolean',N'When true, mechanical Wide stages (intent, hierarchy step, query contract, candidate enumeration, legal-authority proposal, information value, challenge round, candidate matrix, ABV) always run on FastModelCode regardless of the requested model; only user-facing synthesis calls use the requested (reasoning) model. When false, every stage routes through the requested model.'),
		(N'Intelligence.SearchWide.FastModelCode',N'gpt-4.1-mini',N'String',N'Fast-tier CHAT deployment ModelCode used by mechanical Wide stages when tiered model routing is enabled. Must have active feature routes for the wide mechanical features (this is also the Auto default).');

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
