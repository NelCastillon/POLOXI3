SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 0399: POLOXI Legal — LPI Document-Retrieval proposition integration (Phase 2).
--
-- Turns RETRIEVED, attorney-reviewed atomic propositions into the SAME insertion funnel the manual
-- Attorney Decision Input (ADI) path uses. Retrieval supplies propositions; it NEVER scores candidates,
-- picks winners, or initializes interpolation. POLOXI Core remains the sole competition authority and
-- LpiScoreInitializer provides only an advisory, attorney-reviewed initial value (disabled by default).
--
-- Four additive tables (all with standard base/audit fields: TenantId, CreatedDateUtc, CreatedByUserId,
-- ModifiedDateUtc, ModifiedByUserId, IsDeleted). Reuses existing document version/passage/source rows
-- (migrations 0291/0353/0357) and the authoritative hierarchy (0366). Idempotent via OBJECT_ID guards.
--
--   1. Legal_RetrievedProposition      — one extracted atomic proposition + exact source provenance +
--                                         attribution/assertion type + review lifecycle state.
--   2. Legal_PropositionNodeLink        — a proposed placement (node + qualitative relationship +
--                                         rationale + optional reviewed interpolation position).
--   3. Legal_LpiCalculation             — the advisory LPI initializer inputs/formula/assigned values.
--   4. Legal_PropositionIntegrationOp   — add/revise/withdraw operation, actor, reviewer, idempotency,
--                                         superseded links, and reassessment linkage.
--
-- STRICT INVARIANTS:
--   * No numeric outcome-support column here. Relationship is qualitative only.
--   * CONTEXT_ONLY links carry no numeric support contribution.
--   * IdempotencyKey derives from stable operation identity, not raw proposition text.
--   * Revisions/withdrawals SUPERSEDE prior rows after review; history is never overwritten.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── 1. RetrievedProposition: an extracted atomic proposition with immutable source provenance. ─────
IF OBJECT_ID(N'POLOXI.Legal_RetrievedProposition',N'U') IS NULL
CREATE TABLE POLOXI.Legal_RetrievedProposition
(
	RetrievedPropositionId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_RetrievedProposition PRIMARY KEY DEFAULT NEWID(),

	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,

	-- Immutable retrieval provenance (reuses existing document version + passage rows).
	LegalDocumentVersionId     UNIQUEIDENTIFIER NOT NULL,
	LegalDocumentPassageId     UNIQUEIDENTIFIER NULL,
	-- Exact source location WITHIN the version (page/char-range/anchor). Never inferred.
	SourceLocator              NVARCHAR(400) NOT NULL,
	SourceText                 NVARCHAR(MAX) NOT NULL,

	-- The atomic proposition as extracted. Attribution/negation/amounts/dates preserved verbatim.
	PropositionText            NVARCHAR(MAX) NOT NULL,

	-- How the source expresses it: Asserts | Reports | Documents | StatesLaw | Infers.
	AssertionTypeCode          NVARCHAR(30) NOT NULL,
	-- Who/what the assertion is attributed to (e.g. "patient", carrier adjuster, court). Null = source.
	AttributedTo               NVARCHAR(400) NULL,
	-- Effectivity instant for time-scoped propositions. Null = always.
	EffectiveAtUtc             DATETIME2 NULL,

	-- Which complementary retrieval mode produced it: ConditionDirected | DocumentDirected.
	RetrievalModeCode          NVARCHAR(30) NOT NULL,

	-- Review lifecycle: Extracted | PlacementProposed | ReviewRequired | NeedsHierarchyReview |
	-- Accepted | Rejected | Superseded | Withdrawn. Failed validation PRESERVES the row + reason.
	StateCode                  NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_RetrProp_State DEFAULT N'Extracted',
	StateReason                NVARCHAR(2000) NULL,

	-- Model/prompt provenance for the extraction stage (advisory until attorney-reviewed).
	ExtractedByModel           NVARCHAR(150) NULL,
	ExtractionPromptRunId      NVARCHAR(100) NULL,

	-- When this proposition was superseded by a revision (points at the replacement). Null = active.
	SupersededByPropositionId  UNIQUEIDENTIFIER NULL,

	-- Standard base/audit fields.
	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_RetrProp_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_RetrProp_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION
);

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Legal_RetrProp_Matter' AND object_id = OBJECT_ID(N'POLOXI.Legal_RetrievedProposition'))
	CREATE INDEX IX_Legal_RetrProp_Matter ON POLOXI.Legal_RetrievedProposition (TenantId, DecisionMatterId, StateCode) WHERE IsDeleted = 0;

GO

-- ── 2. PropositionNodeLink: a proposed placement with a qualitative relationship. ──────────────────
IF OBJECT_ID(N'POLOXI.Legal_PropositionNodeLink',N'U') IS NULL
CREATE TABLE POLOXI.Legal_PropositionNodeLink
(
	PropositionNodeLinkId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PropositionNodeLink PRIMARY KEY DEFAULT NEWID(),

	RetrievedPropositionId     UNIQUEIDENTIFIER NOT NULL,
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,

	-- Target hierarchy node (at any depth) and the revision it was proposed against (stale guard).
	HierarchyRevision          BIGINT NOT NULL,
	TargetNodeId               UNIQUEIDENTIFIER NOT NULL,
	LeftNeighborId             UNIQUEIDENTIFIER NULL,
	RightNeighborId            UNIQUEIDENTIFIER NULL,

	-- Explicit, reviewed interpolation position between comparable values — NEVER inferred from
	-- display order. Null unless a reviewed interpolation position was set.
	PlacementFraction          DECIMAL(9,6) NULL,

	-- Qualitative relationship only: SUPPORTS | CONTRADICTS | QUALIFIES | CONTEXT_ONLY.
	-- CONTEXT_ONLY carries NO numeric support contribution.
	RelationshipCode           NVARCHAR(30) NOT NULL,
	Rationale                  NVARCHAR(2000) NULL,

	-- Review disposition for this specific placement (a proposition may have multiple placements).
	StateCode                  NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_PropLink_State DEFAULT N'PlacementProposed',

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_PropLink_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_PropLink_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION,

	CONSTRAINT FK_Legal_PropLink_Proposition FOREIGN KEY (RetrievedPropositionId)
		REFERENCES POLOXI.Legal_RetrievedProposition (RetrievedPropositionId)
);

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Legal_PropLink_Proposition' AND object_id = OBJECT_ID(N'POLOXI.Legal_PropositionNodeLink'))
	CREATE INDEX IX_Legal_PropLink_Proposition ON POLOXI.Legal_PropositionNodeLink (TenantId, RetrievedPropositionId) WHERE IsDeleted = 0;

GO

-- ── 3. LpiCalculation: the advisory initializer inputs + formula version + assigned values. ────────
IF OBJECT_ID(N'POLOXI.Legal_LpiCalculation',N'U') IS NULL
CREATE TABLE POLOXI.Legal_LpiCalculation
(
	LpiCalculationId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_LpiCalculation PRIMARY KEY DEFAULT NEWID(),

	PropositionNodeLinkId      UNIQUEIDENTIFIER NOT NULL,
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,

	-- Formula provenance (e.g. LPI_INIT_V1) and the knobs used.
	FormulaVersion             NVARCHAR(40) NOT NULL,
	Alpha                      DECIMAL(9,6) NOT NULL,
	Lambda                     DECIMAL(9,6) NOT NULL,
	MethodCode                 NVARCHAR(40) NOT NULL,     -- Disabled|LocalOnly|Blended|AncestorOnlyReviewRequired|Uninitialized

	-- Inputs/outputs for a single reproducible original-vs-proposed example.
	LocalBaseline              DECIMAL(9,4) NULL,         -- B_c
	AncestorContext            DECIMAL(9,4) NULL,         -- A_c
	InitialScore               DECIMAL(9,4) NULL,         -- S (null when Uninitialized or CONTEXT_ONLY)
	HasScore                   BIT NOT NULL CONSTRAINT DF_Legal_LpiCalc_HasScore DEFAULT 0,
	AncestorsUsedJson          NVARCHAR(MAX) NULL,        -- exactly which ancestors/versions fed A_c
	Explanation                NVARCHAR(2000) NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_LpiCalc_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_LpiCalc_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION,

	CONSTRAINT FK_Legal_LpiCalc_Link FOREIGN KEY (PropositionNodeLinkId)
		REFERENCES POLOXI.Legal_PropositionNodeLink (PropositionNodeLinkId)
);

GO

-- ── 4. PropositionIntegrationOp: add/revise/withdraw operation with idempotency + reassessment. ────
IF OBJECT_ID(N'POLOXI.Legal_PropositionIntegrationOp',N'U') IS NULL
CREATE TABLE POLOXI.Legal_PropositionIntegrationOp
(
	PropositionIntegrationOpId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PropositionIntegrationOp PRIMARY KEY DEFAULT NEWID(),

	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,

	-- Which entry point produced this operation: Retrieval | Manual (same service, shared contract).
	OriginCode                 NVARCHAR(30) NOT NULL,
	-- Operation kind: Add | Revise | Withdraw.
	OperationCode              NVARCHAR(20) NOT NULL,

	RetrievedPropositionId     UNIQUEIDENTIFIER NULL,
	-- The committed decision node produced by the shared insertion (ADI node). Null on reject/withdraw.
	CommittedDecisionNodeId    UNIQUEIDENTIFIER NULL,

	-- Identifying integration context revisions for reproducibility.
	DecisionContractRevision   BIGINT NULL,
	CandidateSetRevision       BIGINT NULL,
	HierarchyRevision          BIGINT NULL,
	SourceDocumentVersionId    UNIQUEIDENTIFIER NULL,
	ReviewerUserId             UNIQUEIDENTIFIER NULL,
	ScoringConfigurationVersion NVARCHAR(100) NULL,

	-- Idempotency: derived from STABLE operation identity, not raw text. Unique per tenant.
	IdempotencyKey             NVARCHAR(200) NOT NULL,

	-- Superseded propositions (JSON array of ids) for revise/withdraw audit. History never overwritten.
	SupersededPropositionIdsJson NVARCHAR(MAX) NULL,

	-- Reassessment linkage: the change event the shared service enqueued and terminal status.
	-- StatusCode: Applied | Idempotent | Rejected | EvaluationPending | EvaluationFailed.
	ChangeEventId              UNIQUEIDENTIFIER NULL,
	StatusCode                 NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_PropOp_Status DEFAULT N'Applied',
	Explanation                NVARCHAR(2000) NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_PropOp_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_PropOp_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION
);

GO

-- Idempotency guard: one operation per (tenant, idempotency key).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Legal_PropOp_Idempotency' AND object_id = OBJECT_ID(N'POLOXI.Legal_PropositionIntegrationOp'))
	CREATE UNIQUE INDEX UX_Legal_PropOp_Idempotency ON POLOXI.Legal_PropositionIntegrationOp (TenantId, IdempotencyKey);

GO

COMMIT TRANSACTION;
