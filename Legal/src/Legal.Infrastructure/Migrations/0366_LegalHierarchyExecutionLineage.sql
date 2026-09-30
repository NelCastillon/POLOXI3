SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0366: POLOXI Legal — Hierarchy Execution Lineage & Authority.
--
-- Persist every accepted POLOXI hierarchy RUN independently; never overwrite. The same Decision
-- Contract can legitimately produce materially different but valid hierarchies on separate runs, so
-- the persistence model stores each execution as an immutable historical artifact, preserves
-- run-specific node lineage, models APR (Atomic Proposition Resolution) atomicity, reconciles
-- semantic continuity across runs explicitly (never inferred from IDs), and promotes exactly one
-- validated execution as AUTHORITATIVE per Decision Contract decision state.
--
-- Core invariants enforced by this schema:
--   * HierarchyNodeId is NEVER a cross-run identity (nodes belong to exactly one execution).
--   * Depth never determines semantic role — NodeRoleCode is explicit.
--   * Lifecycle is split into three orthogonal dimensions: Processing / Validation / Authority.
--   * "Latest" run is not "authoritative"; authority is an explicit, filtered-unique promotion.
--   * Evidence source objects (Legal_SourceAssertion) survive reruns; bindings are run-scoped and
--     revalidated by AER whenever proposition structure materially changes.
--   * Old executions are never rewritten to resemble newer executions; snapshots are immutable.
--
-- Every table lives in the POLOXI schema, is prefixed Legal_Hierarchy*/Legal_Decision*, and carries
-- the standard base/audit fields: TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc,
-- ModifiedByUserId, IsDeleted. Mutable lifecycle rows carry ROWVERSION for optimistic concurrency.
-- Additive and fail-soft: the existing POLOXI Wide pipeline, prompts (incl. v3.21 materiality),
-- branch-state semantics, and scoring are UNCHANGED. Idempotent via CREATE guards.
--
-- FK anchors (verified): POLOXI.Legal_DecisionMatter(DecisionMatterId),
-- POLOXI.Legal_DecisionContract(DecisionContractId), POLOXI.Legal_SourceAssertion(LegalSourceAssertionId).
-- ───────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── HierarchyExecution: one complete POLOXI hierarchy RUN. Immutable historical artifact. ────────
IF OBJECT_ID(N'POLOXI.Legal_HierarchyExecution',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchyExecution
(
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_HierarchyExecution PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,
	DecisionContractId         UNIQUEIDENTIFIER NOT NULL,
	DecisionContractVersion    INT NOT NULL,

	-- Monotonic per (matter, contract, version). R101/R102/R103 are separate runs of the same input.
	RunNumber                  INT NOT NULL,
	-- INITIAL | RERUN | CONTRACT_VERSION_CHANGE | REPAIR
	RunTypeCode                NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_HierExec_RunType DEFAULT N'INITIAL',

	-- Three ORTHOGONAL lifecycle dimensions — never collapsed into one Status.
	-- Processing: QUEUED | GENERATING | GENERATED | FAILED | CANCELLED
	ProcessingStatusCode       NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierExec_Proc DEFAULT N'QUEUED',
	-- Validation: NOT_VALIDATED | VALIDATING | VALID | VALID_WITH_WARNINGS | REPAIR_REQUIRED | INVALID
	ValidationStatusCode       NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierExec_Valid DEFAULT N'NOT_VALIDATED',
	-- Authority: CANDIDATE | AUTHORITATIVE | SUPERSEDED | REJECTED
	AuthorityStatusCode        NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierExec_Auth DEFAULT N'CANDIDATE',

	-- Provenance / reproducibility.
	ModelCode                  NVARCHAR(150) NULL,
	ModelVersion               NVARCHAR(100) NULL,
	PromptCode                 NVARCHAR(100) NOT NULL,
	PromptVersion              INT NOT NULL,
	AlgorithmVersion           NVARCHAR(100) NOT NULL,
	ConfigurationVersion       NVARCHAR(100) NULL,
	-- SHA256(contract + candidates + material context + prompt version + algorithm config).
	-- NOT an idempotency key: same input may legitimately produce different runs.
	InputSnapshotHash          CHAR(64) NOT NULL,

	StartedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierExec_Started DEFAULT SYSUTCDATETIME(),
	CompletedDateUtc           DATETIME2 NULL,
	FailureCode                NVARCHAR(100) NULL,
	FailureMessage             NVARCHAR(2000) NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierExec_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_HierExec_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION NOT NULL,
	CONSTRAINT FK_Legal_HierExec_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_HierExec_Contract FOREIGN KEY (DecisionContractId) REFERENCES POLOXI.Legal_DecisionContract (DecisionContractId)
);

-- One run number per contract decision context.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Legal_HierExec_Run' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyExecution'))
	CREATE UNIQUE INDEX UX_Legal_HierExec_Run ON POLOXI.Legal_HierarchyExecution (TenantId, DecisionMatterId, DecisionContractId, DecisionContractVersion, RunNumber);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierExec_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyExecution'))
	CREATE INDEX IX_Legal_HierExec_Matter ON POLOXI.Legal_HierarchyExecution (TenantId, DecisionMatterId, IsDeleted) INCLUDE (AuthorityStatusCode, ValidationStatusCode, ProcessingStatusCode);

GO

-- ── HierarchyNode: every L1..arbitrary-depth node produced in ONE run. Depth ≠ role. ─────────────
IF OBJECT_ID(N'POLOXI.Legal_HierarchyNode',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchyNode
(
	HierarchyNodeId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_HierarchyNode PRIMARY KEY DEFAULT NEWID(),
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL,
	ParentHierarchyNodeId      UNIQUEIDENTIFIER NULL,

	Depth                      INT NOT NULL,
	DisplayOrder               INT NOT NULL CONSTRAINT DF_Legal_HierNode_Order DEFAULT 0,

	-- Structural type e.g. DIMENSION | ALTERNATIVE.
	NodeTypeCode               NVARCHAR(50) NOT NULL,
	-- Explicit semantic role — NEVER derived from Depth:
	-- GROUPING | DIMENSION | FACTOR | PROPOSITION | ATOMIC_PROPOSITION | DISCRIMINATOR
	-- (also carries POLOXI branchRole: HARD_CONSTRAINT | GUARDRAIL | PREFERENCE | CONTEXT semantics)
	NodeRoleCode               NVARCHAR(50) NOT NULL,

	Title                      NVARCHAR(500) NULL,
	Statement                  NVARCHAR(4000) NOT NULL,
	SearchText                 NVARCHAR(MAX) NULL,

	-- Execution-scoped branch state (existing POLOXI Core semantics):
	-- ACTIVE | DORMANT | RESOLVED | REOPEN | PRUNED
	BranchStateCode            NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierNode_Branch DEFAULT N'ACTIVE',

	ContinueNarrowing          BIT NOT NULL CONSTRAINT DF_Legal_HierNode_Continue DEFAULT 0,
	StopReasonCode             NVARCHAR(100) NULL,
	-- CALIBRATED confidence for interpretive branches (0..0.9 cap for ungrounded).
	Confidence                 DECIMAL(9,6) NULL,
	CapabilityCode             NVARCHAR(100) NULL,

	-- LLM_PROPOSAL | APR_DECOMPOSITION | MANUAL | REPAIR
	OriginCode                 NVARCHAR(50) NOT NULL CONSTRAINT DF_Legal_HierNode_Origin DEFAULT N'LLM_PROPOSAL',
	OriginPromptCode           NVARCHAR(100) NULL,
	OriginPromptVersion        INT NULL,
	OriginModelCode            NVARCHAR(150) NULL,
	SemanticHash               CHAR(64) NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierNode_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_HierNode_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION NOT NULL,
	CONSTRAINT FK_Legal_HierNode_Execution FOREIGN KEY (HierarchyExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId),
	CONSTRAINT FK_Legal_HierNode_Parent FOREIGN KEY (ParentHierarchyNodeId) REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId),
	CONSTRAINT CK_Legal_HierNode_Depth CHECK (Depth >= 1)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierNode_Execution' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyNode'))
	CREATE INDEX IX_Legal_HierNode_Execution ON POLOXI.Legal_HierarchyNode (HierarchyExecutionId, Depth, DisplayOrder);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierNode_Parent' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyNode'))
	CREATE INDEX IX_Legal_HierNode_Parent ON POLOXI.Legal_HierarchyNode (HierarchyExecutionId, ParentHierarchyNodeId);

GO

-- ── HierarchyEdge: non-parent structural relationships within a run. ──────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_HierarchyEdge',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchyEdge
(
	HierarchyEdgeId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_HierarchyEdge PRIMARY KEY DEFAULT NEWID(),
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL,
	FromHierarchyNodeId        UNIQUEIDENTIFIER NOT NULL,
	ToHierarchyNodeId          UNIQUEIDENTIFIER NOT NULL,
	-- REQUIRES | CONDITIONAL_ON | ALTERNATIVE_TO | DEFEATS | SUPPORTS_CANDIDATE |
	-- DEFEATS_CANDIDATE | GOVERNED_BY | DECOMPOSES_INTO
	EdgeTypeCode               NVARCHAR(50) NOT NULL,
	-- ACTIVE | DORMANT | RESOLVED
	StateCode                  NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierEdge_State DEFAULT N'ACTIVE',

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierEdge_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_HierEdge_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_HierEdge_Execution FOREIGN KEY (HierarchyExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId),
	CONSTRAINT FK_Legal_HierEdge_From FOREIGN KEY (FromHierarchyNodeId) REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId),
	CONSTRAINT FK_Legal_HierEdge_To FOREIGN KEY (ToHierarchyNodeId) REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierEdge_Execution' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyEdge'))
	CREATE INDEX IX_Legal_HierEdge_Execution ON POLOXI.Legal_HierarchyEdge (HierarchyExecutionId, EdgeTypeCode) WHERE IsDeleted = 0;

GO

-- ── PropositionResolution: APR atomicity determination per node. Stop(p)=A∧C∧F∧T∧¬M. ─────────────
IF OBJECT_ID(N'POLOXI.Legal_PropositionResolution',N'U') IS NULL
CREATE TABLE POLOXI.Legal_PropositionResolution
(
	PropositionResolutionId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PropositionResolution PRIMARY KEY DEFAULT NEWID(),
	HierarchyNodeId            UNIQUEIDENTIFIER NOT NULL,
	ResolutionRevision         INT NOT NULL CONSTRAINT DF_Legal_PropRes_Rev DEFAULT 1,

	-- ATOMIC | NEEDS_DECOMPOSITION | NON_PROPOSITION | UNDETERMINED
	AtomicityStateCode         NVARCHAR(40) NOT NULL,
	-- APR predicate components.
	ContextResolved            BIT NOT NULL CONSTRAINT DF_Legal_PropRes_Ctx DEFAULT 0,       -- C(p)
	ParentFidelityPassed       BIT NOT NULL CONSTRAINT DF_Legal_PropRes_Fid DEFAULT 0,       -- F(p)
	IndependentlyTestable      BIT NOT NULL CONSTRAINT DF_Legal_PropRes_Test DEFAULT 0,      -- T(p)
	MaterialSplitRemaining     BIT NOT NULL CONSTRAINT DF_Legal_PropRes_Split DEFAULT 0,     -- M(p)

	-- OPEN | ATOMIC | DECOMPOSED | REOPENED | RESOLVED
	ResolutionStateCode        NVARCHAR(40) NOT NULL,
	ResolutionDepth            INT NOT NULL CONSTRAINT DF_Legal_PropRes_Depth DEFAULT 0,
	-- APR_ASSESSMENT | TARGETED_LLM | MANUAL | AER_FEEDBACK
	ResolutionMethodCode       NVARCHAR(40) NOT NULL,
	StopReasonCode             NVARCHAR(100) NULL,
	ReopenedFromResolutionId   UNIQUEIDENTIFIER NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_PropRes_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_PropRes_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION NOT NULL,
	CONSTRAINT FK_Legal_PropRes_Node FOREIGN KEY (HierarchyNodeId) REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId),
	CONSTRAINT FK_Legal_PropRes_Reopen FOREIGN KEY (ReopenedFromResolutionId) REFERENCES POLOXI.Legal_PropositionResolution (PropositionResolutionId),
	CONSTRAINT UQ_Legal_PropRes_NodeRev UNIQUE (HierarchyNodeId, ResolutionRevision)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_PropRes_Node' AND object_id=OBJECT_ID(N'POLOXI.Legal_PropositionResolution'))
	CREATE INDEX IX_Legal_PropRes_Node ON POLOXI.Legal_PropositionResolution (HierarchyNodeId, ResolutionRevision) INCLUDE (AtomicityStateCode, ResolutionStateCode);

GO

-- ── DecisionConcept: OPTIONAL stable semantic identity ACROSS executions (conservative). ─────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionConcept',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionConcept
(
	DecisionConceptId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionConcept PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,
	-- FACTOR | PROPOSITION | DIMENSION | DISCRIMINATOR
	ConceptTypeCode            NVARCHAR(50) NOT NULL,
	CanonicalStatement         NVARCHAR(4000) NOT NULL,
	-- PROVISIONAL | CONFIRMED | RETIRED
	StateCode                  NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_DecConcept_State DEFAULT N'PROVISIONAL',

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecConcept_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_DecConcept_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION NOT NULL,
	CONSTRAINT FK_Legal_DecConcept_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DecConcept_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionConcept'))
	CREATE INDEX IX_Legal_DecConcept_Matter ON POLOXI.Legal_DecisionConcept (TenantId, DecisionMatterId, IsDeleted) INCLUDE (ConceptTypeCode, StateCode);

GO

-- ── HierarchyNodeConcept: run-specific node → cross-run concept mapping. Never inferred from IDs. ─
IF OBJECT_ID(N'POLOXI.Legal_HierarchyNodeConcept',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchyNodeConcept
(
	HierarchyNodeConceptId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_HierarchyNodeConcept PRIMARY KEY DEFAULT NEWID(),
	HierarchyNodeId            UNIQUEIDENTIFIER NOT NULL,
	DecisionConceptId          UNIQUEIDENTIFIER NOT NULL,
	-- SAME_AS | REFINES | DECOMPOSES | RELATED
	RelationCode               NVARCHAR(40) NOT NULL,
	-- EXACT | STABLE_CONCEPT | NORMALIZED_SEMANTIC | EMBEDDING_CANDIDATE | MANUAL
	ResolutionMethodCode       NVARCHAR(40) NOT NULL,
	-- PROVISIONAL | VERIFIED | REJECTED  (embedding similarity alone is never VERIFIED)
	VerificationStateCode      NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierNodeConcept_Verif DEFAULT N'PROVISIONAL',
	RetrievalScore             DECIMAL(9,6) NULL,
	VerifiedByUserId           UNIQUEIDENTIFIER NULL,
	VerifiedDateUtc            DATETIME2 NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierNodeConcept_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_HierNodeConcept_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_HierNodeConcept_Node FOREIGN KEY (HierarchyNodeId) REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId),
	CONSTRAINT FK_Legal_HierNodeConcept_Concept FOREIGN KEY (DecisionConceptId) REFERENCES POLOXI.Legal_DecisionConcept (DecisionConceptId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierNodeConcept_Node' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyNodeConcept'))
	CREATE INDEX IX_Legal_HierNodeConcept_Node ON POLOXI.Legal_HierarchyNodeConcept (HierarchyNodeId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierNodeConcept_Concept' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyNodeConcept'))
	CREATE INDEX IX_Legal_HierNodeConcept_Concept ON POLOXI.Legal_HierarchyNodeConcept (DecisionConceptId) WHERE IsDeleted = 0;

GO

-- ── HierarchyExecutionDiff / HierarchyNodeDiff: explicit cross-run comparison. ────────────────────
IF OBJECT_ID(N'POLOXI.Legal_HierarchyExecutionDiff',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchyExecutionDiff
(
	HierarchyExecutionDiffId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_HierarchyExecutionDiff PRIMARY KEY DEFAULT NEWID(),
	FromExecutionId            UNIQUEIDENTIFIER NOT NULL,
	ToExecutionId              UNIQUEIDENTIFIER NOT NULL,
	-- SAME_CONTRACT_RERUN | CONTRACT_VERSION_CHANGE
	ComparisonKindCode         NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_HierExecDiff_Kind DEFAULT N'SAME_CONTRACT_RERUN',
	-- PENDING | COMPLETED | FAILED
	StatusCode                 NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierExecDiff_Status DEFAULT N'PENDING',

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierExecDiff_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_HierExecDiff_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_HierExecDiff_From FOREIGN KEY (FromExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId),
	CONSTRAINT FK_Legal_HierExecDiff_To FOREIGN KEY (ToExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierExecDiff_Pair' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyExecutionDiff'))
	CREATE INDEX IX_Legal_HierExecDiff_Pair ON POLOXI.Legal_HierarchyExecutionDiff (TenantId, FromExecutionId, ToExecutionId) WHERE IsDeleted = 0;

GO

IF OBJECT_ID(N'POLOXI.Legal_HierarchyNodeDiff',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchyNodeDiff
(
	HierarchyNodeDiffId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_HierarchyNodeDiff PRIMARY KEY DEFAULT NEWID(),
	HierarchyExecutionDiffId   UNIQUEIDENTIFIER NOT NULL,
	FromHierarchyNodeId        UNIQUEIDENTIFIER NULL,
	ToHierarchyNodeId          UNIQUEIDENTIFIER NULL,
	-- UNCHANGED | SEMANTICALLY_EQUIVALENT | REFINED_FROM | DECOMPOSED_FROM | MERGED_FROM | MOVED |
	-- ROLE_CHANGED | DEPENDENCY_CHANGED | ADDED | REMOVED | NON_EQUIVALENT | AMBIGUOUS
	ChangeTypeCode             NVARCHAR(40) NOT NULL,
	-- PROVISIONAL | VERIFIED | REJECTED
	VerificationStateCode      NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierNodeDiff_Verif DEFAULT N'PROVISIONAL',

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierNodeDiff_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_HierNodeDiff_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_HierNodeDiff_Diff FOREIGN KEY (HierarchyExecutionDiffId) REFERENCES POLOXI.Legal_HierarchyExecutionDiff (HierarchyExecutionDiffId),
	CONSTRAINT FK_Legal_HierNodeDiff_From FOREIGN KEY (FromHierarchyNodeId) REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId),
	CONSTRAINT FK_Legal_HierNodeDiff_To FOREIGN KEY (ToHierarchyNodeId) REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierNodeDiff_Diff' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyNodeDiff'))
	CREATE INDEX IX_Legal_HierNodeDiff_Diff ON POLOXI.Legal_HierarchyNodeDiff (HierarchyExecutionDiffId, ChangeTypeCode) WHERE IsDeleted = 0;

GO

-- ── DecisionHierarchyAuthority: which execution is authoritative. "Latest" ≠ "authoritative". ─────
IF OBJECT_ID(N'POLOXI.Legal_DecisionHierarchyAuthority',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionHierarchyAuthority
(
	DecisionHierarchyAuthorityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionHierarchyAuthority PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,
	DecisionContractId         UNIQUEIDENTIFIER NOT NULL,
	DecisionContractVersion    INT NOT NULL,
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL,

	EffectiveDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecHierAuth_Effective DEFAULT SYSUTCDATETIME(),
	SupersededDateUtc          DATETIME2 NULL,
	-- FIRST_VALID | MANUAL_PROMOTION | QA_PROMOTION | REPAIR_PROMOTION
	AuthorityReasonCode        NVARCHAR(50) NOT NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecHierAuth_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_DecHierAuth_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecHierAuth_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_DecHierAuth_Contract FOREIGN KEY (DecisionContractId) REFERENCES POLOXI.Legal_DecisionContract (DecisionContractId),
	CONSTRAINT FK_Legal_DecHierAuth_Execution FOREIGN KEY (HierarchyExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId)
);

-- Exactly one CURRENT authoritative execution per contract decision context.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Legal_DecHierAuth_Current' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionHierarchyAuthority'))
	CREATE UNIQUE INDEX UX_Legal_DecHierAuth_Current ON POLOXI.Legal_DecisionHierarchyAuthority (TenantId, DecisionMatterId, DecisionContractId, DecisionContractVersion) WHERE SupersededDateUtc IS NULL AND IsDeleted = 0;

GO

-- ── PropositionEvidenceBinding: AER-owned run-scoped proposition → source assertion binding. ──────
IF OBJECT_ID(N'POLOXI.Legal_PropositionEvidenceBinding',N'U') IS NULL
CREATE TABLE POLOXI.Legal_PropositionEvidenceBinding
(
	PropositionEvidenceBindingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PropositionEvidenceBinding PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL,
	PropositionNodeId          UNIQUEIDENTIFIER NOT NULL,
	LegalSourceAssertionId     UNIQUEIDENTIFIER NOT NULL,

	-- DIRECT_SUPPORT | PARTIAL_SUPPORT | CORROBORATES | CONTRADICTS | QUALIFIES |
	-- ALTERNATIVE_EXPLANATION | CONTEXT_ONLY | INSUFFICIENT_TO_DETERMINE | IRRELEVANT
	RelationCode               NVARCHAR(50) NOT NULL,
	DirectnessCode             NVARCHAR(30) NULL,
	CoverageCode               NVARCHAR(30) NULL,
	LineageGroupId             UNIQUEIDENTIFIER NULL,
	-- PENDING | VERIFIED | INVALIDATED | PROPOSED_REUSE  (AER owns admission; reuse is never VERIFIED)
	VerificationStateCode      NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_PropEvBind_Verif DEFAULT N'PENDING',
	-- AER | PROPOSED_REUSE | MANUAL
	BindingOriginCode          NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_PropEvBind_Origin DEFAULT N'AER',

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_PropEvBind_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	VerifiedDateUtc            DATETIME2 NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_PropEvBind_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION NOT NULL,
	CONSTRAINT FK_Legal_PropEvBind_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_PropEvBind_Execution FOREIGN KEY (HierarchyExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId),
	CONSTRAINT FK_Legal_PropEvBind_Node FOREIGN KEY (PropositionNodeId) REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId),
	CONSTRAINT FK_Legal_PropEvBind_Assertion FOREIGN KEY (LegalSourceAssertionId) REFERENCES POLOXI.Legal_SourceAssertion (LegalSourceAssertionId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_PropEvBind_Node' AND object_id=OBJECT_ID(N'POLOXI.Legal_PropositionEvidenceBinding'))
	CREATE INDEX IX_Legal_PropEvBind_Node ON POLOXI.Legal_PropositionEvidenceBinding (PropositionNodeId, VerificationStateCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_PropEvBind_Execution' AND object_id=OBJECT_ID(N'POLOXI.Legal_PropositionEvidenceBinding'))
	CREATE INDEX IX_Legal_PropEvBind_Execution ON POLOXI.Legal_PropositionEvidenceBinding (HierarchyExecutionId, RelationCode) WHERE IsDeleted = 0;

GO

-- ── HierarchySnapshot / DecisionSnapshot: immutable freeze used by a decision. ────────────────────
IF OBJECT_ID(N'POLOXI.Legal_HierarchySnapshot',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchySnapshot
(
	HierarchySnapshotId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_HierarchySnapshot PRIMARY KEY DEFAULT NEWID(),
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL,
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,
	SnapshotHash               CHAR(64) NOT NULL,
	-- Frozen JSON projection of nodes/edges/APR/candidate deps/relevant bindings.
	SnapshotJson               NVARCHAR(MAX) NOT NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierSnap_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_HierSnap_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_HierSnap_Execution FOREIGN KEY (HierarchyExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId),
	CONSTRAINT FK_Legal_HierSnap_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierSnap_Execution' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchySnapshot'))
	CREATE INDEX IX_Legal_HierSnap_Execution ON POLOXI.Legal_HierarchySnapshot (HierarchyExecutionId) WHERE IsDeleted = 0;

GO

IF OBJECT_ID(N'POLOXI.Legal_DecisionSnapshot',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionSnapshot
(
	DecisionSnapshotId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionSnapshot PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,
	DecisionContractId         UNIQUEIDENTIFIER NOT NULL,
	DecisionContractVersion    INT NOT NULL,
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL,
	HierarchySnapshotId        UNIQUEIDENTIFIER NOT NULL,
	AlgorithmVersion           NVARCHAR(100) NOT NULL,
	ConfigurationVersion       NVARCHAR(100) NULL,
	SnapshotHash               CHAR(64) NOT NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecSnap_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_DecSnap_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecSnap_Matter FOREIGN KEY (DecisionMatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_DecSnap_Contract FOREIGN KEY (DecisionContractId) REFERENCES POLOXI.Legal_DecisionContract (DecisionContractId),
	CONSTRAINT FK_Legal_DecSnap_Execution FOREIGN KEY (HierarchyExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId),
	CONSTRAINT FK_Legal_DecSnap_HierSnap FOREIGN KEY (HierarchySnapshotId) REFERENCES POLOXI.Legal_HierarchySnapshot (HierarchySnapshotId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DecSnap_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionSnapshot'))
	CREATE INDEX IX_Legal_DecSnap_Matter ON POLOXI.Legal_DecisionSnapshot (TenantId, DecisionMatterId, CreatedDateUtc) WHERE IsDeleted = 0;

GO

-- ── OperationIdempotency: request idempotency (distinct from InputSnapshotHash run variability). ──
IF OBJECT_ID(N'POLOXI.Legal_OperationIdempotency',N'U') IS NULL
CREATE TABLE POLOXI.Legal_OperationIdempotency
(
	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	OperationCode              NVARCHAR(100) NOT NULL,
	IdempotencyKey             NVARCHAR(200) NOT NULL,
	RequestHash                CHAR(64) NOT NULL,
	ResourceId                 UNIQUEIDENTIFIER NULL,
	-- PENDING | COMPLETED | FAILED
	StatusCode                 NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_OpIdem_Status DEFAULT N'PENDING',
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_OpIdem_Created DEFAULT SYSUTCDATETIME(),
	CompletedDateUtc           DATETIME2 NULL,
	CONSTRAINT PK_Legal_OperationIdempotency PRIMARY KEY (TenantId, OperationCode, IdempotencyKey)
);

GO

-- ── Outbox: reliable transactional CDC/event propagation for authority/APR/AER/decision changes. ─
IF OBJECT_ID(N'POLOXI.Legal_HierarchyOutbox',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchyOutbox
(
	OutboxId                   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Legal_HierarchyOutbox PRIMARY KEY,
	OutboxMessageId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Legal_HierOutbox_MsgId DEFAULT NEWID(),
	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	DecisionMatterId           UNIQUEIDENTIFIER NULL,
	-- HierarchyExecutionStarted | HierarchyExecutionCompleted | HierarchyValidationCompleted |
	-- HierarchyNodeRegistered | HierarchyNodeDecomposed | HierarchyNodeReopened | HierarchyNodeResolved |
	-- APRResolutionChanged | HierarchyDiffCompleted | HierarchyAuthorityPromoted |
	-- HierarchyAuthoritySuperseded | EvidenceBindingInvalidated | EvidenceBindingRevalidationRequested |
	-- CandidateDependencyChanged | DecisionReevaluationRequested | DecisionSnapshotCreated
	EventTypeCode              NVARCHAR(80) NOT NULL,
	AggregateTypeCode          NVARCHAR(60) NOT NULL,
	AggregateId                UNIQUEIDENTIFIER NULL,
	CorrelationId              UNIQUEIDENTIFIER NULL,
	PayloadJson                NVARCHAR(MAX) NOT NULL,
	-- PENDING | PROCESSING | PROCESSED | FAILED
	StatusCode                 NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_HierOutbox_Status DEFAULT N'PENDING',
	AttemptCount               INT NOT NULL CONSTRAINT DF_Legal_HierOutbox_Attempts DEFAULT 0,
	AvailableDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierOutbox_Available DEFAULT SYSUTCDATETIME(),
	ProcessedDateUtc           DATETIME2 NULL,
	LastError                  NVARCHAR(2000) NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierOutbox_Created DEFAULT SYSUTCDATETIME()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierOutbox_Dispatch' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyOutbox'))
	CREATE INDEX IX_Legal_HierOutbox_Dispatch ON POLOXI.Legal_HierarchyOutbox (StatusCode, AvailableDateUtc) INCLUDE (EventTypeCode, TenantId);

GO

COMMIT TRANSACTION;
GO
