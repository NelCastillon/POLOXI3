SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Epistemic Authority Layer — EA-1 persistence (§32, §33).
--
-- Three tables governing whether an LLM-generated proposition may affect authoritative POLOXI state.
-- Graph relationships remain in the existing dependency-graph tables; these never duplicate edges.
-- All tables carry the standard base/audit fields.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

-- ── Authoritative claim proposition ────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_ClaimProposition',N'U') IS NULL
CREATE TABLE POLOXI.Legal_ClaimProposition
(
	ClaimId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ClaimProposition PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	MatterId             UNIQUEIDENTIFIER NULL,
	[Text]               NVARCHAR(MAX) NOT NULL,
	NormalizedText       NVARCHAR(MAX) NOT NULL,
	ClaimTypeCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Type DEFAULT N'Other',
	ClaimOriginCode      NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Origin DEFAULT N'LlmGenerated',
	VerificationStateCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_State DEFAULT N'Proposed',
	DecisionAuthorityCode NVARCHAR(20) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Authority DEFAULT N'None',
	VerificationStrength DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Strength DEFAULT 0,
	Materiality          DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Materiality DEFAULT 0,
	DecisionImpact       DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Impact DEFAULT 0,
	Discrimination       DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Discrimination DEFAULT 0,
	Uncertainty          DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Uncertainty DEFAULT 0,
	IsEssential          BIT NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Essential DEFAULT 0,
	SourceBranchId       UNIQUEIDENTIFIER NULL,
	SourceCandidateId    UNIQUEIDENTIFIER NULL,
	ProposedByModel      NVARCHAR(100) NULL,
	PromptRunId          NVARCHAR(120) NULL,
	VerificationReason   NVARCHAR(MAX) NULL,
	[Version]            INT NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Version DEFAULT 1,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_ClaimProposition_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_ClaimProposition_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Legal_ClaimProposition_SessionId',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimProposition_SessionId ON POLOXI.Legal_ClaimProposition (DecisionSessionId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_ClaimProposition_MatterId',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimProposition_MatterId ON POLOXI.Legal_ClaimProposition (MatterId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_ClaimProposition_State',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimProposition_State ON POLOXI.Legal_ClaimProposition (VerificationStateCode) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_ClaimProposition_SourceBranchId',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimProposition_SourceBranchId ON POLOXI.Legal_ClaimProposition (SourceBranchId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_ClaimProposition_SourceCandidateId',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimProposition_SourceCandidateId ON POLOXI.Legal_ClaimProposition (SourceCandidateId) WHERE IsDeleted = 0;

-- ── Claim support edge (evidence/authority → claim) ─────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_ClaimSupport',N'U') IS NULL
CREATE TABLE POLOXI.Legal_ClaimSupport
(
	ClaimSupportId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ClaimSupport PRIMARY KEY DEFAULT NEWID(),
	ClaimId              UNIQUEIDENTIFIER NOT NULL,
	EvidenceId           UNIQUEIDENTIFIER NULL,
	AuthorityId          UNIQUEIDENTIFIER NULL,
	RelationshipCode     NVARCHAR(20) NOT NULL CONSTRAINT DF_Legal_ClaimSupport_Rel DEFAULT N'Supports',
	Strength             DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_ClaimSupport_Strength DEFAULT 0,
	IndependentlyVerified BIT NOT NULL CONSTRAINT DF_Legal_ClaimSupport_Independent DEFAULT 0,
	SourceLocation       NVARCHAR(400) NULL,
	VerificationReason   NVARCHAR(MAX) NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_ClaimSupport_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_ClaimSupport_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_ClaimSupport_Claim FOREIGN KEY (ClaimId) REFERENCES POLOXI.Legal_ClaimProposition (ClaimId)
);

IF OBJECT_ID(N'IX_Legal_ClaimSupport_ClaimId',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimSupport_ClaimId ON POLOXI.Legal_ClaimSupport (ClaimId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_ClaimSupport_EvidenceId',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimSupport_EvidenceId ON POLOXI.Legal_ClaimSupport (EvidenceId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_ClaimSupport_AuthorityId',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimSupport_AuthorityId ON POLOXI.Legal_ClaimSupport (AuthorityId) WHERE IsDeleted = 0;

-- ── Idempotent verification-state transition event ──────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_ClaimVerificationEvent',N'U') IS NULL
CREATE TABLE POLOXI.Legal_ClaimVerificationEvent
(
	EventId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ClaimVerificationEvent PRIMARY KEY DEFAULT NEWID(),
	ClaimId              UNIQUEIDENTIFIER NOT NULL,
	PreviousStateCode    NVARCHAR(40) NOT NULL,
	NewStateCode         NVARCHAR(40) NOT NULL,
	Reason               NVARCHAR(MAX) NOT NULL,
	EvidenceIdsJson      NVARCHAR(MAX) NULL,
	AuthorityIdsJson     NVARCHAR(MAX) NULL,
	IdempotencyKey       NVARCHAR(200) NOT NULL,
	OccurredAt           DATETIMEOFFSET NOT NULL CONSTRAINT DF_Legal_ClaimVerificationEvent_Occurred DEFAULT SYSDATETIMEOFFSET(),
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_ClaimVerificationEvent_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_ClaimVerificationEvent_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_ClaimVerificationEvent_Claim FOREIGN KEY (ClaimId) REFERENCES POLOXI.Legal_ClaimProposition (ClaimId),
	CONSTRAINT UX_Legal_ClaimVerificationEvent_IdempotencyKey UNIQUE (IdempotencyKey)
);

IF OBJECT_ID(N'IX_Legal_ClaimVerificationEvent_ClaimId',N'IX') IS NULL
	CREATE INDEX IX_Legal_ClaimVerificationEvent_ClaimId ON POLOXI.Legal_ClaimVerificationEvent (ClaimId) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
