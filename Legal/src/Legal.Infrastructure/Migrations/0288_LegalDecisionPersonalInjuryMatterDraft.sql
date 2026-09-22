SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Intelligence — Personal Injury "Generate New Matter" draft + provenance.
-- Scaffolding for the AI-assisted creation path: a draft holds proposed PI matter field values that
-- remain NON-authoritative until an attorney reviews and confirms them. Each field carries its
-- provenance (source type, source document/passage, verification state, conflict reason). Extraction
-- wiring is deferred; this migration establishes the DB-backed storage so the review UI binds to real
-- data. All objects live in POLOXI schema with base/audit fields.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Legal_DecisionPIMatterDraft: a pending Generate-New-Matter draft. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterDraft', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIMatterDraft
(
	DecisionPIMatterDraftId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIMatterDraft PRIMARY KEY DEFAULT NEWID(),
	StatusCode              NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionPIMatterDraft_Status DEFAULT N'PENDING_REVIEW',
	Prompt                  NVARCHAR(MAX) NULL,
	SourceDocumentIdsJson   NVARCHAR(MAX) NULL,
	ConfirmedMatterId       UNIQUEIDENTIFIER NULL,
	TenantId                UNIQUEIDENTIFIER NULL,
	CreatedDateUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIMatterDraft_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId         UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc         DATETIME2 NULL,
	ModifiedByUserId        UNIQUEIDENTIFIER NULL,
	IsDeleted               BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIMatterDraft_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterDraft') AND name = N'IX_Legal_DecisionPIMatterDraft_Lookup')
	CREATE INDEX IX_Legal_DecisionPIMatterDraft_Lookup
		ON POLOXI.Legal_DecisionPIMatterDraft (TenantId, StatusCode, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIMatterDraftField: one proposed field value + provenance. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterDraftField', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIMatterDraftField
(
	DecisionPIMatterDraftFieldId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIMatterDraftField PRIMARY KEY DEFAULT NEWID(),
	DecisionPIMatterDraftId      UNIQUEIDENTIFIER NOT NULL
		CONSTRAINT FK_Legal_DecisionPIMatterDraftField_Draft REFERENCES POLOXI.Legal_DecisionPIMatterDraft (DecisionPIMatterDraftId),
	FieldCode                    NVARCHAR(120) NOT NULL,
	ProposedValue                NVARCHAR(MAX) NULL,
	SourceType                   NVARCHAR(60) NULL,   -- PI_DRAFT_SOURCE_TYPE
	SourceDocumentId             UNIQUEIDENTIFIER NULL,
	SourcePassage                NVARCHAR(MAX) NULL,
	VerificationState            NVARCHAR(60) NULL,   -- PI_DRAFT_VERIFICATION_STATE
	ConflictReason               NVARCHAR(1000) NULL,
	ConflictsJson                NVARCHAR(MAX) NULL,  -- competing [{Value,Source}] when Conflict
	SortOrder                    INT NOT NULL CONSTRAINT DF_Legal_DecisionPIMatterDraftField_SortOrder DEFAULT 0,
	TenantId                     UNIQUEIDENTIFIER NULL,
	CreatedDateUtc               DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIMatterDraftField_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId              UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc              DATETIME2 NULL,
	ModifiedByUserId             UNIQUEIDENTIFIER NULL,
	IsDeleted                    BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIMatterDraftField_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterDraftField') AND name = N'IX_Legal_DecisionPIMatterDraftField_Draft')
	CREATE INDEX IX_Legal_DecisionPIMatterDraftField_Draft
		ON POLOXI.Legal_DecisionPIMatterDraftField (DecisionPIMatterDraftId, SortOrder) WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
