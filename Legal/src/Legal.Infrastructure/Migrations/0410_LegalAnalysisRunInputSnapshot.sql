SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Judz Run & Assessment Management — immutable input snapshot (Phase 1 / M5)
-- Captures the exact decision input contract each run executed under, so opening a run reproduces its
-- recorded inputs even after the matter, prompt, Domain Pack, or configuration later change. One
-- snapshot per run (S-###), FK to Legal_AnalysisRun, standard base/audit fields, POLOXI schema.
-- Snapshots are write-once from controlled application paths; they are never updated in place.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

IF OBJECT_ID(N'POLOXI.Legal_AnalysisRunInputSnapshot',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AnalysisRunInputSnapshot
(
	InputSnapshotId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AnalysisRunInputSnapshot PRIMARY KEY DEFAULT NEWID(),
	AnalysisRunId        UNIQUEIDENTIFIER NOT NULL,
	MatterId             UNIQUEIDENTIFIER NOT NULL,
	SnapshotLabel        NVARCHAR(40) NOT NULL,          -- e.g. S-105
	Query                NVARCHAR(MAX) NULL,
	ContextCode          NVARCHAR(50) NULL,
	ModeCode             NVARCHAR(40) NULL,
	ModelCode            NVARCHAR(100) NULL,
	DomainPackCode       NVARCHAR(60) NULL,
	DiscoveryModeCode    NVARCHAR(40) NULL,
	Jurisdiction         NVARCHAR(120) NULL,
	GoverningLaw         NVARCHAR(120) NULL,
	CourtOrForum         NVARCHAR(300) NULL,
	Posture              NVARCHAR(200) NULL,
	MotionTarget         NVARCHAR(300) NULL,
	CounterfactualAssumption NVARCHAR(1000) NULL,
	ClarificationAnswer  NVARCHAR(2000) NULL,
	UsedDependencyGraph  BIT NULL,
	ContractJson         NVARCHAR(MAX) NOT NULL,         -- full immutable projection of the decision input contract
	CapturedUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_AnalysisRunInputSnapshot_Captured DEFAULT SYSUTCDATETIME(),
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_AnalysisRunInputSnapshot_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_AnalysisRunInputSnapshot_IsDeleted DEFAULT 0,
	RowVersion           ROWVERSION,
	CONSTRAINT FK_Legal_AnalysisRunInputSnapshot_Run FOREIGN KEY (AnalysisRunId)
		REFERENCES POLOXI.Legal_AnalysisRun (AnalysisRunId)
);

GO

-- One snapshot per run (non-deleted), scoped to tenant.
IF OBJECT_ID(N'UX_Legal_AnalysisRunInputSnapshot_Run',N'UQ') IS NULL
   AND OBJECT_ID(N'POLOXI.Legal_AnalysisRunInputSnapshot',N'U') IS NOT NULL
	CREATE UNIQUE INDEX UX_Legal_AnalysisRunInputSnapshot_Run
		ON POLOXI.Legal_AnalysisRunInputSnapshot (TenantId, AnalysisRunId)
		WHERE IsDeleted = 0;

GO

IF OBJECT_ID(N'IX_Legal_AnalysisRunInputSnapshot_Matter',N'IX') IS NULL
	CREATE INDEX IX_Legal_AnalysisRunInputSnapshot_Matter
		ON POLOXI.Legal_AnalysisRunInputSnapshot (TenantId, MatterId, CapturedUtc DESC)
		WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
