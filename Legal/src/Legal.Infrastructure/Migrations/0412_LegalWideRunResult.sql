SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Judz Run & Assessment Management — full POLOXI Wide (Decision2) result capture
-- Persists the ENTIRE WideSearchResponse for every matter-scoped Wide run so Analysis History can
-- reopen the exact Decision2 Display (all tabs/sections) read-only. One header row per run plus one
-- normalized child table per display collection. Deeply-nested presentation composer singletons
-- (AnswerContext, LegalAnswer, ResolutionDeliverable, CandidateLandscape, QueryContract,
-- DecisionInspector, FactorInventory, ProposedLegalAuthorities, ExternalKnowledge, ChallengeOutcome,
-- AbvAction, PoloxiComputationDetails, MatterContext, ClarificationOptionItems, LlmRawItems, etc.)
-- are captured without loss in DetailJson on the header. All objects: POLOXI schema, Legal_WideRun*
-- prefix, standard base/audit fields, FK to Legal_AnalysisRun. Write-once from controlled paths.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Header: all top-level scalar fields of WideSearchResponse ─────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunResult',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunResult
(
	WideRunResultId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunResult PRIMARY KEY DEFAULT NEWID(),
	AnalysisRunId            UNIQUEIDENTIFIER NOT NULL,
	MatterId                 UNIQUEIDENTIFIER NOT NULL,
	WideExecutionId          UNIQUEIDENTIFIER NOT NULL,
	Query                    NVARCHAR(MAX) NULL,
	StatusCode               NVARCHAR(60) NULL,
	TerminationReasonCode    NVARCHAR(80) NULL,
	DepthReached             INT NOT NULL CONSTRAINT DF_Legal_WideRunResult_Depth DEFAULT 0,
	LlmCallCount             INT NOT NULL CONSTRAINT DF_Legal_WideRunResult_Llm DEFAULT 0,
	FinalConfidence          DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_WideRunResult_FinalConf DEFAULT 0,
	AnswerVerificationCode   NVARCHAR(60) NULL,
	FinalAnswer              NVARCHAR(MAX) NULL,
	DurationMilliseconds     BIGINT NOT NULL CONSTRAINT DF_Legal_WideRunResult_Duration DEFAULT 0,
	AnswerKindCode           NVARCHAR(40) NULL,
	AnswerKindRoutingApplied BIT NOT NULL CONSTRAINT DF_Legal_WideRunResult_KindRouting DEFAULT 0,
	ProviderCodeUsed         NVARCHAR(100) NULL,
	ModelCodeUsed            NVARCHAR(100) NULL,
	EvidenceCoverage         DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_WideRunResult_EvCov DEFAULT 0,
	DecisionEvidenceCoverage DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_WideRunResult_DecEvCov DEFAULT 0,
	ExternalEvidenceCount    INT NOT NULL CONSTRAINT DF_Legal_WideRunResult_ExtEvCnt DEFAULT 0,
	EnterpriseEvidenceCount  INT NOT NULL CONSTRAINT DF_Legal_WideRunResult_EntEvCnt DEFAULT 0,
	InitialEntropy           DECIMAL(12,6) NOT NULL CONSTRAINT DF_Legal_WideRunResult_IniEnt DEFAULT 0,
	FinalEntropy             DECIMAL(12,6) NOT NULL CONSTRAINT DF_Legal_WideRunResult_FinEnt DEFAULT 0,
	InitialNormalizedEntropy DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_WideRunResult_IniNorm DEFAULT 0,
	FinalNormalizedEntropy   DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_WideRunResult_FinNorm DEFAULT 0,
	TotalActualInformationGain DECIMAL(12,6) NOT NULL CONSTRAINT DF_Legal_WideRunResult_InfoGain DEFAULT 0,
	EntropyBasisCode         NVARCHAR(40) NULL,
	WinnerStability          DECIMAL(9,6) NULL,
	TopKStability            DECIMAL(9,6) NULL,
	DecisionConfidence       DECIMAL(9,6) NULL,
	ClarificationQuestion    NVARCHAR(MAX) NULL,
	ClarificationTarget      NVARCHAR(400) NULL,
	IntentEntropy            DECIMAL(9,6) NULL,
	BestClarificationValue   DECIMAL(9,6) NULL,
	ClarificationGain        DECIMAL(9,6) NULL,
	ClarificationRound       INT NOT NULL CONSTRAINT DF_Legal_WideRunResult_ClarRound DEFAULT 0,
	FinalNarrowingTrend      NVARCHAR(40) NULL,
	-- Complete, loss-free capture of every complex singleton composer object + any field not
	-- promoted to a column above. Reconstructed verbatim for the read-only Decision2 display.
	DetailJson               NVARCHAR(MAX) NULL,
	CapturedUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunResult_Captured DEFAULT SYSUTCDATETIME(),
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunResult_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_WideRunResult_IsDeleted DEFAULT 0,
	RowVersion               ROWVERSION,
	CONSTRAINT FK_Legal_WideRunResult_Run FOREIGN KEY (AnalysisRunId)
		REFERENCES POLOXI.Legal_AnalysisRun (AnalysisRunId)
);

GO

IF OBJECT_ID(N'UX_Legal_WideRunResult_Run',N'UQ') IS NULL
   AND OBJECT_ID(N'POLOXI.Legal_WideRunResult',N'U') IS NOT NULL
	CREATE UNIQUE INDEX UX_Legal_WideRunResult_Run
		ON POLOXI.Legal_WideRunResult (TenantId, AnalysisRunId)
		WHERE IsDeleted = 0;

GO

-- ── Branches ──────────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunBranch',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunBranch
(
	WideRunBranchId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunBranch PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	WideBranchId         UNIQUEIDENTIFIER NOT NULL,
	ParentWideBranchId   UNIQUEIDENTIFIER NULL,
	LevelNumber          INT NOT NULL DEFAULT 0,
	BranchCode           NVARCHAR(100) NULL,
	DisplayName          NVARCHAR(500) NULL,
	Interpretation       NVARCHAR(MAX) NULL,
	CapabilityCode       NVARCHAR(100) NULL,
	SearchText           NVARCHAR(MAX) NULL,
	GroundingStatusCode  NVARCHAR(60) NULL,
	EvidenceCount        INT NOT NULL DEFAULT 0,
	Confidence           DECIMAL(9,6) NOT NULL DEFAULT 0,
	ContinueNarrowing    BIT NOT NULL DEFAULT 0,
	StopReason           NVARCHAR(MAX) NULL,
	IsEliminated         BIT NOT NULL DEFAULT 0,
	EliminationReason    NVARCHAR(MAX) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	BranchStateCode      NVARCHAR(40) NULL,
	SemanticTypeCode     NVARCHAR(40) NULL,
	BranchRoleCode       NVARCHAR(40) NULL,
	InterpretationPrior  DECIMAL(9,6) NOT NULL DEFAULT 0,
	EvidenceSupport      DECIMAL(9,6) NOT NULL DEFAULT 0,
	PoloxiConfidence     DECIMAL(9,6) NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunBranch_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunBranch_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunBranch_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunBranch_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunBranch_Result ON POLOXI.Legal_WideRunBranch (WideRunResultId, SortOrder);

GO

-- ── Evidence ──────────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunEvidence',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunEvidence
(
	WideRunEvidenceId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunEvidence PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	HierarchyBranchId    UNIQUEIDENTIFIER NULL,
	SearchDocumentId     UNIQUEIDENTIFIER NULL,
	EntityTypeCode       NVARCHAR(60) NULL,
	EntityId             UNIQUEIDENTIFIER NULL,
	ModuleCode           NVARCHAR(60) NULL,
	Title                NVARCHAR(MAX) NULL,
	Excerpt              NVARCHAR(MAX) NULL,
	NavigationRoute      NVARCHAR(500) NULL,
	RelevanceScore       DECIMAL(9,6) NOT NULL DEFAULT 0,
	RankNumber           INT NOT NULL DEFAULT 0,
	MatchedBranchesJson  NVARCHAR(MAX) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunEvidence_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunEvidence_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunEvidence_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunEvidence_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunEvidence_Result ON POLOXI.Legal_WideRunEvidence (WideRunResultId, SortOrder);

GO

-- ── Candidates ────────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunCandidate',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunCandidate
(
	WideRunCandidateId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunCandidate PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId          UNIQUEIDENTIFIER NOT NULL,
	WideCandidateId          UNIQUEIDENTIFIER NOT NULL,
	RankNumber               INT NOT NULL DEFAULT 0,
	DisplayName              NVARCHAR(500) NULL,
	Detail                   NVARCHAR(MAX) NULL,
	CompositeScore           DECIMAL(12,6) NOT NULL DEFAULT 0,
	EvidenceCoverage         DECIMAL(9,6) NOT NULL DEFAULT 0,
	IsConstraintViolation    BIT NOT NULL DEFAULT 0,
	QualityScore             DECIMAL(12,6) NOT NULL DEFAULT 0,
	EvidenceConfidence       DECIMAL(9,6) NOT NULL DEFAULT 0,
	AdmissionModeCode        NVARCHAR(40) NULL,
	SupportTierCode          NVARCHAR(40) NULL,
	InterpretiveSupportCount INT NOT NULL DEFAULT 0,
	EvidenceHostSupportCount INT NOT NULL DEFAULT 0,
	TotalSupportCount        INT NOT NULL DEFAULT 0,
	EstimatedResolutionLabel NVARCHAR(200) NULL,
	EstimatedCostBand        NVARCHAR(100) NULL,
	SortOrder                INT NOT NULL DEFAULT 0,
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunCandidate_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_WideRunCandidate_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunCandidate_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunCandidate_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunCandidate_Result ON POLOXI.Legal_WideRunCandidate (WideRunResultId, SortOrder);

GO

-- ── Candidate × Branch scores (incl. roll-up + child scores captured as JSON) ──────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunCandidateBranchScore',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunCandidateBranchScore
(
	WideRunCandidateBranchScoreId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunCandBranchScore PRIMARY KEY DEFAULT NEWID(),
	WideRunCandidateId   UNIQUEIDENTIFIER NOT NULL,
	BranchDisplayName    NVARCHAR(500) NULL,
	EvidenceScore        DECIMAL(12,6) NOT NULL DEFAULT 0,
	DirectScore          DECIMAL(12,6) NULL,
	ChildScoresJson      NVARCHAR(MAX) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunCandBranchScore_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunCandBranchScore_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunCandBranchScore_Candidate FOREIGN KEY (WideRunCandidateId)
		REFERENCES POLOXI.Legal_WideRunCandidate (WideRunCandidateId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunCandBranchScore_Candidate',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunCandBranchScore_Candidate ON POLOXI.Legal_WideRunCandidateBranchScore (WideRunCandidateId, SortOrder);

GO

-- ── Interpretive results + items ──────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunInterpretiveResult',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunInterpretiveResult
(
	WideRunInterpretiveResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunInterpResult PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	BranchDisplayName    NVARCHAR(500) NULL,
	Interpretation       NVARCHAR(MAX) NULL,
	Confidence           DECIMAL(9,6) NOT NULL DEFAULT 0,
	DataVolatility       NVARCHAR(40) NULL,
	IsExternallyGrounded BIT NOT NULL DEFAULT 0,
	BranchStateCode      NVARCHAR(40) NULL,
	LevelNumber          INT NOT NULL DEFAULT 0,
	ScoreSource          NVARCHAR(40) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunInterpResult_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunInterpResult_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunInterpResult_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunInterpResult_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunInterpResult_Result ON POLOXI.Legal_WideRunInterpretiveResult (WideRunResultId, SortOrder);

GO

IF OBJECT_ID(N'POLOXI.Legal_WideRunInterpretiveResultItem',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunInterpretiveResultItem
(
	WideRunInterpretiveResultItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunInterpItem PRIMARY KEY DEFAULT NEWID(),
	WideRunInterpretiveResultId UNIQUEIDENTIFIER NOT NULL,
	RankNumber           INT NOT NULL DEFAULT 0,
	Name                 NVARCHAR(MAX) NULL,
	Detail               NVARCHAR(MAX) NULL,
	Score                DECIMAL(12,6) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunInterpItem_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunInterpItem_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunInterpItem_Parent FOREIGN KEY (WideRunInterpretiveResultId)
		REFERENCES POLOXI.Legal_WideRunInterpretiveResult (WideRunInterpretiveResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunInterpItem_Parent',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunInterpItem_Parent ON POLOXI.Legal_WideRunInterpretiveResultItem (WideRunInterpretiveResultId, SortOrder);

GO

-- ── LLM raw items (single-shot comparison list) ───────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunLlmRawItem',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunLlmRawItem
(
	WideRunLlmRawItemId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunLlmRawItem PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	RankNumber           INT NOT NULL DEFAULT 0,
	Name                 NVARCHAR(MAX) NULL,
	Detail               NVARCHAR(MAX) NULL,
	Score                DECIMAL(12,6) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunLlmRawItem_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunLlmRawItem_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunLlmRawItem_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunLlmRawItem_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunLlmRawItem_Result ON POLOXI.Legal_WideRunLlmRawItem (WideRunResultId, SortOrder);

GO

-- ── External references ───────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunExternalReference',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunExternalReference
(
	WideRunExternalReferenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunExtRef PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	Title                NVARCHAR(MAX) NULL,
	Url                  NVARCHAR(MAX) NULL,
	Source               NVARCHAR(300) NULL,
	Summary              NVARCHAR(MAX) NULL,
	BranchDisplayName    NVARCHAR(500) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunExtRef_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunExtRef_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunExtRef_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunExtRef_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunExtRef_Result ON POLOXI.Legal_WideRunExternalReference (WideRunResultId, SortOrder);

GO

-- ── Proposed legal authorities (retrieval leads, never evidence) ──────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunProposedAuthority',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunProposedAuthority
(
	WideRunProposedAuthorityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunPropAuth PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	Name                 NVARCHAR(MAX) NULL,
	Kind                 NVARCHAR(60) NULL,
	Relevance            NVARCHAR(MAX) NULL,
	VerificationStatus   NVARCHAR(40) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunPropAuth_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunPropAuth_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunPropAuth_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunPropAuth_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunPropAuth_Result ON POLOXI.Legal_WideRunProposedAuthority (WideRunResultId, SortOrder);

GO

-- ── Suggested actions ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunSuggestedAction',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunSuggestedAction
(
	WideRunSuggestedActionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunSuggAction PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	DisplayName          NVARCHAR(500) NULL,
	NavigationRoute      NVARCHAR(500) NULL,
	Rationale            NVARCHAR(MAX) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunSuggAction_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunSuggAction_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunSuggAction_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunSuggAction_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunSuggAction_Result ON POLOXI.Legal_WideRunSuggestedAction (WideRunResultId, SortOrder);

GO

-- ── Stage timings ─────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunStageTiming',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunStageTiming
(
	WideRunStageTimingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunStageTiming PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	StageCode            NVARCHAR(80) NULL,
	DurationMilliseconds BIGINT NOT NULL DEFAULT 0,
	InputTokenCount      INT NOT NULL DEFAULT 0,
	OutputTokenCount     INT NOT NULL DEFAULT 0,
	ModelCode            NVARCHAR(100) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunStageTiming_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunStageTiming_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunStageTiming_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunStageTiming_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunStageTiming_Result ON POLOXI.Legal_WideRunStageTiming (WideRunResultId, SortOrder);

GO

-- ── Information rounds (+ targets captured as JSON) ────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunInformationRound',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunInformationRound
(
	WideRunInformationRoundId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunInfoRound PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	RoundNumber          INT NOT NULL DEFAULT 0,
	EntropyBefore        DECIMAL(12,6) NOT NULL DEFAULT 0,
	NormalizedEntropyBefore DECIMAL(9,6) NOT NULL DEFAULT 0,
	EntropyAfter         DECIMAL(12,6) NULL,
	NormalizedEntropyAfter DECIMAL(9,6) NULL,
	ActualInformationGain DECIMAL(12,6) NULL,
	RawEntropyDelta      DECIMAL(12,6) NULL,
	TargetsJson          NVARCHAR(MAX) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunInfoRound_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunInfoRound_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunInfoRound_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunInfoRound_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunInfoRound_Result ON POLOXI.Legal_WideRunInformationRound (WideRunResultId, SortOrder);

GO

-- ── Narrowing iterations (+ transitions captured as JSON) ──────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunNarrowingIteration',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunNarrowingIteration
(
	WideRunNarrowingIterationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunNarrowIter PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	RoundNumber          INT NOT NULL DEFAULT 0,
	TrendCode            NVARCHAR(40) NULL,
	ActiveBranchCountBefore INT NOT NULL DEFAULT 0,
	ActiveBranchCountAfter  INT NOT NULL DEFAULT 0,
	CandidateCountBefore INT NOT NULL DEFAULT 0,
	CandidateCountAfter  INT NOT NULL DEFAULT 0,
	NormalizedEntropyBefore DECIMAL(9,6) NOT NULL DEFAULT 0,
	NormalizedEntropyAfter  DECIMAL(9,6) NULL,
	ActualInformationGain DECIMAL(12,6) NULL,
	AdmittedCandidateCount INT NOT NULL DEFAULT 0,
	DiscoveredNotAdmittedCount INT NOT NULL DEFAULT 0,
	ResolvedBranchCount  INT NOT NULL DEFAULT 0,
	ReopenedBranchCount  INT NOT NULL DEFAULT 0,
	TransitionsJson      NVARCHAR(MAX) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunNarrowIter_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunNarrowIter_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunNarrowIter_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunNarrowIter_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunNarrowIter_Result ON POLOXI.Legal_WideRunNarrowingIteration (WideRunResultId, SortOrder);

GO

-- ── Ambiguity groups ──────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideRunAmbiguityGroup',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideRunAmbiguityGroup
(
	WideRunAmbiguityGroupId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideRunAmbGroup PRIMARY KEY DEFAULT NEWID(),
	WideRunResultId      UNIQUEIDENTIFIER NOT NULL,
	RootWideBranchId     UNIQUEIDENTIFIER NULL,
	GroupCode            NVARCHAR(100) NULL,
	DisplayName          NVARCHAR(500) NULL,
	Interpretation       NVARCHAR(MAX) NULL,
	Confidence           DECIMAL(9,6) NOT NULL DEFAULT 0,
	SafetyRiskCode       NVARCHAR(60) NULL,
	AnswerKindCode       NVARCHAR(40) NULL,
	CandidateKindCode    NVARCHAR(40) NULL,
	SortOrder            INT NOT NULL DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideRunAmbGroup_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_WideRunAmbGroup_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_WideRunAmbGroup_Result FOREIGN KEY (WideRunResultId)
		REFERENCES POLOXI.Legal_WideRunResult (WideRunResultId)
);

GO

IF OBJECT_ID(N'IX_Legal_WideRunAmbGroup_Result',N'IX') IS NULL
	CREATE INDEX IX_Legal_WideRunAmbGroup_Result ON POLOXI.Legal_WideRunAmbiguityGroup (WideRunResultId, SortOrder);

GO

COMMIT TRANSACTION;
