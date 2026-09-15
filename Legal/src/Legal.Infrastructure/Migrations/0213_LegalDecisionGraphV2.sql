SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal V2 — Dependency-Aware Decision Verification (typed legal evidence/dependency graph)
--
-- Extends the V1 Legal_Decision* graph with first-class, persisted legal reasoning nodes and a
-- single typed edge table so that authority/evidence can only influence a Candidate through a
-- traceable, verifiable chain:
--
--   Authority/Evidence → Fact → LegalProposition → LegalElement → Issue → Strategy → Candidate
--
-- The DECISION remains the primary object; POLOXI Core owns authoritative node/edge state and all
-- scoring. The LLM only proposes graph structure (DECISION_GRAPH) and verification opinions
-- (DECISION_VERIFY); Core persists and propagates. This layer runs only when a session sets the
-- V2 dependency-graph toggle (see 0214). All tables live in the POLOXI schema, are prefixed
-- Legal_Decision*, carry the standard base/audit fields, and are created idempotently.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

-- ── FactProposition: a discrete, verifiable factual assertion (F-nodes). ───────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionFactProposition',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionFactProposition
(
	DecisionFactPropositionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionFactProposition PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId         UNIQUEIDENTIFIER NOT NULL,
	NodeCode                  NVARCHAR(60) NOT NULL,
	Statement                 NVARCHAR(MAX) NOT NULL,
	Support                   DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionFactProp_Support DEFAULT 0,
	IsMaterial                BIT NOT NULL CONSTRAINT DF_Legal_DecisionFactProp_Material DEFAULT 1,
	IsDisputed                BIT NOT NULL CONSTRAINT DF_Legal_DecisionFactProp_Disputed DEFAULT 0,
	VerificationStatus        NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionFactProp_Status DEFAULT N'UNVERIFIED',
	SortOrder                 INT NOT NULL CONSTRAINT DF_Legal_DecisionFactProp_Sort DEFAULT 0,
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionFactProp_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionFactProp_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionFactProp_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionFactProp_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionFactProp_Session ON POLOXI.Legal_DecisionFactProposition (DecisionSessionId) WHERE IsDeleted = 0;

-- ── LegalProposition: a rule/holding assertion drawn from authority (P-nodes). ──────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionLegalProposition',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionLegalProposition
(
	DecisionLegalPropositionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionLegalProp PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId          UNIQUEIDENTIFIER NOT NULL,
	NodeCode                   NVARCHAR(60) NOT NULL,
	Statement                  NVARCHAR(MAX) NOT NULL,
	AuthorityRef               NVARCHAR(500) NULL,
	Support                    DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionLegalProp_Support DEFAULT 0,
	IsMaterial                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionLegalProp_Material DEFAULT 1,
	VerificationStatus         NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionLegalProp_Status DEFAULT N'UNVERIFIED',
	SortOrder                  INT NOT NULL CONSTRAINT DF_Legal_DecisionLegalProp_Sort DEFAULT 0,
	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionLegalProp_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_DecisionLegalProp_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionLegalProp_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionLegalProp_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionLegalProp_Session ON POLOXI.Legal_DecisionLegalProposition (DecisionSessionId) WHERE IsDeleted = 0;

-- ── LegalElement: a required element of a claim/defense that must be satisfied (E-nodes). ───────
IF OBJECT_ID(N'POLOXI.Legal_DecisionLegalElement',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionLegalElement
(
	DecisionLegalElementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionLegalElement PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId      UNIQUEIDENTIFIER NOT NULL,
	NodeCode               NVARCHAR(60) NOT NULL,
	DisplayName            NVARCHAR(300) NOT NULL,
	Statement              NVARCHAR(MAX) NULL,
	IsEssential            BIT NOT NULL CONSTRAINT DF_Legal_DecisionElement_Essential DEFAULT 1,
	IsSatisfied            BIT NOT NULL CONSTRAINT DF_Legal_DecisionElement_Satisfied DEFAULT 0,
	Support                DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionElement_Support DEFAULT 0,
	VerificationStatus     NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionElement_Status DEFAULT N'UNVERIFIED',
	SortOrder              INT NOT NULL CONSTRAINT DF_Legal_DecisionElement_Sort DEFAULT 0,
	TenantId               UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc         DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionElement_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId        UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc        DATETIME2 NULL,
	ModifiedByUserId       UNIQUEIDENTIFIER NULL,
	IsDeleted              BIT NOT NULL CONSTRAINT DF_Legal_DecisionElement_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionElement_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionElement_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionElement_Session ON POLOXI.Legal_DecisionLegalElement (DecisionSessionId) WHERE IsDeleted = 0;

-- ── ReasoningStrategy: a theory tying elements/issues to a candidate outcome (S-nodes). ────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionReasoningStrategy',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionReasoningStrategy
(
	DecisionReasoningStrategyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionStrategy PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId           UNIQUEIDENTIFIER NOT NULL,
	DecisionCandidateId         UNIQUEIDENTIFIER NULL,
	NodeCode                    NVARCHAR(60) NOT NULL,
	DisplayName                 NVARCHAR(300) NOT NULL,
	Rationale                   NVARCHAR(MAX) NULL,
	Support                     DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionStrategy_Support DEFAULT 0,
	IsViable                    BIT NOT NULL CONSTRAINT DF_Legal_DecisionStrategy_Viable DEFAULT 1,
	VerificationStatus          NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionStrategy_Status DEFAULT N'UNVERIFIED',
	SortOrder                   INT NOT NULL CONSTRAINT DF_Legal_DecisionStrategy_Sort DEFAULT 0,
	TenantId                    UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionStrategy_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId             UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc             DATETIME2 NULL,
	ModifiedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                   BIT NOT NULL CONSTRAINT DF_Legal_DecisionStrategy_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionStrategy_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionStrategy_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionStrategy_Session ON POLOXI.Legal_DecisionReasoningStrategy (DecisionSessionId) WHERE IsDeleted = 0;

-- ── BurdenRule: which party bears the burden on an element and the applicable standard (B-nodes). ─
IF OBJECT_ID(N'POLOXI.Legal_DecisionBurdenRule',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionBurdenRule
(
	DecisionBurdenRuleId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionBurdenRule PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId      UNIQUEIDENTIFIER NOT NULL,
	DecisionLegalElementId UNIQUEIDENTIFIER NULL,
	NodeCode               NVARCHAR(60) NOT NULL,
	BurdenedParty          NVARCHAR(120) NOT NULL,
	StandardOfProof        NVARCHAR(120) NULL,
	Statement              NVARCHAR(MAX) NULL,
	IsSatisfied            BIT NOT NULL CONSTRAINT DF_Legal_DecisionBurden_Satisfied DEFAULT 0,
	VerificationStatus     NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionBurden_Status DEFAULT N'UNVERIFIED',
	SortOrder              INT NOT NULL CONSTRAINT DF_Legal_DecisionBurden_Sort DEFAULT 0,
	TenantId               UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc         DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionBurden_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId        UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc        DATETIME2 NULL,
	ModifiedByUserId       UNIQUEIDENTIFIER NULL,
	IsDeleted              BIT NOT NULL CONSTRAINT DF_Legal_DecisionBurden_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionBurden_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionBurden_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionBurden_Session ON POLOXI.Legal_DecisionBurdenRule (DecisionSessionId) WHERE IsDeleted = 0;

-- ── ProceduralConstraint: a procedural gate that must be satisfied for the posture (C-nodes). ───
IF OBJECT_ID(N'POLOXI.Legal_DecisionProceduralConstraint',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionProceduralConstraint
(
	DecisionProceduralConstraintId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionProcedural PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId              UNIQUEIDENTIFIER NOT NULL,
	NodeCode                       NVARCHAR(60) NOT NULL,
	DisplayName                    NVARCHAR(300) NOT NULL,
	Statement                      NVARCHAR(MAX) NULL,
	IsSatisfied                    BIT NOT NULL CONSTRAINT DF_Legal_DecisionProc_Satisfied DEFAULT 0,
	IsDispositive                  BIT NOT NULL CONSTRAINT DF_Legal_DecisionProc_Dispositive DEFAULT 0,
	VerificationStatus             NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionProc_Status DEFAULT N'UNVERIFIED',
	SortOrder                      INT NOT NULL CONSTRAINT DF_Legal_DecisionProc_Sort DEFAULT 0,
	TenantId                       UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc                 DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionProc_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                DATETIME2 NULL,
	ModifiedByUserId               UNIQUEIDENTIFIER NULL,
	IsDeleted                      BIT NOT NULL CONSTRAINT DF_Legal_DecisionProc_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionProc_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionProc_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionProc_Session ON POLOXI.Legal_DecisionProceduralConstraint (DecisionSessionId) WHERE IsDeleted = 0;

-- ── Typed dependency edge: the single relation table that connects every node kind. ─────────────
-- An edge is polymorphic: (SourceKind, SourceNodeId) → (TargetKind, TargetNodeId). This is what
-- forces authority/evidence to reach a candidate only through a traceable chain, and it is the unit
-- the independent verifier marks VERIFIED/INVALIDATED. Core propagates invalidation along edges.
--   RelationCode   : SUPPORTS | REQUIRES | SATISFIES | ESTABLISHES | DEPENDS_ON | CONTRADICTS
--   NodeKind       : EVIDENCE | FACT | PROPOSITION | ELEMENT | BURDEN | PROCEDURE | STRATEGY | CANDIDATE | BRANCH
--   VerificationStatus : UNVERIFIED | VERIFIED | INVALIDATED
IF OBJECT_ID(N'POLOXI.Legal_DecisionGraphEdge',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionGraphEdge
(
	DecisionGraphEdgeId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionGraphEdge PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	RelationCode         NVARCHAR(40) NOT NULL,
	SourceNodeKind       NVARCHAR(40) NOT NULL,
	SourceNodeId         UNIQUEIDENTIFIER NOT NULL,
	TargetNodeKind       NVARCHAR(40) NOT NULL,
	TargetNodeId         UNIQUEIDENTIFIER NOT NULL,
	SupportWeight        DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionEdge_Weight DEFAULT 0,
	Materiality          DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionEdge_Materiality DEFAULT 0,
	IsEssential          BIT NOT NULL CONSTRAINT DF_Legal_DecisionEdge_Essential DEFAULT 0,
	IsDispositive        BIT NOT NULL CONSTRAINT DF_Legal_DecisionEdge_Dispositive DEFAULT 0,
	VerificationStatus   NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionEdge_Status DEFAULT N'UNVERIFIED',
	VerificationNotes    NVARCHAR(MAX) NULL,
	PropagatedStateCode  NVARCHAR(40) NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionEdge_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionEdge_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionEdge_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionEdge_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionEdge_Session ON POLOXI.Legal_DecisionGraphEdge (DecisionSessionId) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_DecisionEdge_Source',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionEdge_Source ON POLOXI.Legal_DecisionGraphEdge (DecisionSessionId, SourceNodeKind, SourceNodeId) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_DecisionEdge_Target',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionEdge_Target ON POLOXI.Legal_DecisionGraphEdge (DecisionSessionId, TargetNodeKind, TargetNodeId) WHERE IsDeleted = 0;

-- ── Strongest-losing-side test result: the mandatory adversarial gate before DECISION_READY. ────
IF OBJECT_ID(N'POLOXI.Legal_DecisionLosingSideTest',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionLosingSideTest
(
	DecisionLosingSideTestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionLosingSide PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId        UNIQUEIDENTIFIER NOT NULL,
	WinnerCandidateId        UNIQUEIDENTIFIER NULL,
	ChallengerCandidateId    UNIQUEIDENTIFIER NULL,
	StrongestCaseSummary     NVARCHAR(MAX) NULL,
	ChallengerStrength       DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionLosing_Strength DEFAULT 0,
	WinnerStrength           DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionLosing_WinStr DEFAULT 0,
	WinnerSurvived           BIT NOT NULL CONSTRAINT DF_Legal_DecisionLosing_Survived DEFAULT 0,
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionLosing_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_DecisionLosing_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionLosing_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionLosing_Session',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionLosing_Session ON POLOXI.Legal_DecisionLosingSideTest (DecisionSessionId) WHERE IsDeleted = 0;

-- ── Session V2 columns: per-session toggle + readiness verdict (ablation target). ──────────────
IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'UseDependencyGraph') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD UseDependencyGraph BIT NOT NULL CONSTRAINT DF_Legal_DecisionSession_UseGraph DEFAULT 0;
IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'ReadinessSatisfied') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD ReadinessSatisfied BIT NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'ReadinessBlockersJson') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD ReadinessBlockersJson NVARCHAR(MAX) NULL;

COMMIT TRANSACTION;
