-- Optional embedding-based corpus proposition -> branch attachment for the wide POLOXI pipeline.
-- Extends migration 0466 (lexical CorpusBranchMinOverlap gate) with an OPTIONAL semantic OR path:
-- a matter-corpus proposition can also attach to a branch when the cosine similarity of their
-- embeddings clears a DB-seeded threshold, catching paraphrased propositions that the deterministic
-- token-overlap gate misses. This is OFF by default and intentionally metered - it only issues
-- embedding calls through the governed AI provider router when a tenant explicitly enables it, and
-- ANY failure (disabled flag, embedding error, dimension mismatch) degrades silently to lexical-only.
-- It never performs new legal reasoning and never lowers the admission gates - semantic similarity is
-- only an additional, tunable lexical-equivalent attachment signal.
--   EnableCorpusSemanticAttachment      (default false) - master opt-in switch for the semantic path.
--   CorpusBranchMinSemanticSimilarity   (default .62)   - minimum normalized cosine similarity [0..1]
--                                                         between a proposition and a branch before
--                                                         the proposition is attached to that branch.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableCorpusSemanticAttachment',N'false',N'Boolean',N'Master opt-in switch for optional embedding-based corpus proposition to branch attachment in the wide POLOXI pipeline. When false (default) only the deterministic lexical overlap gate applies. When true the pipeline also attaches a proposition to a branch when their embedding cosine similarity clears CorpusBranchMinSemanticSimilarity. Fail-soft: any embedding failure falls back to lexical-only.'),
		(N'Intelligence.SearchWide.CorpusBranchMinSemanticSimilarity',N'0.62',N'Decimal',N'Minimum normalized cosine similarity (0..1) between a matter-corpus proposition and a branch before the proposition is attached to and scores that branch via the optional semantic path. Only applies when EnableCorpusSemanticAttachment is true. Range 0..1.');

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
