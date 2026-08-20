SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- POLOXI V2.3: Semantic branch types + entropy re-aiming.
-- ALTERNATIVE branches are mutually exclusive competing interpretations and
-- participate in Shannon entropy. DIMENSION branches are jointly valid
-- criteria (importance/coverage-scored) and are EXCLUDED from winner-take-all
-- entropy. When too few ALTERNATIVE branches exist, entropy is measured over
-- the deterministic candidate-signal distribution (CANDIDATE basis) instead,
-- so Information Gain targets "which candidate wins", not "which dimension
-- wins". Also adds evidence-priority expansion-guard and candidate-admission
-- configuration.
-- ============================================================================

-- ── POLOXI.WideBranch: semantic type (ALTERNATIVE | DIMENSION) ──
IF OBJECT_ID(N'POLOXI.WideBranch',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.WideBranch',N'SemanticTypeCode') IS NULL
		ALTER TABLE POLOXI.WideBranch ADD SemanticTypeCode NVARCHAR(20) NOT NULL CONSTRAINT DF_PoloxiWideBranch_SemanticType DEFAULT N'ALTERNATIVE';
END;

-- ── POLOXI.WideInformationRound: which distribution entropy was measured over ──
IF OBJECT_ID(N'POLOXI.WideInformationRound',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.WideInformationRound',N'EntropyBasisCode') IS NULL
		ALTER TABLE POLOXI.WideInformationRound ADD EntropyBasisCode NVARCHAR(20) NOT NULL CONSTRAINT DF_PoloxiWideInfoRound_EntropyBasis DEFAULT N'BRANCH';
END;

-- ── POLOXI.WideExecution: execution-level entropy basis ──
IF OBJECT_ID(N'POLOXI.WideExecution',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.WideExecution',N'EntropyBasisCode') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD EntropyBasisCode NVARCHAR(20) NULL;
END;

-- ── V2.3 configuration settings (DB is the source of truth) ──
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
		(N'Intelligence.SearchWide.EvidencePriorityMinimumDepth',N'4',N'Integer',N'Depth at or beyond which the evidence-priority expansion guard is evaluated: when semantic understanding is adequate but evidence coverage is the actual weakness, POLOXI stops generating deeper hierarchy and investigates instead.'),
		(N'Intelligence.SearchWide.EvidencePriorityCoverageFloor',N'0.35',N'Decimal',N'Share of surviving branches with any evidence support below which (at/after the minimum depth) hierarchy expansion stops with EVIDENCE_COVERAGE_PRIORITY so retrieval effort goes to distinguishing candidates.'),
		(N'Intelligence.SearchWide.MinimumCandidateDimensionSupport',N'2',N'Integer',N'Minimum number of distinct interpretive dimensions a candidate must appear in to be admitted into the candidate competition. Single-dimension appearances (for example an affordability-only list) are flagged as constraint-style exclusions, never silently dropped.');

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

COMMIT TRANSACTION;
