SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Judz Run & Assessment Management — execution ledger (Phase 1 / M2–M4 read slice)
-- Records every requested analysis as an AnalysisRun with its actual execution steps and attempts,
-- plus a guarded current-assessment pointer (one per decision/matter). Follows Table → API → UI
-- order; all objects live in the POLOXI schema, are prefixed Legal_AnalysisRun*, and carry the
-- standard base/audit fields. Existing decision/cockpit tables are left intact.
-- Immutable history is enforced through controlled writes at the application layer; completed
-- snapshots are not updated through ordinary paths.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── AnalysisRun: one requested execution for a matter (and optionally a specific decision). ──
IF OBJECT_ID(N'POLOXI.Legal_AnalysisRun',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AnalysisRun
(
	AnalysisRunId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AnalysisRun PRIMARY KEY DEFAULT NEWID(),
	MatterId             UNIQUEIDENTIFIER NOT NULL,
	DecisionId           UNIQUEIDENTIFIER NULL,
	RunLabel             NVARCHAR(40) NOT NULL,          -- display identifier, e.g. R-105
	Title                NVARCHAR(300) NOT NULL,
	RunTypeCode          NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_AnalysisRun_RunType DEFAULT N'MATTER_ANALYSIS',
	TriggerTypeCode      NVARCHAR(80) NULL,
	TriggerText          NVARCHAR(300) NULL,
	ExecutionStatusCode  NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_AnalysisRun_ExecStatus DEFAULT N'Pending',
	PublicationStatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_AnalysisRun_PubStatus DEFAULT N'NotPublished',
	PackLabel            NVARCHAR(80) NULL,              -- e.g. Domain Pack / Original
	InputSnapshotLabel   NVARCHAR(40) NULL,             -- e.g. S-105
	RequestedUtc         DATETIME2 NOT NULL CONSTRAINT DF_Legal_AnalysisRun_Requested DEFAULT SYSUTCDATETIME(),
	StartedUtc           DATETIME2 NULL,
	CompletedUtc         DATETIME2 NULL,
	DurationSeconds      INT NULL,
	ExecutionBanner      NVARCHAR(600) NULL,
	FooterNote           NVARCHAR(600) NULL,
	IdempotencyScope     NVARCHAR(120) NOT NULL CONSTRAINT DF_Legal_AnalysisRun_IdemScope DEFAULT N'DEFAULT',
	IdempotencyKey       NVARCHAR(200) NOT NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_AnalysisRun_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_AnalysisRun_IsDeleted DEFAULT 0,
	RowVersion           ROWVERSION
);

GO

IF OBJECT_ID(N'UX_Legal_AnalysisRun_Idempotency',N'UQ') IS NULL
   AND OBJECT_ID(N'POLOXI.Legal_AnalysisRun',N'U') IS NOT NULL
	CREATE UNIQUE INDEX UX_Legal_AnalysisRun_Idempotency
		ON POLOXI.Legal_AnalysisRun (TenantId, IdempotencyScope, IdempotencyKey)
		WHERE IsDeleted = 0;

GO

IF OBJECT_ID(N'IX_Legal_AnalysisRun_Matter',N'IX') IS NULL
	CREATE INDEX IX_Legal_AnalysisRun_Matter
		ON POLOXI.Legal_AnalysisRun (TenantId, MatterId, RequestedUtc DESC)
		WHERE IsDeleted = 0;

GO

-- ── AnalysisRunStep: one actual operation within a run's pipeline. ──
IF OBJECT_ID(N'POLOXI.Legal_AnalysisRunStep',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AnalysisRunStep
(
	AnalysisRunStepId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AnalysisRunStep PRIMARY KEY DEFAULT NEWID(),
	AnalysisRunId        UNIQUEIDENTIFIER NOT NULL,
	StepCode             NVARCHAR(80) NOT NULL,
	DisplayName          NVARCHAR(200) NOT NULL,
	Description          NVARCHAR(600) NULL,
	Sequence             INT NOT NULL,
	StatusCode           NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_AnalysisRunStep_Status DEFAULT N'Pending',
	StatusLabel          NVARCHAR(60) NULL,
	IsRequiredForCompletion  BIT NOT NULL CONSTRAINT DF_Legal_AnalysisRunStep_ReqComplete DEFAULT 1,
	IsRequiredForPublication BIT NOT NULL CONSTRAINT DF_Legal_AnalysisRunStep_ReqPublish DEFAULT 1,
	StartedUtc           DATETIME2 NULL,
	CompletedUtc         DATETIME2 NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_AnalysisRunStep_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_AnalysisRunStep_IsDeleted DEFAULT 0,
	RowVersion           ROWVERSION,
	CONSTRAINT FK_Legal_AnalysisRunStep_Run FOREIGN KEY (AnalysisRunId)
		REFERENCES POLOXI.Legal_AnalysisRun (AnalysisRunId)
);

GO

IF OBJECT_ID(N'UX_Legal_AnalysisRunStep_Sequence',N'UQ') IS NULL
   AND OBJECT_ID(N'POLOXI.Legal_AnalysisRunStep',N'U') IS NOT NULL
	CREATE UNIQUE INDEX UX_Legal_AnalysisRunStep_Sequence
		ON POLOXI.Legal_AnalysisRunStep (AnalysisRunId, Sequence)
		WHERE IsDeleted = 0;

GO

-- ── AnalysisRunStepAttempt: preserves retries and their individual outcomes. ──
IF OBJECT_ID(N'POLOXI.Legal_AnalysisRunStepAttempt',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AnalysisRunStepAttempt
(
	AnalysisRunStepAttemptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AnalysisRunStepAttempt PRIMARY KEY DEFAULT NEWID(),
	AnalysisRunStepId    UNIQUEIDENTIFIER NOT NULL,
	AttemptNumber        INT NOT NULL,
	StatusCode           NVARCHAR(40) NOT NULL,
	Summary              NVARCHAR(400) NULL,
	StartedUtc           DATETIME2 NULL,
	CompletedUtc         DATETIME2 NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_AnalysisRunStepAttempt_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_AnalysisRunStepAttempt_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_AnalysisRunStepAttempt_Step FOREIGN KEY (AnalysisRunStepId)
		REFERENCES POLOXI.Legal_AnalysisRunStep (AnalysisRunStepId)
);

GO

IF OBJECT_ID(N'UX_Legal_AnalysisRunStepAttempt_Number',N'UQ') IS NULL
   AND OBJECT_ID(N'POLOXI.Legal_AnalysisRunStepAttempt',N'U') IS NOT NULL
	CREATE UNIQUE INDEX UX_Legal_AnalysisRunStepAttempt_Number
		ON POLOXI.Legal_AnalysisRunStepAttempt (AnalysisRunStepId, AttemptNumber)
		WHERE IsDeleted = 0;

GO

-- ── AnalysisRunCurrentAssessment: guarded current pointer (one per decision/matter). ──
IF OBJECT_ID(N'POLOXI.Legal_AnalysisRunCurrentAssessment',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AnalysisRunCurrentAssessment
(
	CurrentAssessmentId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AnalysisRunCurrentAssessment PRIMARY KEY DEFAULT NEWID(),
	MatterId             UNIQUEIDENTIFIER NOT NULL,
	DecisionId           UNIQUEIDENTIFIER NULL,
	AnalysisRunId        UNIQUEIDENTIFIER NOT NULL,
	AssessmentLabel      NVARCHAR(40) NOT NULL,          -- e.g. A-104
	ReviewStateText      NVARCHAR(120) NULL,             -- e.g. Attorney reviewed
	FreshnessStatusCode  NVARCHAR(40) NULL,
	FreshnessNote        NVARCHAR(300) NULL,
	PublishedUtc         DATETIME2 NOT NULL CONSTRAINT DF_Legal_AnalysisRunCurrentAssessment_Published DEFAULT SYSUTCDATETIME(),
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_AnalysisRunCurrentAssessment_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_AnalysisRunCurrentAssessment_IsDeleted DEFAULT 0,
	RowVersion           ROWVERSION,
	CONSTRAINT FK_Legal_AnalysisRunCurrentAssessment_Run FOREIGN KEY (AnalysisRunId)
		REFERENCES POLOXI.Legal_AnalysisRun (AnalysisRunId)
);

GO

-- One current pointer per decision (or per matter when DecisionId is NULL), scoped to tenant.
IF OBJECT_ID(N'UX_Legal_AnalysisRunCurrentAssessment_Decision',N'UQ') IS NULL
   AND OBJECT_ID(N'POLOXI.Legal_AnalysisRunCurrentAssessment',N'U') IS NOT NULL
	CREATE UNIQUE INDEX UX_Legal_AnalysisRunCurrentAssessment_Decision
		ON POLOXI.Legal_AnalysisRunCurrentAssessment (TenantId, MatterId, DecisionId)
		WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
