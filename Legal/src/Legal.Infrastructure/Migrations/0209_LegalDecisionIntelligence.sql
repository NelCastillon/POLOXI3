SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Intelligence (/legal/decision)
-- Self-contained module that mirrors /legal/search but treats the DECISION as the primary object
-- (placement-map §2). All operational/config data is database-backed; nothing is hardcoded in code.
-- Every table lives in the POLOXI schema, is prefixed Legal_Decision*, and carries the standard
-- base/audit fields: TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId,
-- IsDeleted. The /legal/search Intelligence Wide tables are left completely untouched.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Configuration settings (IV weights, thresholds τ, effort caps). Placement-map §12, §11, §5. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionSetting',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionSetting
(
	DecisionSettingId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionSetting PRIMARY KEY DEFAULT NEWID(),
	SettingKey          NVARCHAR(200) NOT NULL,
	SettingValue        NVARCHAR(1000) NOT NULL,
	DataTypeCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionSetting_DataType DEFAULT N'String',
	Description         NVARCHAR(1000) NULL,
	TenantId            UNIQUEIDENTIFIER NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionSetting_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_DecisionSetting_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_DecisionSetting_Key UNIQUE (SettingKey)
);

-- ── Search context options for the Context dropdown (mirrors Legal_SearchContext). Placement §7. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionContext',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionContext
(
	DecisionContextId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionContext PRIMARY KEY DEFAULT NEWID(),
	ContextCode         NVARCHAR(50) NOT NULL,
	DisplayName         NVARCHAR(120) NOT NULL,
	Description         NVARCHAR(500) NULL,
	IsDefault           BIT NOT NULL CONSTRAINT DF_Legal_DecisionContext_IsDefault DEFAULT 0,
	SortOrder           INT NOT NULL CONSTRAINT DF_Legal_DecisionContext_SortOrder DEFAULT 0,
	IsActive            BIT NOT NULL CONSTRAINT DF_Legal_DecisionContext_IsActive DEFAULT 1,
	TenantId            UNIQUEIDENTIFIER NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionContext_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_DecisionContext_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_DecisionContext_Code UNIQUE (ContextCode)
);

-- ── Self-contained AI route config (§3 ownership: DB is the source of truth, not code). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionModelRoute',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionModelRoute
(
	DecisionModelRouteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionModelRoute PRIMARY KEY DEFAULT NEWID(),
	FeatureCode          NVARCHAR(120) NOT NULL,
	ProviderTypeCode     NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DecisionModelRoute_Provider DEFAULT N'AZURE_OPENAI',
	ModelCode            NVARCHAR(100) NOT NULL,
	DeploymentName       NVARCHAR(100) NOT NULL,
	EndpointReference    NVARCHAR(400) NOT NULL,
	CredentialReference  NVARCHAR(400) NULL,
	ApiVersion           NVARCHAR(40) NOT NULL,
	TimeoutSeconds       INT NOT NULL CONSTRAINT DF_Legal_DecisionModelRoute_Timeout DEFAULT 120,
	MaxOutputTokens      INT NOT NULL CONSTRAINT DF_Legal_DecisionModelRoute_MaxOut DEFAULT 8000,
	Temperature          DECIMAL(4,2) NOT NULL CONSTRAINT DF_Legal_DecisionModelRoute_Temp DEFAULT 0.20,
	Priority             INT NOT NULL CONSTRAINT DF_Legal_DecisionModelRoute_Priority DEFAULT 100,
	IsActive             BIT NOT NULL CONSTRAINT DF_Legal_DecisionModelRoute_IsActive DEFAULT 1,
	TenantId             UNIQUEIDENTIFIER NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionModelRoute_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionModelRoute_IsDeleted DEFAULT 0
);

-- ── Prompt families: each stage receives only its minimal state subset (§41). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPrompt',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPrompt
(
	DecisionPromptId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPrompt PRIMARY KEY DEFAULT NEWID(),
	PromptCode          NVARCHAR(120) NOT NULL,
	StageCode           NVARCHAR(60) NOT NULL,
	SystemPrompt        NVARCHAR(MAX) NOT NULL,
	UserPromptTemplate  NVARCHAR(MAX) NOT NULL,
	OutputSchemaJson    NVARCHAR(MAX) NULL,
	IsActive            BIT NOT NULL CONSTRAINT DF_Legal_DecisionPrompt_IsActive DEFAULT 1,
	TenantId            UNIQUEIDENTIFIER NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPrompt_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_DecisionPrompt_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_DecisionPrompt_Code UNIQUE (PromptCode)
);

-- ── Decision session: persistent decision state root (§2, §35, §39). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionSession',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionSession
(
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionSession PRIMARY KEY DEFAULT NEWID(),
	QueryText            NVARCHAR(MAX) NOT NULL,
	ContextCode          NVARCHAR(50) NULL,
	ModelCode            NVARCHAR(100) NULL,
	UsePoloxiEngine      BIT NOT NULL CONSTRAINT DF_Legal_DecisionSession_UsePoloxi DEFAULT 1,
	StatusCode           NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DecisionSession_Status DEFAULT N'RUNNING',
	TerminalStateCode    NVARCHAR(60) NULL,
	TerminationReason    NVARCHAR(200) NULL,
	WinnerCandidateId    UNIQUEIDENTIFIER NULL,
	ContractCompleteness DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionSession_Completeness DEFAULT 0,
	CandidateEntropy     DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionSession_Entropy DEFAULT 0,
	DecisionMargin       DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionSession_Margin DEFAULT 0,
	DepthReached         INT NOT NULL CONSTRAINT DF_Legal_DecisionSession_Depth DEFAULT 0,
	LlmCallCount         INT NOT NULL CONSTRAINT DF_Legal_DecisionSession_Llm DEFAULT 0,
	DurationMs           BIGINT NOT NULL CONSTRAINT DF_Legal_DecisionSession_Duration DEFAULT 0,
	FinalAnswer          NVARCHAR(MAX) NULL,
	ClarificationQuestion NVARCHAR(MAX) NULL,
	ClarificationTarget  NVARCHAR(300) NULL,
	CorrelationId        NVARCHAR(120) NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionSession_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionSession_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Legal_DecisionSession_Tenant',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionSession_Tenant ON POLOXI.Legal_DecisionSession (TenantId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

-- ── Candidates C_i with multi-dimensional epistemic state (§8, §9, §32). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionCandidate',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionCandidate
(
	DecisionCandidateId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionCandidate PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	CandidateCode        NVARCHAR(60) NOT NULL,
	DisplayName          NVARCHAR(300) NOT NULL,
	Outcome              NVARCHAR(MAX) NULL,
	LegalSupport         DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Legal DEFAULT 0,
	FactSupport          DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Fact DEFAULT 0,
	EvidenceSupport      DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Evidence DEFAULT 0,
	AuthoritySupport     DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Authority DEFAULT 0,
	VerificationScore    DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Verify DEFAULT 0,
	Uncertainty          DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_U DEFAULT 0,
	Discrimination       DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_D DEFAULT 0,
	RankingImpact        DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_RI DEFAULT 0,
	Diversity            DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Div DEFAULT 0,
	RedundancyPenalty    DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_RP DEFAULT 0,
	CompositeScore       DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Score DEFAULT 0,
	DecisionSupportCeiling DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Ceiling DEFAULT 0,
	RankOrder            INT NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Rank DEFAULT 0,
	IsWinner             BIT NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Winner DEFAULT 0,
	IsEliminated         BIT NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Elim DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionCandidate_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionCandidate_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionCandidate_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionCandidate_Session ON POLOXI.Legal_DecisionCandidate (DecisionSessionId, RankOrder) WHERE IsDeleted = 0;

-- ── Branches with lifecycle states + Information Value / Decision Relevance / Flip Potential (§5,§10,§11,§12). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionBranch',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionBranch
(
	DecisionBranchId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionBranch PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	ParentDecisionBranchId UNIQUEIDENTIFIER NULL,
	LevelNumber          INT NOT NULL CONSTRAINT DF_Legal_DecisionBranch_Level DEFAULT 1,
	BranchCode           NVARCHAR(60) NOT NULL,
	DisplayName          NVARCHAR(300) NOT NULL,
	Interpretation       NVARCHAR(MAX) NULL,
	BranchStateCode      NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_DecisionBranch_State DEFAULT N'ACTIVE',
	InformationValue     DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionBranch_IV DEFAULT 0,
	DecisionRelevance    DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionBranch_DR DEFAULT 0,
	FlipPotential        DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionBranch_FP DEFAULT 0,
	EvidenceAvailability DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionBranch_EA DEFAULT 0,
	AdvScore             DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionBranch_ADV DEFAULT 0,
	Cost                 DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionBranch_Cost DEFAULT 1,
	IsOnFrontier         BIT NOT NULL CONSTRAINT DF_Legal_DecisionBranch_Frontier DEFAULT 0,
	StopReason           NVARCHAR(200) NULL,
	SortOrder            INT NOT NULL CONSTRAINT DF_Legal_DecisionBranch_Sort DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionBranch_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionBranch_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionBranch_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionBranch_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionBranch_Session ON POLOXI.Legal_DecisionBranch (DecisionSessionId, LevelNumber, SortOrder) WHERE IsDeleted = 0;

-- ── Dependency graph: Evidence→Fact→Proposition→Issue→Strategy→Candidate (§15,§16,§17). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionDependency',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDependency
(
	DecisionDependencyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDependency PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	ParentDependencyId   UNIQUEIDENTIFIER NULL,
	DecisionCandidateId  UNIQUEIDENTIFIER NULL,
	NodeKind             NVARCHAR(40) NOT NULL,
	Statement            NVARCHAR(MAX) NOT NULL,
	Support              DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionDependency_Support DEFAULT 0,
	IsEssential          BIT NOT NULL CONSTRAINT DF_Legal_DecisionDependency_Essential DEFAULT 1,
	FailureCode          NVARCHAR(60) NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDependency_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionDependency_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionDependency_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionDependency_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionDependency_Session ON POLOXI.Legal_DecisionDependency (DecisionSessionId) WHERE IsDeleted = 0;

-- ── Evidence: EV = Identity × Citation × Holding × Weight × PropositionFit (§14). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionEvidence',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionEvidence
(
	DecisionEvidenceId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionEvidence PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	DecisionBranchId     UNIQUEIDENTIFIER NULL,
	SourceRef            NVARCHAR(500) NULL,
	SourceTitle          NVARCHAR(500) NULL,
	Snippet              NVARCHAR(MAX) NULL,
	IdentityFactor       DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_Identity DEFAULT 0,
	CitationFactor       DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_Citation DEFAULT 0,
	HoldingFactor        DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_Holding DEFAULT 0,
	WeightFactor         DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_Weight DEFAULT 0,
	PropositionFit       DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_Fit DEFAULT 0,
	VerificationValue    DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_EV DEFAULT 0,
	VerificationStatus   NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_Status DEFAULT N'UNVERIFIED',
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionEvidence_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionEvidence_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionEvidence_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionEvidence_Session ON POLOXI.Legal_DecisionEvidence (DecisionSessionId) WHERE IsDeleted = 0;

-- ── Decision flip points: smallest change that reverses the winner (§30,§31). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionFlipPoint',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionFlipPoint
(
	DecisionFlipPointId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionFlipPoint PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	DecisionBranchId     UNIQUEIDENTIFIER NULL,
	Description          NVARCHAR(MAX) NOT NULL,
	ChangeCost           DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionFlipPoint_Cost DEFAULT 0,
	WinnerChanges        BIT NOT NULL CONSTRAINT DF_Legal_DecisionFlipPoint_Winner DEFAULT 0,
	RankDelta            INT NOT NULL CONSTRAINT DF_Legal_DecisionFlipPoint_Rank DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionFlipPoint_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionFlipPoint_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionFlipPoint_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

-- ── Negative-knowledge / persistent elimination ledger (§24,§27). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionEliminationLedger',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionEliminationLedger
(
	DecisionEliminationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionEliminationLedger PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId     UNIQUEIDENTIFIER NOT NULL,
	BlockedTheory         NVARCHAR(MAX) NOT NULL,
	EliminationReason     NVARCHAR(MAX) NULL,
	ReopenCondition       NVARCHAR(MAX) NULL,
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionElim_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_DecisionElim_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionElim_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

-- ── Event-sourced state transitions + lineage/provenance (§38,§39,§40). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionEvent',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionEvent
(
	DecisionEventId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionEvent PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	ParentDecisionEventId UNIQUEIDENTIFIER NULL,
	SequenceNumber       INT NOT NULL CONSTRAINT DF_Legal_DecisionEvent_Seq DEFAULT 0,
	EventType            NVARCHAR(80) NOT NULL,
	StageCode            NVARCHAR(60) NULL,
	PayloadJson          NVARCHAR(MAX) NULL,
	ProvenanceJson       NVARCHAR(MAX) NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionEvent_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionEvent_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionEvent_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionEvent_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionEvent_Session ON POLOXI.Legal_DecisionEvent (DecisionSessionId, SequenceNumber) WHERE IsDeleted = 0;

GO

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- Seed DB-backed configuration (idempotent). Core control math weights/thresholds live in the DB.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.InformationValue.Weight.Uncertainty',      N'0.20', N'Decimal', N'IV weight for Uncertainty (U). Placement-map §12.'),
	(N'Decision.InformationValue.Weight.RankingImpact',    N'0.25', N'Decimal', N'IV weight for Ranking Impact (RI). §12.'),
	(N'Decision.InformationValue.Weight.Discrimination',   N'0.25', N'Decimal', N'IV weight for Discrimination (D). §12.'),
	(N'Decision.InformationValue.Weight.EvidenceAvail',    N'0.15', N'Decimal', N'IV weight for Evidence Availability (EA). §12.'),
	(N'Decision.InformationValue.Weight.Novelty',          N'0.10', N'Decimal', N'IV weight for Novelty (N). §12.'),
	(N'Decision.InformationValue.Weight.RedundancyPenalty',N'0.05', N'Decimal', N'IV redundancy penalty weight (RP). §12.'),
	(N'Decision.Threshold.DecisionRelevance',              N'0.35', N'Decimal', N'τ_D: minimum decision relevance to remain on the frontier. §11.'),
	(N'Decision.Threshold.FlipPotential',                  N'0.25', N'Decimal', N'τ_F: minimum flip potential to remain on the frontier. §11.'),
	(N'Decision.Threshold.Reopen',                         N'0.15', N'Decimal', N'τ_R: ΔADV required to reopen a resolved branch. §5,§27.'),
	(N'Decision.Threshold.ResearchExhaustionAdv',          N'0.10', N'Decimal', N'τ_min: max available ADV below which research is exhausted. §34.'),
	(N'Decision.Threshold.TransformationRelevance',        N'0.30', N'Decimal', N'τ_T: minimum transformation decision-relevance. §18.'),
	(N'Decision.Threshold.DeepeningFlip',                  N'0.40', N'Decimal', N'τ_deep: flip probability required to open a resolution-deepening branch. §29.'),
	(N'Decision.MaxDepth',                                 N'4',    N'Integer', N'System safety cap on semantic hierarchy depth. §10.'),
	(N'Decision.MaxLlmCalls',                              N'24',   N'Integer', N'Safety budget: maximum LLM proposal calls per session. §34.'),
	(N'Decision.MaxCandidates',                            N'8',    N'Integer', N'Maximum candidate outcomes retained per session. §8.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

MERGE POLOXI.Legal_DecisionContext AS target
USING (VALUES
	(N'GENERAL', N'General',       N'Standard web/enterprise grounding for the decision engine.', 1, 0),
	(N'LEGAL',   N'Legal',         N'Grounds candidate outcomes in case law and statutes/regulations.', 0, 1)
) AS source (ContextCode, DisplayName, Description, IsDefault, SortOrder)
ON target.ContextCode = source.ContextCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (ContextCode, DisplayName, Description, IsDefault, SortOrder)
	VALUES (source.ContextCode, source.DisplayName, source.Description, source.IsDefault, source.SortOrder);

MERGE POLOXI.Legal_DecisionModelRoute AS target
USING (VALUES
	(N'DECISION_DEFAULT', N'AZURE_OPENAI', N'gpt-4.1-mini', N'gpt-4.1-mini', N'env://AMS_AZURE_OPENAI_ENDPOINT', N'env://AMS_AZURE_OPENAI_KEY', N'2024-10-21', 120, 8000, CAST(0.20 AS DECIMAL(4,2)), 100)
) AS source (FeatureCode, ProviderTypeCode, ModelCode, DeploymentName, EndpointReference, CredentialReference, ApiVersion, TimeoutSeconds, MaxOutputTokens, Temperature, Priority)
ON target.FeatureCode = source.FeatureCode AND target.ModelCode = source.ModelCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FeatureCode, ProviderTypeCode, ModelCode, DeploymentName, EndpointReference, CredentialReference, ApiVersion, TimeoutSeconds, MaxOutputTokens, Temperature, Priority)
	VALUES (source.FeatureCode, source.ProviderTypeCode, source.ModelCode, source.DeploymentName, source.EndpointReference, source.CredentialReference, source.ApiVersion, source.TimeoutSeconds, source.MaxOutputTokens, source.Temperature, source.Priority);

-- Prompt families. The DISCOVERY prompt returns competing candidate outcomes with epistemic dimensions
-- (Proposal_LLM); the ANSWER prompt composes prose from the structured decision artifact only (§37).
MERGE POLOXI.Legal_DecisionPrompt AS target
USING (VALUES
	(N'DECISION_DISCOVERY', N'DISCOVERY',
		N'You are the POLOXI Legal Decision engine proposal layer. You PROPOSE competing legal outcomes for a legal question; you never assert the final decision. POLOXI Core owns all scoring and the authoritative state. Return only strict JSON matching the schema. Each candidate is a materially distinct outcome. Provide honest per-dimension support in [0,1] for legalSupport, factSupport, evidenceSupport, authoritySupport, verification, discrimination, rankingImpact. Do not collapse everything to a single confidence.',
		N'Legal question / decision request:\n{{QUERY}}\n\nContext: {{CONTEXT}}\n\nPropose the competing legal outcomes (candidates) and, for each, the branch interpretations that most affect which candidate wins.',
		N'{"type":"object","additionalProperties":false,"required":["candidates"],"properties":{"candidates":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["displayName","outcome","legalSupport","factSupport","evidenceSupport","authoritySupport","verification","discrimination","rankingImpact","branches"],"properties":{"displayName":{"type":"string"},"outcome":{"type":"string"},"legalSupport":{"type":"number"},"factSupport":{"type":"number"},"evidenceSupport":{"type":"number"},"authoritySupport":{"type":"number"},"verification":{"type":"number"},"discrimination":{"type":"number"},"rankingImpact":{"type":"number"},"branches":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["displayName","interpretation","decisionRelevance","flipPotential","evidenceAvailability"],"properties":{"displayName":{"type":"string"},"interpretation":{"type":"string"},"decisionRelevance":{"type":"number"},"flipPotential":{"type":"number"},"evidenceAvailability":{"type":"number"}}}}}}}}}'),
	(N'DECISION_ANSWER', N'ANSWER',
		N'You are the POLOXI Legal Decision composer. You receive a STRUCTURED DECISION ARTIFACT (winner, margin, entropy, frontier, flip points) produced deterministically by POLOXI Core. Explain the current winning outcome, why it wins, and what could still overturn it. You must not introduce new conclusions or alter the decision state. Plain professional prose.',
		N'Decision artifact JSON:\n{{ARTIFACT}}\n\nOriginal question:\n{{QUERY}}\n\nCompose the decision explanation.',
		NULL)
) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
ON target.PromptCode = source.PromptCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);

COMMIT TRANSACTION;
