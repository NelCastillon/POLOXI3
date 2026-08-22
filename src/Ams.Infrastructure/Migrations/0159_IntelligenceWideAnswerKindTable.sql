-- V3.3 Answer-Kind Lookup Table + Remaining Calibration Dials.
-- POLOXI.AnswerKind replaces the C# answer-kind constants and the five per-kind budget
-- configuration keys as the source of truth for answer-kind routing. Each row defines a
-- Stage 0 classification the pipeline recognizes, its workflow budgets, and whether the
-- deterministic Candidate Competition applies. New kinds (e.g. COMPARISON, YES_NO) can be
-- added by inserting rows - no recompile. Semantics preserved from V3.2:
--   DepthCeiling 0            = use the full default depth ceiling (never expands it)
--   MaxInformationRounds NULL = use the full default information-round cap
--   RunsCandidateCompetition  = 0 routes to interpretive composition (category error otherwise)
-- Global defaults use TenantId NULL; tenant-specific rows (same AnswerKindCode) override.
-- Fail-safe: with no rows the application falls back to the compiled defaults, biased
-- toward the full pipeline (thoroughness over speed).
IF SCHEMA_ID(N'POLOXI') IS NULL EXEC(N'CREATE SCHEMA POLOXI');
IF OBJECT_ID(N'POLOXI.AnswerKind',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.AnswerKind(
		AnswerKindId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_POLOXI_AnswerKind_Id DEFAULT NEWSEQUENTIALID(),
		TenantId UNIQUEIDENTIFIER NULL,
		AnswerKindCode NVARCHAR(30) NOT NULL,
		DisplayName NVARCHAR(100) NOT NULL,
		Description NVARCHAR(500) NULL,
		DepthCeiling INT NOT NULL CONSTRAINT DF_POLOXI_AnswerKind_Depth DEFAULT 0,
		MaxInformationRounds INT NULL,
		RunsCandidateCompetition BIT NOT NULL CONSTRAINT DF_POLOXI_AnswerKind_Competition DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_AnswerKind_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_POLOXI_AnswerKind_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AnswerKind_Deleted DEFAULT 0,
		CONSTRAINT PK_POLOXI_AnswerKind PRIMARY KEY CLUSTERED(AnswerKindId)
	);
	CREATE UNIQUE INDEX UX_POLOXI_AnswerKind_Tenant_Code ON POLOXI.AnswerKind(AnswerKindCode,TenantId) WHERE IsDeleted=0;
END;

-- Seed the three V3.1/V3.2 kinds as global defaults (idempotent).
MERGE POLOXI.AnswerKind AS target
USING(VALUES
	(N'ENTITY_RANKING',N'Entity Ranking',N'Rank/compare/list named, independently verifiable entities. Full pipeline: full budgets and the deterministic Candidate Competition.',0,NULL,CAST(1 AS BIT),1),
	(N'CONTENT_ENUMERATION',N'Content Enumeration',N'Requested items are pieces of content to produce or compile (questions, tips, steps, examples). Tuned budgets; Candidate Competition is a category error.',2,1,CAST(0 AS BIT),2),
	(N'SINGLE_ANSWER',N'Single Answer',N'A single factual/definitional answer. Tuned budgets; no candidate competition.',2,0,CAST(0 AS BIT),3)
)AS source(AnswerKindCode,DisplayName,Description,DepthCeiling,MaxInformationRounds,RunsCandidateCompetition,SortOrder)
ON target.AnswerKindCode=source.AnswerKindCode AND target.TenantId IS NULL AND target.IsDeleted=0
WHEN NOT MATCHED THEN INSERT(TenantId,AnswerKindCode,DisplayName,Description,DepthCeiling,MaxInformationRounds,RunsCandidateCompetition,SortOrder)
VALUES(NULL,source.AnswerKindCode,source.DisplayName,source.Description,source.DepthCeiling,source.MaxInformationRounds,source.RunsCandidateCompetition,source.SortOrder);

-- Seed the last hardcoded calibration dial: the V2.8.5 answer->candidate reweight boost.
-- Candidates whose name/detail tokens overlap the user's clarification answer get
-- CompositeScore * (1 + Boost * overlap). Previously the 0.35 factor was compiled in.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	MERGE Core.ConfigurationSetting AS target
	USING(VALUES(
		N'Intelligence.SearchWide.ClarificationReweightBoost',N'0.35',N'Decimal',
		N'Deterministic boost factor applied to candidate composite scores by clarification-answer token overlap: score * (1 + boost * overlap). V3.3: moved from a compiled constant to a seeded dial.'
	))AS source(SettingKey,SettingValue,DataTypeCode,Description)
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
