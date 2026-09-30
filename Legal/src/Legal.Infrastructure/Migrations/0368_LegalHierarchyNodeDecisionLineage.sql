SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 0368: POLOXI Legal — Hierarchy Node → Decision Lineage (durable channel-to-decision bridge).
--
-- THE GAP THIS CLOSES: a channel contribution (0367) binds to a run-scoped hierarchy node
-- (HierarchyExecutionId + HierarchyNodeId from 0366). POLOXI candidate competition, however, runs on
-- the SEPARATE decision model (0209): Legal_DecisionSession → Legal_DecisionBranch /
-- Legal_DecisionCandidate. There is NO column anywhere linking a hierarchy node to an authoritative
-- POLOXI branch/candidate — HierarchyNodeId is deliberately NOT a POLOXI BranchId/CandidateId. This
-- table is the ONE durable, DB-as-source-of-truth place that mapping lives, so that verified
-- contributions can be projected into typed recompetition WITHOUT the adapter ever guessing lineage.
--
-- STRICT INVARIANTS:
--   * Run-scoped: a row binds exactly one (HierarchyExecutionId, HierarchyNodeId) to one decision
--     session, plus OPTIONAL branch and/or candidate ids. HierarchyNodeId is never a cross-run
--     identity, so both execution and node are stored and FK-anchored (mirrors 0367).
--   * Additive / fail-soft: absence of a mapping simply yields no lineage (contribution does not
--     project). It NEVER blocks the existing POLOXI Wide pipeline, CDC/CDI tables, or scoring.
--   * Qualitative bridge only: NO numeric score/delta column — POLOXI Wide2 remains the SOLE owner of
--     candidate competition. This table only says "node N maps to branch B / candidate C".
--   * Optional branch/candidate: a node may resolve to a branch, a candidate, both, or neither
--     (neither = context node with no ranking effect). At least one of session/branch/candidate is
--     always meaningful; DecisionSessionId is required to scope the mapping to a decision run.
--
-- Standard base/audit fields on every row: TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc,
-- ModifiedByUserId, IsDeleted; RowVersion for optimistic concurrency. Idempotent via CREATE guards.
--
-- FK anchors (verified): POLOXI.Legal_HierarchyExecution(HierarchyExecutionId),
-- POLOXI.Legal_HierarchyNode(HierarchyNodeId), POLOXI.Legal_DecisionMatter(DecisionMatterId),
-- POLOXI.Legal_DecisionSession(DecisionSessionId), POLOXI.Legal_DecisionBranch(DecisionBranchId),
-- POLOXI.Legal_DecisionCandidate(DecisionCandidateId).
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── HierarchyNodeDecisionLineage: durable map from a run-scoped hierarchy node to POLOXI decision ids. ──
IF OBJECT_ID(N'POLOXI.Legal_HierarchyNodeDecisionLineage',N'U') IS NULL
CREATE TABLE POLOXI.Legal_HierarchyNodeDecisionLineage
(
	HierarchyNodeDecisionLineageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_HierNodeDecisionLineage PRIMARY KEY DEFAULT NEWID(),

	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,

	-- Run-scoped authoritative source node. Both required (HierarchyNodeId is never cross-run identity).
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL,
	HierarchyNodeId            UNIQUEIDENTIFIER NOT NULL,

	-- The POLOXI decision run this node participates in (0209 decision root). Required.
	DecisionSessionId          UNIQUEIDENTIFIER NOT NULL,

	-- Authoritative POLOXI targets. Either, both, or neither may be present:
	--   * DecisionBranchId    → node maps to a decision branch (branch-level signal).
	--   * DecisionCandidateId → node maps to a candidate (candidate-level signal).
	-- Neither present = context/structural node with no ranking effect.
	DecisionBranchId           UNIQUEIDENTIFIER NULL,
	DecisionCandidateId        UNIQUEIDENTIFIER NULL,

	-- How this mapping was established (auditable provenance of the lineage itself, not a score):
	-- SYSTEM | HIERARCHY_PROJECTION | MANUAL | IMPORTED
	LineageSourceCode          NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_HierNodeLineage_Source DEFAULT N'SYSTEM',

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_HierNodeLineage_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_HierNodeLineage_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION NOT NULL,

	CONSTRAINT FK_Legal_HierNodeLineage_Matter    FOREIGN KEY (DecisionMatterId)     REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_HierNodeLineage_Execution FOREIGN KEY (HierarchyExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId),
	CONSTRAINT FK_Legal_HierNodeLineage_Node      FOREIGN KEY (HierarchyNodeId)       REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId),
	CONSTRAINT FK_Legal_HierNodeLineage_Session   FOREIGN KEY (DecisionSessionId)     REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId),
	CONSTRAINT FK_Legal_HierNodeLineage_Branch    FOREIGN KEY (DecisionBranchId)      REFERENCES POLOXI.Legal_DecisionBranch (DecisionBranchId),
	CONSTRAINT FK_Legal_HierNodeLineage_Candidate FOREIGN KEY (DecisionCandidateId)   REFERENCES POLOXI.Legal_DecisionCandidate (DecisionCandidateId)
);

GO

-- Primary query path: "resolve lineage for a contribution's node" (drives the lineage resolver).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierNodeLineage_Node' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyNodeDecisionLineage'))
	CREATE INDEX IX_Legal_HierNodeLineage_Node ON POLOXI.Legal_HierarchyNodeDecisionLineage (TenantId, HierarchyExecutionId, HierarchyNodeId, IsDeleted) INCLUDE (DecisionSessionId, DecisionBranchId, DecisionCandidateId);

GO

-- Secondary: "all node lineage for a decision session / matter" (reverse lookups, provenance UI).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_HierNodeLineage_Session' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyNodeDecisionLineage'))
	CREATE INDEX IX_Legal_HierNodeLineage_Session ON POLOXI.Legal_HierarchyNodeDecisionLineage (TenantId, DecisionSessionId, IsDeleted) INCLUDE (HierarchyNodeId, DecisionBranchId, DecisionCandidateId);

GO

-- One active lineage row per (execution, node, session, branch, candidate) target — prevents dup mappings.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Legal_HierNodeLineage_Target' AND object_id=OBJECT_ID(N'POLOXI.Legal_HierarchyNodeDecisionLineage'))
	CREATE UNIQUE INDEX UX_Legal_HierNodeLineage_Target ON POLOXI.Legal_HierarchyNodeDecisionLineage (TenantId, HierarchyExecutionId, HierarchyNodeId, DecisionSessionId, DecisionBranchId, DecisionCandidateId) WHERE IsDeleted = 0;

GO

COMMIT;
