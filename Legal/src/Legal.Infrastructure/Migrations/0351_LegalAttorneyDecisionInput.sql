SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Attorney Decision Input (ADI) / "Human Intelligence"
-- Native extension of the POLOXI decision hierarchy. Authorized attorneys may propose, place, assess,
-- challenge and (via governance) approve decision nodes. POLOXI remains the single authoritative
-- evaluator; nothing here computes or overrides scoring. All operational/config data is DB-backed.
--
-- Every table lives in the POLOXI schema, is prefixed Legal_Decision*/Legal_Attorney*, and carries the
-- standard base/audit fields: TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc,
-- ModifiedByUserId, IsDeleted. Assessment tables additionally carry a rowversion for optimistic
-- concurrency (§18/§23 of the ADI spec). Safe to re-run (idempotent CREATE guards).
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Canonical decision node: per-matter hierarchy proposed by LLM or attorneys (§2, §6, §7). ──────
IF OBJECT_ID(N'POLOXI.Legal_DecisionNode',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionNode
(
	DecisionNodeId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionNode PRIMARY KEY DEFAULT NEWID(),
	MatterId            UNIQUEIDENTIFIER NOT NULL,
	ParentNodeId        UNIQUEIDENTIFIER NULL,
	CanonicalKey        NVARCHAR(200) NOT NULL,
	-- Candidate (L1) | Factor (L2) | Proposition (L3+)
	NodeKindCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionNode_Kind DEFAULT N'Proposition',
	NodeLevel           INT NOT NULL CONSTRAINT DF_Legal_DecisionNode_Level DEFAULT 3,
	NodeText            NVARCHAR(2000) NOT NULL,
	-- LlmProposed | AttorneySupplied | EvidenceDerived | SystemDerived | Imported
	OriginCode          NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionNode_Origin DEFAULT N'LlmProposed',
	-- Stable placement ordering under the parent (fractional/lexicographic key — §7).
	PlacementKey        NVARCHAR(200) NULL,
	NodeVersion         BIGINT NOT NULL CONSTRAINT DF_Legal_DecisionNode_Version DEFAULT 1,
	-- Proposed | Validating | Valid | Compound | Ambiguous | Duplicate | SemanticDrift | InvalidPremise | Unresolved | Rejected
	StructuralStateCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionNode_Structural DEFAULT N'Proposed',
	-- NotEvaluated | Unresolved | Supported | PartiallySupported | Contradicted | Mixed | InsufficientToDetermine
	EvidenceStateCode   NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionNode_Evidence DEFAULT N'NotEvaluated',
	-- NotRequired | Pending | Verified | PartiallyVerified | Unverified | Inapplicable | Rejected
	AuthorityStateCode  NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionNode_Authority DEFAULT N'NotRequired',
	-- Final evaluated POLOXI value (owned by POLOXI; NULL until authoritatively evaluated). Display only here.
	EvaluatedValue      DECIMAL(9,4) NULL,
	TenantId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionNode_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_DecisionNode_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_DecisionNode_Canonical UNIQUE (TenantId, MatterId, CanonicalKey)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DecisionNode_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionNode'))
	CREATE INDEX IX_Legal_DecisionNode_Matter ON POLOXI.Legal_DecisionNode (TenantId, MatterId, IsDeleted) INCLUDE (ParentNodeId, NodeLevel, PlacementKey);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DecisionNode_Parent' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionNode'))
	CREATE INDEX IX_Legal_DecisionNode_Parent ON POLOXI.Legal_DecisionNode (ParentNodeId, IsDeleted);

GO

-- ── Attorney relative assessment: independent professional-judgment position per node (§4, §5, §6). ─
IF OBJECT_ID(N'POLOXI.Legal_AttorneyRelativeAssessment',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AttorneyRelativeAssessment
(
	AssessmentId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AttorneyRelativeAssessment PRIMARY KEY DEFAULT NEWID(),
	MatterId              UNIQUEIDENTIFIER NOT NULL,
	DecisionNodeId        UNIQUEIDENTIFIER NOT NULL,
	AttorneyUserId        UNIQUEIDENTIFIER NOT NULL,
	ConfirmedValue        DECIMAL(9,4) NOT NULL,
	SuggestedMidpoint     DECIMAL(9,4) NULL,
	PreviousSiblingId     UNIQUEIDENTIFIER NULL,
	PreviousSiblingValue  DECIMAL(9,4) NULL,
	NextSiblingId         UNIQUEIDENTIFIER NULL,
	NextSiblingValue      DECIMAL(9,4) NULL,
	DecisionSnapshotId    UNIQUEIDENTIFIER NULL,
	ScoringConfigVersion  NVARCHAR(100) NULL,
	-- Method used to derive ConfirmedValue (e.g. Midpoint | Adjusted | BoundedEntry | PendingAssessment).
	MethodCode            NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_ARA_Method DEFAULT N'Midpoint',
	Rationale             NVARCHAR(2000) NULL,
	-- Draft | Submitted | Current | Different | Superseded | Withdrawn | Rejected | ApprovedForMatter
	StatusCode            NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_ARA_Status DEFAULT N'Draft',
	AssessmentVersion     BIGINT NOT NULL CONSTRAINT DF_Legal_ARA_Version DEFAULT 1,
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_ARA_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_ARA_IsDeleted DEFAULT 0,
	RowVersion            ROWVERSION,
	CONSTRAINT FK_Legal_ARA_Node FOREIGN KEY (DecisionNodeId) REFERENCES POLOXI.Legal_DecisionNode (DecisionNodeId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_ARA_Node' AND object_id=OBJECT_ID(N'POLOXI.Legal_AttorneyRelativeAssessment'))
	CREATE INDEX IX_Legal_ARA_Node ON POLOXI.Legal_AttorneyRelativeAssessment (TenantId, MatterId, DecisionNodeId, IsDeleted) INCLUDE (AttorneyUserId, StatusCode, ConfirmedValue);

GO

-- ── Approved matter assessment: at most one active approved assessment per node/scoring context (§5). ─
IF OBJECT_ID(N'POLOXI.Legal_ApprovedMatterAssessment',N'U') IS NULL
CREATE TABLE POLOXI.Legal_ApprovedMatterAssessment
(
	ApprovalId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ApprovedMatterAssessment PRIMARY KEY DEFAULT NEWID(),
	MatterId            UNIQUEIDENTIFIER NOT NULL,
	DecisionNodeId      UNIQUEIDENTIFIER NOT NULL,
	AssessmentId        UNIQUEIDENTIFIER NOT NULL,
	ApprovedByUserId    UNIQUEIDENTIFIER NOT NULL,
	-- LeadAttorneyApproves | MatterOwnerApproves | DesignatedReviewerApproves | ExplicitConsensus
	GovernancePolicyCode NVARCHAR(60) NOT NULL,
	DecisionSnapshotId  UNIQUEIDENTIFIER NULL,
	ApprovalVersion     BIGINT NOT NULL CONSTRAINT DF_Legal_AMA_Version DEFAULT 1,
	-- Only one active (IsDeleted=0) approved assessment per node/scoring context is enforced below.
	IsActive            BIT NOT NULL CONSTRAINT DF_Legal_AMA_IsActive DEFAULT 1,
	TenantId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_AMA_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_AMA_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_AMA_Node FOREIGN KEY (DecisionNodeId) REFERENCES POLOXI.Legal_DecisionNode (DecisionNodeId),
	CONSTRAINT FK_Legal_AMA_Assessment FOREIGN KEY (AssessmentId) REFERENCES POLOXI.Legal_AttorneyRelativeAssessment (AssessmentId)
);

-- At most one active approved assessment per node (filtered unique index — §5 invariant).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Legal_AMA_ActiveNode' AND object_id=OBJECT_ID(N'POLOXI.Legal_ApprovedMatterAssessment'))
	CREATE UNIQUE INDEX UX_Legal_AMA_ActiveNode ON POLOXI.Legal_ApprovedMatterAssessment (TenantId, MatterId, DecisionNodeId) WHERE IsActive=1 AND IsDeleted=0;

GO

-- ── Attorney decision challenge: disputes against a node or relationship (§3, §11 CHALLENGED_BY). ──
IF OBJECT_ID(N'POLOXI.Legal_AttorneyDecisionChallenge',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AttorneyDecisionChallenge
(
	ChallengeId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AttorneyDecisionChallenge PRIMARY KEY DEFAULT NEWID(),
	MatterId            UNIQUEIDENTIFIER NOT NULL,
	DecisionNodeId      UNIQUEIDENTIFIER NOT NULL,
	AttorneyUserId      UNIQUEIDENTIFIER NOT NULL,
	-- Structural | Placement | Duplicate | Premise | Coverage | Relationship | Evidence | Authority
	ChallengeTypeCode   NVARCHAR(60) NOT NULL,
	ChallengeText       NVARCHAR(2000) NOT NULL,
	-- Open | UnderReview | Accepted | Rejected | Withdrawn | Resolved
	StatusCode          NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_ADC_Status DEFAULT N'Open',
	ResolvedByUserId    UNIQUEIDENTIFIER NULL,
	ResolvedDateUtc     DATETIME2 NULL,
	TenantId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_ADC_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_ADC_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_ADC_Node FOREIGN KEY (DecisionNodeId) REFERENCES POLOXI.Legal_DecisionNode (DecisionNodeId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_ADC_Node' AND object_id=OBJECT_ID(N'POLOXI.Legal_AttorneyDecisionChallenge'))
	CREATE INDEX IX_Legal_ADC_Node ON POLOXI.Legal_AttorneyDecisionChallenge (TenantId, MatterId, DecisionNodeId, IsDeleted) INCLUDE (AttorneyUserId, ChallengeTypeCode, StatusCode);

COMMIT TRANSACTION;
