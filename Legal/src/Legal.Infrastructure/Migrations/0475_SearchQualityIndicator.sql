SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0475: Enterprise Search Quality Indicator for /legal/search.
--
-- Scope: the Wide pipeline (IntelligenceWideService / WideSearchResponse /
-- POLOXI.Legal_WideSearchRunResult). Adds three DB-backed configuration tables
-- that drive a per-result quality assessment (A-E grade + composite score +
-- factor breakdown) and model-appropriateness suggestions, plus persisted
-- quality columns on the run-result capture so Search History reflects the
-- quality computed at run time. No hardcoded rubric/recommendations in code:
-- the scorer reads these rows (Table -> repository -> service -> response).
--
-- Platform rows are tenant-null global defaults and are overridable per tenant,
-- matching POLOXI.Legal_SearchContext / Legal_SearchExamplePrompt conventions.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

-- ---------------------------------------------------------------------------
-- 1) Quality factors: each row is one weighted dimension of the composite score.
--    MetricCode names the WideSearchResponse signal the scorer reads; the scorer
--    normalizes the raw signal into 0-1 using MinValue/MaxValue (HigherIsBetter
--    controls direction), multiplies by Weight, and sums to the composite.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_SearchQualityFactor',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_SearchQualityFactor
	(
		SearchQualityFactorId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_SearchQualityFactor PRIMARY KEY DEFAULT NEWID(),
		TenantId              UNIQUEIDENTIFIER NULL,
		FactorCode            NVARCHAR(60) NOT NULL,
		DisplayName          NVARCHAR(120) NOT NULL,
		Description          NVARCHAR(400) NULL,
		MetricCode           NVARCHAR(80) NOT NULL,
		Weight               DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_SearchQualityFactor_Weight DEFAULT 0,
		MinValue             DECIMAL(18,6) NOT NULL CONSTRAINT DF_Legal_SearchQualityFactor_Min DEFAULT 0,
		MaxValue             DECIMAL(18,6) NOT NULL CONSTRAINT DF_Legal_SearchQualityFactor_Max DEFAULT 1,
		HigherIsBetter       BIT NOT NULL CONSTRAINT DF_Legal_SearchQualityFactor_Higher DEFAULT 1,
		IsActive             BIT NOT NULL CONSTRAINT DF_Legal_SearchQualityFactor_IsActive DEFAULT 1,
		SortOrder            INT NOT NULL CONSTRAINT DF_Legal_SearchQualityFactor_Sort DEFAULT 0,
		CreatedDateUtc       DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_SearchQualityFactor_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId      UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc      DATETIME2(3) NULL,
		ModifiedByUserId     UNIQUEIDENTIFIER NULL,
		IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_SearchQualityFactor_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_SearchQualityFactor_TenantCode ON POLOXI.Legal_SearchQualityFactor(TenantId,FactorCode) WHERE IsDeleted=0;
END;

-- ---------------------------------------------------------------------------
-- 2) Grade bands: map the 0-1 composite score to an enterprise letter grade.
--    MinScore inclusive lower bound; highest matching band wins.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_SearchQualityGradeBand',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_SearchQualityGradeBand
	(
		SearchQualityGradeBandId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_SearchQualityGradeBand PRIMARY KEY DEFAULT NEWID(),
		TenantId        UNIQUEIDENTIFIER NULL,
		Grade           NVARCHAR(4) NOT NULL,
		Label           NVARCHAR(60) NOT NULL,
		MinScore        DECIMAL(9,6) NOT NULL,
		ColorHex        NVARCHAR(9) NOT NULL CONSTRAINT DF_Legal_SearchQualityGradeBand_Color DEFAULT N'#6b7280',
		IsActive        BIT NOT NULL CONSTRAINT DF_Legal_SearchQualityGradeBand_IsActive DEFAULT 1,
		SortOrder       INT NOT NULL CONSTRAINT DF_Legal_SearchQualityGradeBand_Sort DEFAULT 0,
		CreatedDateUtc  DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_SearchQualityGradeBand_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted       BIT NOT NULL CONSTRAINT DF_Legal_SearchQualityGradeBand_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_SearchQualityGradeBand_TenantGrade ON POLOXI.Legal_SearchQualityGradeBand(TenantId,Grade) WHERE IsDeleted=0;
END;

-- ---------------------------------------------------------------------------
-- 3) Model recommendations: advisory guidance mapping a detected search type
--    (answer kind) to the model best suited for it. The scorer compares the
--    run's actual ModelCodeUsed/AnswerKindCode against these rows and, when a
--    better model exists, surfaces a deterministic suggestion.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_SearchModelRecommendation',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_SearchModelRecommendation
	(
		SearchModelRecommendationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_SearchModelRecommendation PRIMARY KEY DEFAULT NEWID(),
		TenantId            UNIQUEIDENTIFIER NULL,
		AnswerKindCode      NVARCHAR(40) NULL,
		ContextCode         NVARCHAR(50) NULL,
		RecommendedModelCode NVARCHAR(100) NOT NULL,
		RecommendedModelLabel NVARCHAR(120) NOT NULL,
		Rationale           NVARCHAR(400) NOT NULL,
		Priority            INT NOT NULL CONSTRAINT DF_Legal_SearchModelRecommendation_Priority DEFAULT 0,
		IsActive            BIT NOT NULL CONSTRAINT DF_Legal_SearchModelRecommendation_IsActive DEFAULT 1,
		CreatedDateUtc      DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_SearchModelRecommendation_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2(3) NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_SearchModelRecommendation_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_SearchModelRecommendation_TenantKey ON POLOXI.Legal_SearchModelRecommendation(TenantId,AnswerKindCode,ContextCode,RecommendedModelCode) WHERE IsDeleted=0;
END;

-- ---------------------------------------------------------------------------
-- 4) Persisted per-run quality columns on the run-result capture so Search
--    History shows the quality computed at run time (not recomputed on read).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_WideSearchRunResult',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_WideSearchRunResult',N'QualityScore') IS NULL
		ALTER TABLE POLOXI.Legal_WideSearchRunResult ADD QualityScore DECIMAL(9,6) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideSearchRunResult',N'QualityGrade') IS NULL
		ALTER TABLE POLOXI.Legal_WideSearchRunResult ADD QualityGrade NVARCHAR(4) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideSearchRunResult',N'QualityFactorsJson') IS NULL
		ALTER TABLE POLOXI.Legal_WideSearchRunResult ADD QualityFactorsJson NVARCHAR(MAX) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_WideSearchRunResult',N'QualitySuggestionsJson') IS NULL
		ALTER TABLE POLOXI.Legal_WideSearchRunResult ADD QualitySuggestionsJson NVARCHAR(MAX) NULL;
END;

COMMIT TRANSACTION;
GO

-- ---------------------------------------------------------------------------
-- 5) Seed platform (tenant-null) factors. Idempotent via MERGE on FactorCode.
-- ---------------------------------------------------------------------------
MERGE POLOXI.Legal_SearchQualityFactor AS target
USING (VALUES
	(N'DECISION_CONFIDENCE', N'Confidence',            N'Combined confidence in the final answer (decision commitment and overall answer confidence).', N'DecisionConfidence', CAST(0.45 AS DECIMAL(9,6)), CAST(0 AS DECIMAL(18,6)), CAST(1 AS DECIMAL(18,6)), 1, 10),
	(N'WINNER_STABILITY',    N'Ranking Stability',     N'How consistently the same leading answer held across reasoning rounds.',N'WinnerStability',   CAST(0.30 AS DECIMAL(9,6)), CAST(0 AS DECIMAL(18,6)), CAST(1 AS DECIMAL(18,6)), 1, 20),
	(N'CANDIDATE_DEPTH',     N'Analytical Depth',       N'Breadth of candidates considered before narrowing.',                 N'CandidateCount',     CAST(0.25 AS DECIMAL(9,6)), CAST(0 AS DECIMAL(18,6)), CAST(8 AS DECIMAL(18,6)), 1, 50)
) AS source(FactorCode,DisplayName,Description,MetricCode,Weight,MinValue,MaxValue,HigherIsBetter,SortOrder)
   ON target.TenantId IS NULL AND target.FactorCode=source.FactorCode AND target.IsDeleted=0
WHEN MATCHED THEN
	UPDATE SET target.DisplayName=source.DisplayName, target.Description=source.Description, target.MetricCode=source.MetricCode,
		target.Weight=source.Weight, target.MinValue=source.MinValue, target.MaxValue=source.MaxValue,
		target.HigherIsBetter=source.HigherIsBetter, target.SortOrder=source.SortOrder, target.IsActive=1,
		target.ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN
	INSERT(SearchQualityFactorId,TenantId,FactorCode,DisplayName,Description,MetricCode,Weight,MinValue,MaxValue,HigherIsBetter,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,source.FactorCode,source.DisplayName,source.Description,source.MetricCode,source.Weight,source.MinValue,source.MaxValue,source.HigherIsBetter,1,source.SortOrder,SYSUTCDATETIME(),0);

-- ---------------------------------------------------------------------------
-- 6) Seed grade bands (A-E). Idempotent via MERGE on Grade.
-- ---------------------------------------------------------------------------
MERGE POLOXI.Legal_SearchQualityGradeBand AS target
USING (VALUES
	(N'A', N'Excellent',  CAST(0.85 AS DECIMAL(9,6)), N'#16a34a', 10),
	(N'B', N'Strong',     CAST(0.70 AS DECIMAL(9,6)), N'#65a30d', 20),
	(N'C', N'Adequate',   CAST(0.50 AS DECIMAL(9,6)), N'#ca8a04', 30),
	(N'D', N'Weak',       CAST(0.30 AS DECIMAL(9,6)), N'#ea580c', 40),
	(N'E', N'Poor',       CAST(0.00 AS DECIMAL(9,6)), N'#dc2626', 50)
) AS source(Grade,Label,MinScore,ColorHex,SortOrder)
   ON target.TenantId IS NULL AND target.Grade=source.Grade AND target.IsDeleted=0
WHEN MATCHED THEN
	UPDATE SET target.Label=source.Label, target.MinScore=source.MinScore, target.ColorHex=source.ColorHex,
		target.SortOrder=source.SortOrder, target.IsActive=1, target.ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN
	INSERT(SearchQualityGradeBandId,TenantId,Grade,Label,MinScore,ColorHex,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,source.Grade,source.Label,source.MinScore,source.ColorHex,1,source.SortOrder,SYSUTCDATETIME(),0);

-- ---------------------------------------------------------------------------
-- 7) Seed model recommendations keyed by detected search type (answer kind).
--    Model codes match the deployments seeded by 0161/0195/0205 (gpt-6-astra,
--    gpt-5.6-sol, gpt-4.1-mini). AnswerKindCode NULL = general default.
-- ---------------------------------------------------------------------------
MERGE POLOXI.Legal_SearchModelRecommendation AS target
USING (VALUES
	(N'ENTITY_RANKING',   N'gpt-6-astra',   N'GPT-6 (Astra)',  N'Ranking and comparison questions benefit from the strongest reasoning model for reliable ordering and tie-breaking.', 10),
	(N'LEGAL_AUTHORITY',  N'gpt-6-astra',   N'GPT-6 (Astra)',  N'Legal-authority questions need the deepest model to reason over statutes, precedent, and citations accurately.',       10),
	(N'INTERPRETIVE',     N'gpt-5.6-sol',   N'GPT-5.6',        N'Open interpretive questions are well served by a balanced high-reasoning model without maximum latency.',             20),
	(N'DECISION',         N'gpt-6-astra',   N'GPT-6 (Astra)',  N'Decision questions require the strongest model to weigh competing factors and commit with high confidence.',          10),
	(NULL,                N'gpt-4.1-mini',  N'GPT-4.1 Mini',   N'For simple factual lookups the fast model gives quick answers at lower cost; escalate only when confidence is low.',   90)
) AS source(AnswerKindCode,RecommendedModelCode,RecommendedModelLabel,Rationale,Priority)
   ON target.TenantId IS NULL
	  AND ISNULL(target.AnswerKindCode,N'~')=ISNULL(source.AnswerKindCode,N'~')
	  AND target.ContextCode IS NULL
	  AND target.RecommendedModelCode=source.RecommendedModelCode
	  AND target.IsDeleted=0
WHEN MATCHED THEN
	UPDATE SET target.RecommendedModelLabel=source.RecommendedModelLabel, target.Rationale=source.Rationale,
		target.Priority=source.Priority, target.IsActive=1, target.ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN
	INSERT(SearchModelRecommendationId,TenantId,AnswerKindCode,ContextCode,RecommendedModelCode,RecommendedModelLabel,Rationale,Priority,IsActive,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,source.AnswerKindCode,NULL,source.RecommendedModelCode,source.RecommendedModelLabel,source.Rationale,source.Priority,1,SYSUTCDATETIME(),0);
GO

