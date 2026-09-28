SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Decision Change Intelligence (first-class DecisionDelta on top of CDC).
--
-- Continuous Decision Integrity (migration 0340) captures WHAT changed (change events + impacts) and
-- reevaluates the decision (append-only snapshots). This layer records the ANSWER attorneys actually
-- ask: "what changed in my matter since yesterday, why does it matter, and what should I examine?"
--
-- Each material reevaluation of a change event produces exactly one immutable Legal_DecisionDelta row
-- that projects the intelligence already produced by POLOXI Core into a single traceable artifact:
--
--   Before  → the superseded snapshot / prior winner
--   Change  → the matter change event that triggered the reevaluation
--   Affected propositions / evidence → counts captured from the recorded impacts
--   Score delta      → margin + entropy before/after (POLOXI Core owns the math; this only records it)
--   Candidate delta  → previous winner vs current winner, and whether the winner changed
--   IV / Frontier / Readiness deltas → recorded when available, left NULL when not computed (never faked)
--   Required action  → the attorney-facing next step
--
-- The row is append-only and never scores: it is a durable read-model of an already-computed
-- recompetition. Carries the standard base/audit fields, is tenant-scoped, and is created idempotently.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── DecisionDelta: one immutable "what changed" record per material reevaluation. ──────────────────
--   DeltaKindCode:   WINNER_CHANGED | MARGIN_SHIFTED | REEVALUATED_NO_CHANGE.
--   FromSnapshotId:  the snapshot that was current before the change (superseded), when one existed.
--   ToSnapshotId:    the newly appended snapshot, when the outcome changed.
IF OBJECT_ID(N'POLOXI.Legal_DecisionDelta', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDelta
(
	DecisionDeltaId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDelta PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId          UNIQUEIDENTIFIER NOT NULL,
	MatterChangeEventId       UNIQUEIDENTIFIER NULL,
	FromSnapshotId            UNIQUEIDENTIFIER NULL,
	ToSnapshotId              UNIQUEIDENTIFIER NULL,
	DeltaKindCode             NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DecisionDelta_Kind DEFAULT N'REEVALUATED_NO_CHANGE',
	ClassificationCode        NVARCHAR(60) NULL,
	Summary                   NVARCHAR(MAX) NULL,
	-- Change context
	ChangeSourceLabel         NVARCHAR(400) NULL,
	-- Candidate delta
	WinnerChanged             BIT NOT NULL CONSTRAINT DF_Legal_DecisionDelta_WinnerChanged DEFAULT 0,
	PreviousWinnerId          UNIQUEIDENTIFIER NULL,
	CurrentWinnerId           UNIQUEIDENTIFIER NULL,
	PreviousWinnerLabel       NVARCHAR(400) NULL,
	CurrentWinnerLabel        NVARCHAR(400) NULL,
	-- Score delta (recorded, not computed here)
	PreviousMargin            FLOAT NULL,
	CurrentMargin             FLOAT NULL,
	PreviousEntropy           FLOAT NULL,
	CurrentEntropy            FLOAT NULL,
	-- Affected structure (from recorded impacts)
	AffectedPropositionCount  INT NOT NULL CONSTRAINT DF_Legal_DecisionDelta_AffProp DEFAULT 0,
	AffectedCandidateCount    INT NOT NULL CONSTRAINT DF_Legal_DecisionDelta_AffCand DEFAULT 0,
	AffectedEvidenceCount     INT NOT NULL CONSTRAINT DF_Legal_DecisionDelta_AffEvid DEFAULT 0,
	-- IV / Frontier / Readiness deltas (NULL when not computed at this join point)
	InformationValueDelta     FLOAT NULL,
	FrontierChanged           BIT NULL,
	PreviousReadinessCode     NVARCHAR(60) NULL,
	CurrentReadinessCode      NVARCHAR(60) NULL,
	-- Attorney-facing outcome
	AttorneyReviewRequired    BIT NOT NULL CONSTRAINT DF_Legal_DecisionDelta_Review DEFAULT 0,
	RequiredAction            NVARCHAR(MAX) NULL,
	DetailJson                NVARCHAR(MAX) NULL,
	OccurredDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDelta_Occurred DEFAULT SYSUTCDATETIME(),
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDelta_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionDelta_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionDelta_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_DecisionDelta_Event FOREIGN KEY (MatterChangeEventId) REFERENCES POLOXI.Legal_MatterChangeEvent (MatterChangeEventId),
	CONSTRAINT FK_Legal_DecisionDelta_From FOREIGN KEY (FromSnapshotId) REFERENCES POLOXI.Legal_DecisionSnapshot (DecisionSnapshotId),
	CONSTRAINT FK_Legal_DecisionDelta_To FOREIGN KEY (ToSnapshotId) REFERENCES POLOXI.Legal_DecisionSnapshot (DecisionSnapshotId)
);

IF OBJECT_ID(N'IX_Legal_DecisionDelta_Matter', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionDelta_Matter ON POLOXI.Legal_DecisionDelta (DecisionMatterId, OccurredDateUtc DESC, CreatedDateUtc DESC) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_DecisionDelta_Event', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionDelta_Event ON POLOXI.Legal_DecisionDelta (MatterChangeEventId) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Legal_DecisionDelta_Review', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionDelta_Review ON POLOXI.Legal_DecisionDelta (TenantId, AttorneyReviewRequired, OccurredDateUtc DESC) WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
