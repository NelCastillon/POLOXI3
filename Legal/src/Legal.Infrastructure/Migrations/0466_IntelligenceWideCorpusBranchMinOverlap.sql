-- Configurable corpus proposition -> branch attachment gate for the wide POLOXI pipeline.
-- Root cause fixed: WideCorpusEvidenceProjection credited a branch whenever an uploaded-document
-- proposition shared ANY single significant token with the branch (the gate was a hardcoded ">0").
-- That caused false positives from incidental token overlap (one shared word mis-crediting an
-- unrelated branch) with no way to tune the sensitivity. This setting replaces the hardcoded gate
-- with a DB-seeded minimum overlap ratio: a proposition is only credited to a branch when it shares
-- at least this fraction of the branch's significant tokens.
--   CorpusBranchMinOverlap (default .34) - minimum fraction of a branch's significant tokens that
--                                          must also appear in a corpus proposition before the
--                                          proposition is attached to (and scores) that branch.
-- This never performs new legal reasoning and never lowers the admission gates - it only tightens
-- the deterministic lexical attachment between corpus propositions and branches.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.CorpusBranchMinOverlap',N'0.34',N'Decimal',N'Minimum fraction of a branch''s significant tokens that must also appear in a matter-corpus proposition before the proposition is attached to and scores that branch. Replaces the previous hardcoded any-shared-token gate. Range 0..1.');

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
