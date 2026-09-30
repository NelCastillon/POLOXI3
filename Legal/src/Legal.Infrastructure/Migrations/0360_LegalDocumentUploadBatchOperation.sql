SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- Judz Enterprise Document Evidence Upload — Stage 1 of 3 (Request identity + batch reporting).
--
-- These tables are ADDITIVE. They do not change how documents are stored, hashed, extracted, or
-- reasoned over. They give the upload path two things the canonical corpus stack does not yet model:
--
--   1. UploadBatch  — one immutable batch per user/API "add evidence" action (e.g. a production drop).
--                     Used for the enterprise upload dashboard (files discovered / accepted / deduped /
--                     failed) and to group EvidenceOccurrence rows (added in migration 0361).
--
--   2. UploadOperation — one row per file upload command, keyed by a client IdempotencyKey so a retried
--                     POST (network retry, double-click) returns the same operation instead of creating
--                     a duplicate evidence intake. The physical content hash lives on the existing
--                     POLOXI.Legal_MatterDocumentVersion.Sha256Hash; this row records the REQUEST
--                     identity and a fingerprint so a key reused for a DIFFERENT file can be rejected.
--
-- Governing principle: never process the same upload command twice; never lose evidence. The canonical
-- content deduplication (same SHA-256) already exists in the intake service — this stage only adds the
-- request-idempotency and batch-reporting layer around it. Schema-only (Table stage of Table/API/UI).
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'POLOXI.Legal_UploadBatch', N'U') IS NULL
CREATE TABLE POLOXI.Legal_UploadBatch
(
	LegalUploadBatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_UploadBatch PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_UploadBatch_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	BatchNumber NVARCHAR(60) NOT NULL,
	SourceTypeCode NVARCHAR(60) NULL,
	Custodian NVARCHAR(300) NULL,
	ProducedBy NVARCHAR(300) NULL,
	ProductionId NVARCHAR(120) NULL,
	ReceivedDateUtc DATETIME2 NULL,
	Notes NVARCHAR(2000) NULL,
	StatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_UploadBatch_Status DEFAULT N'OPEN',
	-- Rolling report counters maintained by the intake service as each file is processed.
	FilesDiscovered INT NOT NULL CONSTRAINT DF_Legal_UploadBatch_Discovered DEFAULT 0,
	FilesAccepted INT NOT NULL CONSTRAINT DF_Legal_UploadBatch_Accepted DEFAULT 0,
	ExactContentDuplicates INT NOT NULL CONSTRAINT DF_Legal_UploadBatch_Dupes DEFAULT 0,
	NewEvidenceOccurrences INT NOT NULL CONSTRAINT DF_Legal_UploadBatch_Occurrences DEFAULT 0,
	ProcessingReused INT NOT NULL CONSTRAINT DF_Legal_UploadBatch_Reused DEFAULT 0,
	SecurityFailures INT NOT NULL CONSTRAINT DF_Legal_UploadBatch_Security DEFAULT 0,
	ProcessingFailures INT NOT NULL CONSTRAINT DF_Legal_UploadBatch_Failures DEFAULT 0,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_UploadBatch_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_UploadBatch_Deleted DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_UploadBatch') AND name=N'UX_Legal_UploadBatch_Number')
	CREATE UNIQUE INDEX UX_Legal_UploadBatch_Number ON POLOXI.Legal_UploadBatch (TenantId, BatchNumber) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_UploadBatch') AND name=N'IX_Legal_UploadBatch_Matter')
	CREATE INDEX IX_Legal_UploadBatch_Matter ON POLOXI.Legal_UploadBatch (TenantId, DecisionMatterId, StatusCode) INCLUDE (BatchNumber, FilesAccepted, CreatedDateUtc) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_UploadOperation', N'U') IS NULL
CREATE TABLE POLOXI.Legal_UploadOperation
(
	LegalUploadOperationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_UploadOperation PRIMARY KEY DEFAULT NEWID(),
	LegalUploadBatchId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_UploadOperation_Batch REFERENCES POLOXI.Legal_UploadBatch (LegalUploadBatchId),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_UploadOperation_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	-- Client-supplied request identity. A retried POST with the same key + fingerprint returns the
	-- same operation; the same key with a DIFFERENT fingerprint is a client bug and is rejected.
	IdempotencyKey NVARCHAR(200) NOT NULL,
	RequestFingerprint NVARCHAR(200) NOT NULL,
	FileName NVARCHAR(500) NOT NULL,
	DeclaredContentType NVARCHAR(200) NULL,
	ExpectedLength BIGINT NULL,
	StatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_UploadOperation_Status DEFAULT N'CREATED',
	-- Result linkage: on success the operation points at the document/version the intake produced.
	-- ContentReused is 1 when the physical bytes already existed (same SHA-256) and only a new
	-- provenance occurrence was created.
	ResultLegalDocumentId UNIQUEIDENTIFIER NULL,
	ResultLegalDocumentVersionId UNIQUEIDENTIFIER NULL,
	ContentReused BIT NOT NULL CONSTRAINT DF_Legal_UploadOperation_Reused DEFAULT 0,
	Sha256Hash CHAR(64) NULL,
	ErrorCode NVARCHAR(100) NULL,
	ErrorMessage NVARCHAR(4000) NULL,
	CorrelationId NVARCHAR(120) NULL,
	CompletedDateUtc DATETIME2 NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_UploadOperation_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_UploadOperation_Deleted DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_UploadOperation') AND name=N'UX_Legal_UploadOperation_Idempotency')
	CREATE UNIQUE INDEX UX_Legal_UploadOperation_Idempotency ON POLOXI.Legal_UploadOperation (TenantId, DecisionMatterId, IdempotencyKey) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_UploadOperation') AND name=N'IX_Legal_UploadOperation_Batch')
	CREATE INDEX IX_Legal_UploadOperation_Batch ON POLOXI.Legal_UploadOperation (TenantId, LegalUploadBatchId, StatusCode) INCLUDE (FileName, ResultLegalDocumentId, ContentReused) WHERE IsDeleted=0;
GO

COMMIT TRANSACTION;
