SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Verified Decision Signals — lightweight decision-support persistence (frozen slice-1).
--
-- One table storing verifiable support signals ("this material fact/authority, if verified, moves
-- the outcome by this much"), keyed by decision session and authoritative branch/candidate lineage.
-- This never duplicates dependency-graph edges; scoring stays in the Candidate × Branch engine.
-- Carries the standard base/audit fields.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionSupportSignal',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionSupportSignal
(
	SignalId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionSupportSignal PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId     UNIQUEIDENTIFIER NOT NULL,
	MatterId              UNIQUEIDENTIFIER NULL,
	[Statement]           NVARCHAR(MAX) NOT NULL,
	NormalizedStatement   NVARCHAR(MAX) NOT NULL,
	OriginCode            NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionSupportSignal_Origin DEFAULT N'LlmGenerated',
	VerificationStateCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionSupportSignal_State DEFAULT N'Unverified',
	RequiresVerification  BIT NOT NULL CONSTRAINT DF_Legal_DecisionSupportSignal_Requires DEFAULT 1,
	VerificationStrength  DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionSupportSignal_Strength DEFAULT 0,
	DecisionImpact        DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionSupportSignal_Impact DEFAULT 0,
	SourceBranchId        UNIQUEIDENTIFIER NULL,
	SourceCandidateId     UNIQUEIDENTIFIER NULL,
	ProposedByModel       NVARCHAR(100) NULL,
	PromptRunId           NVARCHAR(120) NULL,
	VerificationReason    NVARCHAR(MAX) NULL,
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionSupportSignal_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_DecisionSupportSignal_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Legal_DecisionSupportSignal_SessionId',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionSupportSignal_SessionId ON POLOXI.Legal_DecisionSupportSignal (DecisionSessionId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_DecisionSupportSignal_MatterId',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionSupportSignal_MatterId ON POLOXI.Legal_DecisionSupportSignal (MatterId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_DecisionSupportSignal_State',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionSupportSignal_State ON POLOXI.Legal_DecisionSupportSignal (VerificationStateCode) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_DecisionSupportSignal_SourceBranchId',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionSupportSignal_SourceBranchId ON POLOXI.Legal_DecisionSupportSignal (SourceBranchId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Legal_DecisionSupportSignal_SourceCandidateId',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionSupportSignal_SourceCandidateId ON POLOXI.Legal_DecisionSupportSignal (SourceCandidateId) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
