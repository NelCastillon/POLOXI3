SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- /legal/search Run History — per-user standalone Wide search run capture
-- Persists the ENTIRE WideSearchResponse for every /legal/search run so the inline Run History
-- panel can list past searches (newest first) and reopen the exact saved result read-only, or
-- re-run the original query. Isolated from Decision2 (POLOXI.Legal_WideRunResult / Legal_AnalysisRun):
-- this table is self-contained, has NO MatterId FK, and is scoped to the running user via
-- (TenantId, CreatedByUserId). Promoted scalar columns drive the list/cards; DetailJson holds the
-- loss-free serialized WideSearchResponse for full reopen. Standard base/audit fields included.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

IF OBJECT_ID(N'POLOXI.Legal_WideSearchRunResult',N'U') IS NULL
CREATE TABLE POLOXI.Legal_WideSearchRunResult
(
	WideSearchRunResultId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideSearchRunResult PRIMARY KEY DEFAULT NEWID(),
	WideExecutionId          UNIQUEIDENTIFIER NOT NULL,
	Query                    NVARCHAR(MAX) NULL,
	StatusCode               NVARCHAR(60) NULL,
	TerminationReasonCode    NVARCHAR(80) NULL,
	DepthReached             INT NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_Depth DEFAULT 0,
	LlmCallCount             INT NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_Llm DEFAULT 0,
	FinalConfidence          DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_FinalConf DEFAULT 0,
	DecisionConfidence       DECIMAL(9,6) NULL,
	AnswerVerificationCode   NVARCHAR(60) NULL,
	AnswerKindCode           NVARCHAR(40) NULL,
	FinalAnswer              NVARCHAR(MAX) NULL,
	DurationMilliseconds     BIGINT NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_Duration DEFAULT 0,
	ProviderCodeUsed         NVARCHAR(100) NULL,
	ModelCodeUsed            NVARCHAR(100) NULL,
	EvidenceCoverage         DECIMAL(9,6) NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_EvCov DEFAULT 0,
	ExternalEvidenceCount    INT NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_ExtEvCnt DEFAULT 0,
	EnterpriseEvidenceCount  INT NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_EntEvCnt DEFAULT 0,
	BranchCount              INT NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_BranchCnt DEFAULT 0,
	CandidateCount           INT NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_CandCnt DEFAULT 0,
	ContextCode              NVARCHAR(50) NULL,
	-- Loss-free capture of the entire WideSearchResponse for read-only reopen of the exact result.
	DetailJson               NVARCHAR(MAX) NULL,
	CapturedUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_Captured DEFAULT SYSUTCDATETIME(),
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_WideSearchRunResult_IsDeleted DEFAULT 0,
	RowVersion               ROWVERSION
);

GO

-- Primary list access path: a user's own runs, newest first, tenant-scoped.
IF OBJECT_ID(N'IX_Legal_WideSearchRunResult_User',N'IX') IS NULL
   AND OBJECT_ID(N'POLOXI.Legal_WideSearchRunResult',N'U') IS NOT NULL
	CREATE INDEX IX_Legal_WideSearchRunResult_User
		ON POLOXI.Legal_WideSearchRunResult (TenantId, CreatedByUserId, CreatedDateUtc DESC)
		WHERE IsDeleted = 0;

GO

-- Direct lookup by execution id (reopen by WideExecutionId).
IF OBJECT_ID(N'IX_Legal_WideSearchRunResult_Exec',N'IX') IS NULL
   AND OBJECT_ID(N'POLOXI.Legal_WideSearchRunResult',N'U') IS NOT NULL
	CREATE INDEX IX_Legal_WideSearchRunResult_Exec
		ON POLOXI.Legal_WideSearchRunResult (TenantId, WideExecutionId)
		WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
