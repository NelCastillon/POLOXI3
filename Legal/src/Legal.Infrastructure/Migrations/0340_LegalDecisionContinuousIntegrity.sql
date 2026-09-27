SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Continuous Decision Integrity (Phase 1: Change Awareness).
--
-- A legal conclusion must stay connected to the evidence and assumptions that made it valid. These
-- durable tables let Judz preserve prior conclusions immutably, record the dependency structure that
-- makes a conclusion valid, detect when newly arrived evidence MAY affect a conclusion, and track a
-- distinct CURRENT reliance status that is separate from the historical readiness at evaluation time.
--
-- POLOXI Core remains stateless and authoritative for scoring. These tables are the change-awareness +
-- audit substrate that Judz owns:
--
--   Legal_DecisionSnapshot          — immutable evaluation state (readiness at time-of-eval + current reliance).
--   Legal_PropositionEvidenceLink   — source(version)→proposition SUPPORT or CONTRADICTION link.
--   Legal_DecisionDependency        — validated candidate/proposition/branch dependency relationship.
--   Legal_MatterChangeEvent         — new/corrected/contradictory information entering the matter.
--   Legal_DecisionImpact            — which propositions/candidates/decisions a change event affects.
--   Legal_DecisionReviewTask        — required investigation or attorney action produced by an impact.
--
-- All carry the standard base/audit fields, are tenant-scoped, and are created idempotently. Snapshots
-- are append-only: history is never overwritten or retrospectively re-labeled as wrong.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── DecisionSnapshot: an immutable evaluation state for a matter/session. ──────────────────────
--   ReadinessStatusCode is the historical readiness captured AT evaluation time (immutable).
--   RelianceStatusCode is the CURRENT reliance state and is the only mutable status column:
--     CURRENT | REVIEW_PENDING | REASSESSMENT_REQUIRED | SUPERSEDED | WITHDRAWN.
--   SupersededBySnapshotId links a snapshot to the newer evaluated snapshot that replaced it.
IF OBJECT_ID(N'POLOXI.Legal_DecisionSnapshot', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionSnapshot
(
	DecisionSnapshotId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionSnapshot PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId          UNIQUEIDENTIFIER NOT NULL,
	DecisionSessionId         UNIQUEIDENTIFIER NULL,
	SnapshotNumber            INT NOT NULL CONSTRAINT DF_Legal_DecisionSnapshot_Num DEFAULT 1,
	Title                     NVARCHAR(400) NULL,
	PropositionStatement      NVARCHAR(MAX) NULL,
	ReadinessStatusCode       NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DecisionSnapshot_Ready DEFAULT N'NOT_READY',
	RelianceStatusCode        NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DecisionSnapshot_Reliance DEFAULT N'CURRENT',
	RelianceReason            NVARCHAR(MAX) NULL,
	IsAttorneyApproved        BIT NOT NULL CONSTRAINT DF_Legal_DecisionSnapshot_Approved DEFAULT 0,
	ApprovedByUserId          UNIQUEIDENTIFIER NULL,
	ApprovedDateUtc           DATETIME2 NULL,
	SupersededBySnapshotId    UNIQUEIDENTIFIER NULL,
	EvidenceSummaryJson       NVARCHAR(MAX) NULL,
	EvaluatedDateUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionSnapshot_Eval DEFAULT SYSUTCDATETIME(),
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionSnapshot_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionSnapshot_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionSnapshot_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_DecisionSnapshot_Superseded FOREIGN KEY (SupersededBySnapshotId) REFERENCES POLOXI.Legal_DecisionSnapshot (DecisionSnapshotId)
);

IF OBJECT_ID(N'IX_Legal_DecisionSnapshot_Matter', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionSnapshot_Matter ON POLOXI.Legal_DecisionSnapshot (DecisionMatterId, SnapshotNumber DESC, CreatedDateUtc DESC) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_DecisionSnapshot_Reliance', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionSnapshot_Reliance ON POLOXI.Legal_DecisionSnapshot (TenantId, RelianceStatusCode, ModifiedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── PropositionEvidenceLink: how a source version supports/contradicts a proposition. ──────────
--   LinkKindCode: SUPPORT | CONTRADICTION | CONTEXT. LegalDocumentVersionId is the immutable source
--   version (existing document-version table). StatusCode tracks whether the link is CONTESTED after
--   a later contradicting source arrives — the OLD source is NOT auto-invalidated; only contested.
IF OBJECT_ID(N'POLOXI.Legal_PropositionEvidenceLink', N'U') IS NULL
CREATE TABLE POLOXI.Legal_PropositionEvidenceLink
(
	PropositionEvidenceLinkId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PropEvidenceLink PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId          UNIQUEIDENTIFIER NOT NULL,
	DecisionSnapshotId        UNIQUEIDENTIFIER NULL,
	PropositionKey            NVARCHAR(200) NOT NULL,
	PropositionStatement      NVARCHAR(MAX) NULL,
	LegalDocumentVersionId    UNIQUEIDENTIFIER NULL,
	LegalDocumentPassageId    UNIQUEIDENTIFIER NULL,
	LinkKindCode              NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_PropEvidenceLink_Kind DEFAULT N'SUPPORT',
	SupportWeight             DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_PropEvidenceLink_Weight DEFAULT 0,
	StatusCode                NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_PropEvidenceLink_Status DEFAULT N'ACTIVE',
	Notes                     NVARCHAR(MAX) NULL,
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_PropEvidenceLink_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_PropEvidenceLink_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_PropEvidenceLink_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_PropEvidenceLink_Snapshot FOREIGN KEY (DecisionSnapshotId) REFERENCES POLOXI.Legal_DecisionSnapshot (DecisionSnapshotId)
);

IF OBJECT_ID(N'IX_Legal_PropEvidenceLink_Matter', N'IX') IS NULL
	CREATE INDEX IX_Legal_PropEvidenceLink_Matter ON POLOXI.Legal_PropositionEvidenceLink (DecisionMatterId, PropositionKey) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_PropEvidenceLink_Version', N'IX') IS NULL
	CREATE INDEX IX_Legal_PropEvidenceLink_Version ON POLOXI.Legal_PropositionEvidenceLink (LegalDocumentVersionId) WHERE IsDeleted = 0;

GO

-- ── DecisionDependency: validated candidate/proposition/branch relationships. ──────────────────
--   This is the bounded matter-level dependency structure that makes targeted reevaluation possible
--   without a graph database: when a proposition is affected, we know which candidates depend on it.
IF OBJECT_ID(N'POLOXI.Legal_MatterDependency', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterDependency
(
	DecisionDependencyId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterDependency PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId          UNIQUEIDENTIFIER NOT NULL,
	DecisionSnapshotId        UNIQUEIDENTIFIER NULL,
	DependentKindCode         NVARCHAR(40) NOT NULL,     -- CANDIDATE | PROPOSITION | BRANCH | DECISION
	DependentKey              NVARCHAR(200) NOT NULL,
	DependsOnKindCode         NVARCHAR(40) NOT NULL,     -- PROPOSITION | EVIDENCE | AUTHORITY | ASSUMPTION
	DependsOnKey              NVARCHAR(200) NOT NULL,
	RelationCode              NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_MatterDependency_Rel DEFAULT N'DEPENDS_ON',
	IsEssential               BIT NOT NULL CONSTRAINT DF_Legal_MatterDependency_Ess DEFAULT 0,
	SupportWeight             DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_MatterDependency_Weight DEFAULT 0,
	StatusCode                NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterDependency_Status DEFAULT N'VALID',
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterDependency_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_MatterDependency_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_MatterDependency_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId)
);

IF OBJECT_ID(N'IX_Legal_MatterDependency_DependsOn', N'IX') IS NULL
	CREATE INDEX IX_Legal_MatterDependency_DependsOn ON POLOXI.Legal_MatterDependency (DecisionMatterId, DependsOnKindCode, DependsOnKey) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_MatterDependency_Dependent', N'IX') IS NULL
	CREATE INDEX IX_Legal_MatterDependency_Dependent ON POLOXI.Legal_MatterDependency (DecisionMatterId, DependentKindCode, DependentKey) WHERE IsDeleted = 0;

GO

-- ── MatterChangeEvent: new/corrected/contradictory information entering the matter. ────────────
--   IdempotencyKey (tenant+matter+version+kind) guarantees a retried ingest never processes twice.
--   ClassificationCode: NO_MATERIAL_IMPACT | POTENTIAL_IMPACT | MATERIAL_CONTRADICTION | NEW_MATERIAL_FACT.
--   ProcessingStatusCode: PENDING | PROCESSED | FAILED. Source version is immutable (hash preserved).
IF OBJECT_ID(N'POLOXI.Legal_MatterChangeEvent', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterChangeEvent
(
	MatterChangeEventId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterChangeEvent PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId          UNIQUEIDENTIFIER NOT NULL,
	ChangeSourceCode          NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_MatterChangeEvent_Src DEFAULT N'DOCUMENT_UPLOAD',
	LegalDocumentId           UNIQUEIDENTIFIER NULL,
	LegalDocumentVersionId    UNIQUEIDENTIFIER NULL,
	SourceHash                NVARCHAR(128) NULL,
	SourceLabel               NVARCHAR(400) NULL,
	DocumentDateUtc           DATETIME2 NULL,
	IdempotencyKey            NVARCHAR(300) NOT NULL,
	ClassificationCode        NVARCHAR(60) NULL,
	Summary                   NVARCHAR(MAX) NULL,
	CandidateFactsJson        NVARCHAR(MAX) NULL,
	ProcessingStatusCode      NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterChangeEvent_Status DEFAULT N'PENDING',
	ProcessingError           NVARCHAR(MAX) NULL,
	AffectedPropositionCount  INT NOT NULL CONSTRAINT DF_Legal_MatterChangeEvent_Props DEFAULT 0,
	AffectedCandidateCount    INT NOT NULL CONSTRAINT DF_Legal_MatterChangeEvent_Cands DEFAULT 0,
	ProcessedDateUtc          DATETIME2 NULL,
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterChangeEvent_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_MatterChangeEvent_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_MatterChangeEvent_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId)
);

IF OBJECT_ID(N'UX_Legal_MatterChangeEvent_Idem', N'UQ') IS NULL AND OBJECT_ID(N'UX_Legal_MatterChangeEvent_Idem', N'IX') IS NULL
	CREATE UNIQUE INDEX UX_Legal_MatterChangeEvent_Idem ON POLOXI.Legal_MatterChangeEvent (DecisionMatterId, IdempotencyKey) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_MatterChangeEvent_Matter', N'IX') IS NULL
	CREATE INDEX IX_Legal_MatterChangeEvent_Matter ON POLOXI.Legal_MatterChangeEvent (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── DecisionImpact: which conclusions/candidates a change event affects. ───────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionImpact', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionImpact
(
	DecisionImpactId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionImpact PRIMARY KEY DEFAULT NEWID(),
	MatterChangeEventId       UNIQUEIDENTIFIER NOT NULL,
	DecisionMatterId          UNIQUEIDENTIFIER NOT NULL,
	DecisionSnapshotId        UNIQUEIDENTIFIER NULL,
	AffectedKindCode          NVARCHAR(40) NOT NULL,     -- PROPOSITION | CANDIDATE | DECISION
	AffectedKey               NVARCHAR(200) NOT NULL,
	AffectedLabel             NVARCHAR(400) NULL,
	PreviousStateCode         NVARCHAR(60) NULL,
	CurrentStateCode          NVARCHAR(60) NULL,
	ImpactSeverityCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionImpact_Sev DEFAULT N'POTENTIAL',
	Rationale                 NVARCHAR(MAX) NULL,
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionImpact_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionImpact_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionImpact_Event FOREIGN KEY (MatterChangeEventId) REFERENCES POLOXI.Legal_MatterChangeEvent (MatterChangeEventId),
	CONSTRAINT FK_Legal_DecisionImpact_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_DecisionImpact_Snapshot FOREIGN KEY (DecisionSnapshotId) REFERENCES POLOXI.Legal_DecisionSnapshot (DecisionSnapshotId)
);

IF OBJECT_ID(N'IX_Legal_DecisionImpact_Event', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionImpact_Event ON POLOXI.Legal_DecisionImpact (MatterChangeEventId) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_DecisionImpact_Matter', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionImpact_Matter ON POLOXI.Legal_DecisionImpact (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── DecisionReviewTask: required investigation or attorney action from an impact. ──────────────
--   Attorney-approved conclusions are never silently reversed; a review task is created and the
--   attorney controls whether a revised analysis replaces the approved decision (Phase 3).
IF OBJECT_ID(N'POLOXI.Legal_DecisionReviewTask', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionReviewTask
(
	DecisionReviewTaskId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionReviewTask PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId          UNIQUEIDENTIFIER NOT NULL,
	MatterChangeEventId       UNIQUEIDENTIFIER NULL,
	DecisionSnapshotId        UNIQUEIDENTIFIER NULL,
	TaskKindCode              NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DecisionReviewTask_Kind DEFAULT N'CHANGE_REVIEW',
	Title                     NVARCHAR(400) NOT NULL,
	Detail                    NVARCHAR(MAX) NULL,
	RequiredAction            NVARCHAR(MAX) NULL,
	PriorityCode              NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionReviewTask_Prio DEFAULT N'NORMAL',
	StatusCode                NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionReviewTask_Status DEFAULT N'OPEN',
	AssignedToUserId          UNIQUEIDENTIFIER NULL,
	ResolvedByUserId          UNIQUEIDENTIFIER NULL,
	ResolvedDateUtc           DATETIME2 NULL,
	ResolutionNotes           NVARCHAR(MAX) NULL,
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionReviewTask_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionReviewTask_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionReviewTask_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_DecisionReviewTask_Event FOREIGN KEY (MatterChangeEventId) REFERENCES POLOXI.Legal_MatterChangeEvent (MatterChangeEventId),
	CONSTRAINT FK_Legal_DecisionReviewTask_Snapshot FOREIGN KEY (DecisionSnapshotId) REFERENCES POLOXI.Legal_DecisionSnapshot (DecisionSnapshotId)
);

IF OBJECT_ID(N'IX_Legal_DecisionReviewTask_Matter', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionReviewTask_Matter ON POLOXI.Legal_DecisionReviewTask (DecisionMatterId, StatusCode, CreatedDateUtc DESC) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_DecisionReviewTask_Open', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionReviewTask_Open ON POLOXI.Legal_DecisionReviewTask (TenantId, StatusCode, PriorityCode, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
