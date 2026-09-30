SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- Judz Enterprise Document Evidence Upload — Stage 3 of 3 (Processing identity + evidentiary
-- independence identity).
--
--   ProcessingOperation : one reusable, deterministic ledger for every expensive processing effect
--                         (extract, OCR, passage, embed, assert, AER, bind). Keyed by
--                         (Tenant, OperationType, OperationKey) where OperationKey is a SHA-256 of the
--                         operation type + input identity + input hash + processor version + config
--                         version. Same input + same processor version => RETRY (reuse); changed input
--                         / model / prompt / config => a NEW operation (reprocessing). Lease columns
--                         give at-least-once delivery + idempotent effects for distributed workers.
--                         This WRAPS the existing POLOXI.Legal_DocumentProcessingRun (which stays the
--                         per-stage execution record); it does not replace it or change the reasoning
--                         core.
--
--   EvidenceLineageGroup / Member : physical dedup answers "same bytes?"; lineage answers "independent
--                         source of evidentiary information?". Multiple documents can derive from one
--                         underlying source (police report → deposition → expert report quoting it) and
--                         therefore must not count as independent evidence. Lineage is resolved AFTER
--                         physical dedup and is advisory (fail-soft): POLOXI can consult it when
--                         weighing independent support without it ever blocking intake.
--
-- ADDITIVE, schema-only. No change to extraction, embeddings, propositions, CDC, IV, or scoring.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'POLOXI.Legal_ProcessingOperation', N'U') IS NULL
CREATE TABLE POLOXI.Legal_ProcessingOperation
(
	LegalProcessingOperationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ProcessingOperation PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_ProcessingOperation_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	OperationTypeCode NVARCHAR(60) NOT NULL,
	-- Deterministic identity: SHA-256(OperationType + InputIdentity + InputVersion + InputHash +
	-- ProcessorVersion + ConfigVersion). Same key = same effect, so a retry is a no-op reuse.
	OperationKey CHAR(64) NOT NULL,
	InputEntityTypeCode NVARCHAR(60) NULL,
	InputEntityId UNIQUEIDENTIFIER NULL,
	InputVersion INT NULL,
	InputHash CHAR(64) NULL,
	ProcessorCode NVARCHAR(100) NULL,
	ProcessorVersion NVARCHAR(60) NULL,
	ConfigVersion NVARCHAR(60) NULL,
	StatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_ProcessingOperation_Status DEFAULT N'PENDING',
	AttemptCount INT NOT NULL CONSTRAINT DF_Legal_ProcessingOperation_Attempt DEFAULT 0,
	LeaseOwner NVARCHAR(200) NULL,
	LeaseExpiresDateUtc DATETIME2 NULL,
	StartedDateUtc DATETIME2 NULL,
	CompletedDateUtc DATETIME2 NULL,
	ResultEntityTypeCode NVARCHAR(60) NULL,
	ResultEntityId UNIQUEIDENTIFIER NULL,
	ResultHash CHAR(64) NULL,
	ErrorCode NVARCHAR(100) NULL,
	ErrorMessage NVARCHAR(4000) NULL,
	CorrelationId NVARCHAR(120) NULL,
	CausationId NVARCHAR(120) NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_ProcessingOperation_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_ProcessingOperation_Deleted DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_ProcessingOperation') AND name=N'UX_Legal_ProcessingOperation_Key')
	CREATE UNIQUE INDEX UX_Legal_ProcessingOperation_Key ON POLOXI.Legal_ProcessingOperation (TenantId, OperationTypeCode, OperationKey) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_ProcessingOperation') AND name=N'IX_Legal_ProcessingOperation_Lease')
	CREATE INDEX IX_Legal_ProcessingOperation_Lease ON POLOXI.Legal_ProcessingOperation (TenantId, StatusCode, LeaseExpiresDateUtc) INCLUDE (OperationTypeCode, AttemptCount) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_EvidenceLineageGroup', N'U') IS NULL
CREATE TABLE POLOXI.Legal_EvidenceLineageGroup
(
	LegalEvidenceLineageGroupId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_EvidenceLineageGroup PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_EvidenceLineageGroup_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	LineageLabel NVARCHAR(300) NULL,
	-- The underlying independent source this lineage represents (e.g. "Officer observation").
	OriginDescription NVARCHAR(1000) NULL,
	IndependenceBasisCode NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_EvidenceLineageGroup_Basis DEFAULT N'UNRESOLVED',
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_EvidenceLineageGroup_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_EvidenceLineageGroup_Deleted DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_EvidenceLineageGroup') AND name=N'IX_Legal_EvidenceLineageGroup_Matter')
	CREATE INDEX IX_Legal_EvidenceLineageGroup_Matter ON POLOXI.Legal_EvidenceLineageGroup (TenantId, DecisionMatterId) INCLUDE (IndependenceBasisCode, LineageLabel) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_EvidenceLineageMember', N'U') IS NULL
CREATE TABLE POLOXI.Legal_EvidenceLineageMember
(
	LegalEvidenceLineageMemberId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_EvidenceLineageMember PRIMARY KEY DEFAULT NEWID(),
	LegalEvidenceLineageGroupId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_EvidenceLineageMember_Group REFERENCES POLOXI.Legal_EvidenceLineageGroup (LegalEvidenceLineageGroupId),
	-- A lineage member is an occurrence that derives from (or is) the group's underlying source.
	LegalEvidenceOccurrenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_EvidenceLineageMember_Occurrence REFERENCES POLOXI.Legal_EvidenceOccurrence (LegalEvidenceOccurrenceId),
	-- ORIGINAL = the primary source; DERIVATIVE = quotes/restates it (does not add independent weight).
	RoleCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_EvidenceLineageMember_Role DEFAULT N'ORIGINAL',
	DerivationNote NVARCHAR(1000) NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_EvidenceLineageMember_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_EvidenceLineageMember_Deleted DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_EvidenceLineageMember') AND name=N'UX_Legal_EvidenceLineageMember_Occurrence')
	CREATE UNIQUE INDEX UX_Legal_EvidenceLineageMember_Occurrence ON POLOXI.Legal_EvidenceLineageMember (TenantId, LegalEvidenceLineageGroupId, LegalEvidenceOccurrenceId) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_EvidenceLineageMember') AND name=N'IX_Legal_EvidenceLineageMember_Group')
	CREATE INDEX IX_Legal_EvidenceLineageMember_Group ON POLOXI.Legal_EvidenceLineageMember (TenantId, LegalEvidenceLineageGroupId, RoleCode) WHERE IsDeleted=0;
GO

COMMIT TRANSACTION;
