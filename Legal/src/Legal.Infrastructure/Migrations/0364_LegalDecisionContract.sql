SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Decision Contract (first-class, versioned Judz domain).
--
-- The Decision Contract is the authoritative problem specification that sits above POLOXI: it frames
-- WHAT is being decided, under WHICH legal framework, against WHICH competing outcomes, and within
-- WHAT evidence/authority boundaries. It does NOT decide which candidate wins — POLOXI remains the
-- single authoritative evaluator. This migration adds the contract aggregate + supporting tables and
-- DB-backed dropdown options. Approved contracts are immutable; edits happen through new DRAFT
-- versions. Only one ACTIVE contract per matter (filtered unique index).
--
-- Every table lives in the POLOXI schema, is prefixed Legal_DecisionContract*, and carries the
-- standard base/audit fields: TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc,
-- ModifiedByUserId, IsDeleted. The contract head carries a rowversion for optimistic concurrency.
-- Idempotent (CREATE guards + MERGE seeds). Data-backed options reuse Legal_DecisionMatterOption.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Contract head: versioned per-matter authoritative decision specification. ────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionContract',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionContract
(
	DecisionContractId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionContract PRIMARY KEY DEFAULT NEWID(),
	MatterId                 UNIQUEIDENTIFIER NOT NULL,
	VersionNumber            INT NOT NULL CONSTRAINT DF_Legal_DecisionContract_Version DEFAULT 1,
	-- DRAFT | READY_FOR_REVIEW | APPROVED | ACTIVE | SUPERSEDED | REJECTED
	StatusCode               NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_DecisionContract_Status DEFAULT N'DRAFT',

	-- Decision
	DecisionQuestion         NVARCHAR(2000) NULL,
	ClientObjective          NVARCHAR(1000) NULL,
	SuccessDefinition        NVARCHAR(1500) NULL,
	DecisionDate             DATE NULL,

	-- Legal context
	Jurisdiction             NVARCHAR(300) NULL,
	CourtOrForum             NVARCHAR(500) NULL,
	GoverningLaw             NVARCHAR(1000) NULL,
	ProceduralPosture        NVARCHAR(300) NULL,
	CaseType                 NVARCHAR(300) NULL,
	ApplicableLegalFramework NVARCHAR(1000) NULL,

	-- Burden & standard
	MovingParty              NVARCHAR(300) NULL,
	InitialBurden            NVARCHAR(1000) NULL,
	UltimateBurden           NVARCHAR(1000) NULL,
	StandardOfProofOrReview  NVARCHAR(1000) NULL,
	BurdenNotes              NVARCHAR(2000) NULL,

	-- Decision boundaries
	EvidenceBoundary         NVARCHAR(2000) NULL,
	AuthorityBoundary        NVARCHAR(2000) NULL,
	SourceRestrictions       NVARCHAR(2000) NULL,
	AuthorityCutoffDate      DATE NULL,

	-- Additional settings
	DecisionHorizon          NVARCHAR(300) NULL,
	ExternalResearchPermitted BIT NOT NULL CONSTRAINT DF_Legal_DecisionContract_ExtResearch DEFAULT 1,
	ReviewBeforeActivation   BIT NOT NULL CONSTRAINT DF_Legal_DecisionContract_ReviewActivate DEFAULT 1,
	ReviewBeforeFinal        BIT NOT NULL CONSTRAINT DF_Legal_DecisionContract_ReviewFinal DEFAULT 1,
	SemanticValidationMode   NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionContract_SemMode DEFAULT N'STRICT',
	Notes                    NVARCHAR(2000) NULL,

	-- Governance
	ApprovedByUserId         UNIQUEIDENTIFIER NULL,
	ApprovedDateUtc          DATETIME2 NULL,
	SubmittedByUserId        UNIQUEIDENTIFIER NULL,
	SubmittedDateUtc         DATETIME2 NULL,

	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionContract_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_DecisionContract_IsDeleted DEFAULT 0,
	RowVersion               ROWVERSION NOT NULL,
	CONSTRAINT UQ_Legal_DecisionContract_MatterVersion UNIQUE (TenantId, MatterId, VersionNumber)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DecisionContract_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionContract'))
	CREATE INDEX IX_Legal_DecisionContract_Matter ON POLOXI.Legal_DecisionContract (TenantId, MatterId, IsDeleted) INCLUDE (VersionNumber, StatusCode);

-- One authoritative ACTIVE contract per matter.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Legal_DecisionContract_OneActive' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionContract'))
	CREATE UNIQUE INDEX UX_Legal_DecisionContract_OneActive ON POLOXI.Legal_DecisionContract (TenantId, MatterId) WHERE StatusCode = N'ACTIVE' AND IsDeleted = 0;

GO

-- ── Competing outcomes: at least two active per contract; identity survives within the version. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionContractCandidate',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionContractCandidate
(
	DecisionContractCandidateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionContractCandidate PRIMARY KEY DEFAULT NEWID(),
	DecisionContractId          UNIQUEIDENTIFIER NOT NULL,
	CandidateCode               NVARCHAR(30) NOT NULL,
	OutcomeText                 NVARCHAR(2000) NOT NULL,
	-- Dispositive | Partial | Procedural | Alternative | Other (DB-backed CANDIDATE_TYPE options)
	CandidateTypeCode           NVARCHAR(100) NULL,
	-- ACTIVE | INACTIVE (DB-backed CANDIDATE_STATE options)
	StateCode                   NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_DCCandidate_State DEFAULT N'ACTIVE',
	DisplayOrder                INT NOT NULL CONSTRAINT DF_Legal_DCCandidate_Order DEFAULT 0,
	TenantId                    UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_DCCandidate_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId             UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc             DATETIME2 NULL,
	ModifiedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                   BIT NOT NULL CONSTRAINT DF_Legal_DCCandidate_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DCCandidate_Contract FOREIGN KEY (DecisionContractId) REFERENCES POLOXI.Legal_DecisionContract (DecisionContractId),
	CONSTRAINT UQ_Legal_DCCandidate_Code UNIQUE (DecisionContractId, CandidateCode)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DCCandidate_Contract' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionContractCandidate'))
	CREATE INDEX IX_Legal_DCCandidate_Contract ON POLOXI.Legal_DecisionContractCandidate (DecisionContractId, IsDeleted) INCLUDE (DisplayOrder);

GO

-- ── Fact-boundary references: KNOWN | DISPUTED | UNKNOWN facts scoped to the contract version. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionContractFactBoundary',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionContractFactBoundary
(
	DecisionContractFactBoundaryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionContractFactBoundary PRIMARY KEY DEFAULT NEWID(),
	DecisionContractId             UNIQUEIDENTIFIER NOT NULL,
	FactId                         UNIQUEIDENTIFIER NULL,
	-- KNOWN | DISPUTED | UNKNOWN
	FactStateCode                  NVARCHAR(30) NOT NULL,
	SnapshotText                   NVARCHAR(2000) NOT NULL,
	DisplayOrder                   INT NOT NULL CONSTRAINT DF_Legal_DCFact_Order DEFAULT 0,
	TenantId                       UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc                 DATETIME2 NOT NULL CONSTRAINT DF_Legal_DCFact_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                DATETIME2 NULL,
	ModifiedByUserId               UNIQUEIDENTIFIER NULL,
	IsDeleted                      BIT NOT NULL CONSTRAINT DF_Legal_DCFact_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DCFact_Contract FOREIGN KEY (DecisionContractId) REFERENCES POLOXI.Legal_DecisionContract (DecisionContractId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DCFact_Contract' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionContractFactBoundary'))
	CREATE INDEX IX_Legal_DCFact_Contract ON POLOXI.Legal_DecisionContractFactBoundary (DecisionContractId, IsDeleted) INCLUDE (FactStateCode, DisplayOrder);

GO

-- ── Key issues / statutes tags (structured multi-value). ─────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionContractTag',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionContractTag
(
	DecisionContractTagId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionContractTag PRIMARY KEY DEFAULT NEWID(),
	DecisionContractId    UNIQUEIDENTIFIER NOT NULL,
	-- STATUTE_RULE | KEY_ISSUE
	TagKindCode           NVARCHAR(40) NOT NULL,
	TagText               NVARCHAR(400) NOT NULL,
	DisplayOrder          INT NOT NULL CONSTRAINT DF_Legal_DCTag_Order DEFAULT 0,
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DCTag_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_DCTag_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DCTag_Contract FOREIGN KEY (DecisionContractId) REFERENCES POLOXI.Legal_DecisionContract (DecisionContractId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DCTag_Contract' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionContractTag'))
	CREATE INDEX IX_Legal_DCTag_Contract ON POLOXI.Legal_DecisionContractTag (DecisionContractId, IsDeleted) INCLUDE (TagKindCode, DisplayOrder);

GO

-- ── Review records: submitted / returned / approved / rejected with reviewer + comment. ──────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionContractReview',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionContractReview
(
	DecisionContractReviewId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionContractReview PRIMARY KEY DEFAULT NEWID(),
	DecisionContractId       UNIQUEIDENTIFIER NOT NULL,
	-- SUBMITTED | APPROVED | RETURNED | REJECTED
	ReviewActionCode         NVARCHAR(30) NOT NULL,
	ReviewerUserId           UNIQUEIDENTIFIER NULL,
	Comment                  NVARCHAR(2000) NULL,
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_DCReview_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_DCReview_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DCReview_Contract FOREIGN KEY (DecisionContractId) REFERENCES POLOXI.Legal_DecisionContract (DecisionContractId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DCReview_Contract' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionContractReview'))
	CREATE INDEX IX_Legal_DCReview_Contract ON POLOXI.Legal_DecisionContractReview (DecisionContractId, IsDeleted) INCLUDE (CreatedDateUtc);

GO

-- ── Audit trail: every material contract operation (created/edited/submitted/approved/etc.). ─────
IF OBJECT_ID(N'POLOXI.Legal_DecisionContractAudit',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionContractAudit
(
	DecisionContractAuditId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionContractAudit PRIMARY KEY DEFAULT NEWID(),
	DecisionContractId      UNIQUEIDENTIFIER NOT NULL,
	MatterId                UNIQUEIDENTIFIER NOT NULL,
	-- CREATED | EDITED | SUBMITTED | RETURNED | APPROVED | REJECTED | ACTIVATED | SUPERSEDED | CLONED
	ActionCode              NVARCHAR(40) NOT NULL,
	SectionCode             NVARCHAR(60) NULL,
	Detail                  NVARCHAR(2000) NULL,
	TenantId                UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_DCAudit_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId         UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc         DATETIME2 NULL,
	ModifiedByUserId        UNIQUEIDENTIFIER NULL,
	IsDeleted               BIT NOT NULL CONSTRAINT DF_Legal_DCAudit_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DCAudit_Contract FOREIGN KEY (DecisionContractId) REFERENCES POLOXI.Legal_DecisionContract (DecisionContractId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DCAudit_Contract' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionContractAudit'))
	CREATE INDEX IX_Legal_DCAudit_Contract ON POLOXI.Legal_DecisionContractAudit (DecisionContractId, IsDeleted) INCLUDE (CreatedDateUtc);

GO

-- ── DB-backed dropdown options (reuse Legal_DecisionMatterOption; idempotent MERGE). ─────────────
-- FieldCode groups consumed by the Decision Contract editor dropdowns.
MERGE POLOXI.Legal_DecisionMatterOption AS target
USING (VALUES
	-- Procedural posture
	(N'DC_PROCEDURAL_POSTURE', N'Pleading / Complaint',            10),
	(N'DC_PROCEDURAL_POSTURE', N'Motion to Dismiss',               20),
	(N'DC_PROCEDURAL_POSTURE', N'Discovery',                       30),
	(N'DC_PROCEDURAL_POSTURE', N'Summary Judgment',                40),
	(N'DC_PROCEDURAL_POSTURE', N'Trial',                           50),
	(N'DC_PROCEDURAL_POSTURE', N'Post-Trial',                      60),
	(N'DC_PROCEDURAL_POSTURE', N'Appeal',                          70),

	-- Case type
	(N'DC_CASE_TYPE', N'Personal Injury - Auto Collision',         10),
	(N'DC_CASE_TYPE', N'Personal Injury - Premises',               20),
	(N'DC_CASE_TYPE', N'Personal Injury - Product Liability',      30),
	(N'DC_CASE_TYPE', N'Medical Malpractice',                      40),
	(N'DC_CASE_TYPE', N'Wrongful Death',                           50),
	(N'DC_CASE_TYPE', N'Employment',                               60),
	(N'DC_CASE_TYPE', N'Contract Dispute',                         70),
	(N'DC_CASE_TYPE', N'Other',                                    80),

	-- Legal framework
	(N'DC_LEGAL_FRAMEWORK', N'Negligence (General)',               10),
	(N'DC_LEGAL_FRAMEWORK', N'Negligence Per Se',                  20),
	(N'DC_LEGAL_FRAMEWORK', N'Comparative Fault',                  30),
	(N'DC_LEGAL_FRAMEWORK', N'Strict Liability',                   40),
	(N'DC_LEGAL_FRAMEWORK', N'Premises Liability',                 50),
	(N'DC_LEGAL_FRAMEWORK', N'Products Liability',                 60),
	(N'DC_LEGAL_FRAMEWORK', N'Breach of Contract',                 70),

	-- Candidate type
	(N'DC_CANDIDATE_TYPE', N'Dispositive',                         10),
	(N'DC_CANDIDATE_TYPE', N'Partial',                             20),
	(N'DC_CANDIDATE_TYPE', N'Procedural',                          30),
	(N'DC_CANDIDATE_TYPE', N'Alternative',                         40),
	(N'DC_CANDIDATE_TYPE', N'Other',                               50),

	-- Candidate state
	(N'DC_CANDIDATE_STATE', N'ACTIVE',                             10),
	(N'DC_CANDIDATE_STATE', N'INACTIVE',                           20),

	-- Burden party (moving / initial / ultimate)
	(N'DC_BURDEN_PARTY', N'Plaintiff',                             10),
	(N'DC_BURDEN_PARTY', N'Defendant',                             20),
	(N'DC_BURDEN_PARTY', N'Petitioner',                            30),
	(N'DC_BURDEN_PARTY', N'Respondent',                            40),
	(N'DC_BURDEN_PARTY', N'Moving Party',                          50),
	(N'DC_BURDEN_PARTY', N'Non-Moving Party',                      60),

	-- Decision standard
	(N'DC_DECISION_STANDARD', N'No triable issue of material fact', 10),
	(N'DC_DECISION_STANDARD', N'Preponderance of the evidence',    20),
	(N'DC_DECISION_STANDARD', N'Clear and convincing evidence',    30),
	(N'DC_DECISION_STANDARD', N'Beyond a reasonable doubt',        40),
	(N'DC_DECISION_STANDARD', N'Substantial evidence',             50),
	(N'DC_DECISION_STANDARD', N'De novo review',                   60),
	(N'DC_DECISION_STANDARD', N'Abuse of discretion',              70),

	-- Semantic validation mode
	(N'DC_SEMANTIC_MODE', N'STRICT',                               10),
	(N'DC_SEMANTIC_MODE', N'BALANCED',                             20),
	(N'DC_SEMANTIC_MODE', N'ADVISORY',                             30),

	-- Decision horizon
	(N'DC_DECISION_HORIZON', N'Current motion',                    10),
	(N'DC_DECISION_HORIZON', N'Current phase',                     20),
	(N'DC_DECISION_HORIZON', N'Entire matter',                     30)
) AS source (FieldCode, Value, SortOrder)
ON target.FieldCode = source.FieldCode AND target.Value = source.Value
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FieldCode, Value, DisplayName, SortOrder)
	VALUES (source.FieldCode, source.Value, source.Value, source.SortOrder);

GO

COMMIT TRANSACTION;
