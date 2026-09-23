SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal V2.1 — Closed-loop persistence tables.
--
-- These durable tables record the closed loop that turns a verification change into a new POLOXI
-- investigation. POLOXI remains authoritative; these tables are the AUDIT + IDEMPOTENCY substrate:
--
--   Legal_DecisionDependencyEvent   — one row per verification-change event (idempotency key).
--   Legal_DecisionRecompetition     — one row per Candidate×Branch recompetition triggered by an event.
--   Legal_DecisionResearchNeed      — outcome-directed ResearchNeed produced from the highest-IV frontier.
--   Legal_DecisionFrontierSnapshot  — a frontier/entropy/IV snapshot captured after recompetition.
--
-- All carry the standard base/audit fields, are tenant-scoped, and are created idempotently.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

-- ── DependencyEvent: the authoritative record of a verification change + its structured impact. ──
--   IdempotencyKey is unique per (session, edge, new-status, requested-version) so a retried event
--   never runs the loop twice.
IF OBJECT_ID(N'POLOXI.Legal_DecisionDependencyEvent', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDependencyEvent
(
	DecisionDependencyEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDepEvent PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId         UNIQUEIDENTIFIER NOT NULL,
	MatterId                  UNIQUEIDENTIFIER NULL,
	DecisionGraphEdgeId       UNIQUEIDENTIFIER NULL,
	IdempotencyKey            NVARCHAR(200) NOT NULL,
	PreviousStatus            NVARCHAR(40) NULL,
	NewStatus                 NVARCHAR(40) NOT NULL,
	ImpactJson                NVARCHAR(MAX) NULL,
	AffectedBranchCount       INT NOT NULL CONSTRAINT DF_Legal_DecisionDepEvent_Branches DEFAULT 0,
	AffectedCandidateCount    INT NOT NULL CONSTRAINT DF_Legal_DecisionDepEvent_Cands DEFAULT 0,
	RecompetitionTriggered    BIT NOT NULL CONSTRAINT DF_Legal_DecisionDepEvent_Recomp DEFAULT 0,
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDepEvent_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionDepEvent_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionDepEvent_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'UX_Legal_DecisionDepEvent_Idem', N'UQ') IS NULL AND OBJECT_ID(N'UX_Legal_DecisionDepEvent_Idem', N'IX') IS NULL
	CREATE UNIQUE INDEX UX_Legal_DecisionDepEvent_Idem ON POLOXI.Legal_DecisionDependencyEvent (DecisionSessionId, IdempotencyKey) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_DecisionDepEvent_Session', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionDepEvent_Session ON POLOXI.Legal_DecisionDependencyEvent (DecisionSessionId, CreatedDateUtc) WHERE IsDeleted = 0;

-- ── Recompetition: audit of a Candidate×Branch re-ranking triggered by a dependency event. ───────
--   PreviousRankingJson / CurrentRankingJson store ordered candidate snapshots for auditability
--   (WHY did C1 weaken?). WinnerChanged flags a leadership flip.
IF OBJECT_ID(N'POLOXI.Legal_DecisionRecompetition', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionRecompetition
(
	DecisionRecompetitionId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionRecomp PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId         UNIQUEIDENTIFIER NOT NULL,
	DecisionDependencyEventId UNIQUEIDENTIFIER NULL,
	PreviousWinnerCandidateId UNIQUEIDENTIFIER NULL,
	CurrentWinnerCandidateId  UNIQUEIDENTIFIER NULL,
	WinnerChanged             BIT NOT NULL CONSTRAINT DF_Legal_DecisionRecomp_WinChg DEFAULT 0,
	PreviousEntropy           DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionRecomp_PrevEnt DEFAULT 0,
	CurrentEntropy            DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionRecomp_CurEnt DEFAULT 0,
	PreviousMargin            DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionRecomp_PrevMar DEFAULT 0,
	CurrentMargin             DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionRecomp_CurMar DEFAULT 0,
	ReopenedBranchCount       INT NOT NULL CONSTRAINT DF_Legal_DecisionRecomp_Reopen DEFAULT 0,
	PreviousRankingJson       NVARCHAR(MAX) NULL,
	CurrentRankingJson        NVARCHAR(MAX) NULL,
	ReasonCode                NVARCHAR(60) NULL,
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionRecomp_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionRecomp_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionRecomp_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionRecomp_Session', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionRecomp_Session ON POLOXI.Legal_DecisionRecompetition (DecisionSessionId, CreatedDateUtc) WHERE IsDeleted = 0;

-- ── ResearchNeed: the outcome-directed research request produced from the highest-IV frontier. ───
IF OBJECT_ID(N'POLOXI.Legal_DecisionResearchNeed', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionResearchNeed
(
	DecisionResearchNeedId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionResearch PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId         UNIQUEIDENTIFIER NOT NULL,
	MatterId                  UNIQUEIDENTIFIER NULL,
	DecisionBranchId          UNIQUEIDENTIFIER NULL,
	DecisionDependencyEventId UNIQUEIDENTIFIER NULL,
	IssueLabel                NVARCHAR(400) NULL,
	PropositionToResolve      NVARCHAR(MAX) NULL,
	AuthorityKind             NVARCHAR(60) NULL,
	RequiredEvidenceKind      NVARCHAR(60) NULL,
	WhyDecisionRelevant       NVARCHAR(MAX) NULL,
	ExpectedDiscrimination    DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionResearch_Disc DEFAULT 0,
	CurrentUncertainty        DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionResearch_Unc DEFAULT 0,
	InformationValue          DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionResearch_IV DEFAULT 0,
	FalsificationCondition    NVARCHAR(MAX) NULL,
	StatusCode                NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionResearch_Status DEFAULT N'OPEN',
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionResearch_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionResearch_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionResearch_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionResearch_Session', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionResearch_Session ON POLOXI.Legal_DecisionResearchNeed (DecisionSessionId, StatusCode) WHERE IsDeleted = 0;

-- ── FrontierSnapshot: the decision-frontier + entropy/IV state captured after a recompetition. ───
IF OBJECT_ID(N'POLOXI.Legal_DecisionFrontierSnapshot', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionFrontierSnapshot
(
	DecisionFrontierSnapshotId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionFrontier PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId          UNIQUEIDENTIFIER NOT NULL,
	DecisionRecompetitionId    UNIQUEIDENTIFIER NULL,
	Entropy                    DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionFrontier_Ent DEFAULT 0,
	Margin                     DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionFrontier_Mar DEFAULT 0,
	OpenFrontierCount          INT NOT NULL CONSTRAINT DF_Legal_DecisionFrontier_Open DEFAULT 0,
	TopBranchId                UNIQUEIDENTIFIER NULL,
	TopBranchInformationValue  DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_DecisionFrontier_TopIV DEFAULT 0,
	FrontierJson               NVARCHAR(MAX) NULL,
	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionFrontier_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_DecisionFrontier_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionFrontier_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionFrontier_Session', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionFrontier_Session ON POLOXI.Legal_DecisionFrontierSnapshot (DecisionSessionId, CreatedDateUtc) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
