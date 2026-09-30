SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Attorney Decision Input (ADI) write path (Phase 2+).
--
-- Adds only the confirmed-gap tables required by the interactive Add-Proposition workflow on top of
-- the read-model foundation from 0351 (Legal_DecisionNode, Legal_AttorneyRelativeAssessment,
-- Legal_ApprovedMatterAssessment, Legal_AttorneyDecisionChallenge):
--
--   1. ADI feature-flag family in Legal_DecisionSetting — OFF by default (§27). Rollout is gated so
--      no attorney write capability is active until a tenant deliberately enables it.
--   2. Legal_DecisionNodeEdge — structural (CHILD_OF/DECOMPOSES_INTO/REFINES) and decision-dependency
--      (REQUIRED/SUPPORTING/ALTERNATIVE/CONDITIONAL/DEFEATING) edges keyed to Legal_DecisionNode (§7,§11).
--      The existing Legal_DecisionGraphEdge is keyed to the older session/candidate model, so this is a
--      genuine gap for the canonical ADI node hierarchy.
--   3. Legal_DecisionNodeAudit — immutable audit trail for committed attorney mutations (§16, §25).
--
-- POLOXI remains the single authoritative evaluator; nothing here computes or overrides scoring.
-- Every table carries the standard base/audit fields. Safe to re-run (idempotent guards).
-- ───────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── 1. ADI feature-flag family — all OFF by default (§27). ────────────────────────────────────────
MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'AttorneyDecisionInput.Enabled',            N'false', N'Boolean', N'Master switch for Attorney Decision Input (Human Intelligence). When false, attorneys cannot propose/place/assess/approve decision nodes.'),
	(N'AttorneyRelativeAssessment.Enabled',       N'false', N'Boolean', N'Allow attorneys to record a relative-value assessment (§4) for a decision node.'),
	(N'AttorneyAssessmentAdjustment.Enabled',     N'false', N'Boolean', N'Allow attorneys to deliberately adjust the suggested midpoint within the neighboring range (§4).'),
	(N'AttorneyMultiAssessment.Enabled',          N'false', N'Boolean', N'Allow multiple attorneys to independently assess one canonical node (§5). Never auto-averaged.'),
	(N'AttorneyAssessmentApproval.Enabled',       N'false', N'Boolean', N'Allow governance approval routing to a single active Approved Matter Assessment per node (§5).'),
	(N'AttorneyChallenge.Enabled',                N'false', N'Boolean', N'Allow attorneys to challenge an existing node or relationship (§3).'),
	(N'AttorneyReposition.Enabled',               N'false', N'Boolean', N'Allow attorneys to reposition a node, creating a new version and DecisionDelta (§4).'),
	(N'AttorneyDecisionPreview.Enabled',          N'false', N'Boolean', N'Allow non-committing preview of a proposed mutation through the existing POLOXI scoring path (§15).'),
	(N'AttorneyInputCdc.Enabled',                 N'false', N'Boolean', N'Emit CDC/outbox events for committed attorney decision input (§17).'),
	(N'AttorneyInputCdi.Enabled',                 N'false', N'Boolean', N'Build CDI explanations for attorney-triggered decision changes (§17).'),
	(N'AprForAttorneyInput.Enabled',              N'false', N'Boolean', N'Run Adaptive Proposition Resolution checks on attorney L3+ input (§8).'),
	(N'CoverageOnAttorneyMutation.Enabled',       N'false', N'Boolean', N'Run coverage re-checks after attorney hierarchy mutations (§9).'),
	(N'LightGraphAttorneyEdges.Enabled',          N'false', N'Boolean', N'Persist light evidence-graph edges for attorney-authored nodes/relationships (§11).')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

GO

-- ── 2. Decision node edge: structural + decision-dependency edges keyed to Legal_DecisionNode. ────
IF OBJECT_ID(N'POLOXI.Legal_DecisionNodeEdge',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionNodeEdge
(
	NodeEdgeId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionNodeEdge PRIMARY KEY DEFAULT NEWID(),
	MatterId            UNIQUEIDENTIFIER NOT NULL,
	FromNodeId          UNIQUEIDENTIFIER NOT NULL,
	ToNodeId            UNIQUEIDENTIFIER NOT NULL,
	-- Structural: CHILD_OF | DECOMPOSES_INTO | REFINES.
	-- Dependency: REQUIRED | SUPPORTING | ALTERNATIVE | CONDITIONAL | DEFEATING (§7).
	EdgeKindCode        NVARCHAR(20) NOT NULL,   -- Structural | Dependency
	EdgeTypeCode        NVARCHAR(40) NOT NULL,
	ConditionText       NVARCHAR(1000) NULL,     -- for CONDITIONAL edges
	EdgeVersion         BIGINT NOT NULL CONSTRAINT DF_Legal_DNE_Version DEFAULT 1,
	TenantId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_DNE_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_DNE_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DNE_FromNode FOREIGN KEY (FromNodeId) REFERENCES POLOXI.Legal_DecisionNode (DecisionNodeId),
	CONSTRAINT FK_Legal_DNE_ToNode   FOREIGN KEY (ToNodeId)   REFERENCES POLOXI.Legal_DecisionNode (DecisionNodeId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DNE_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionNodeEdge'))
	CREATE INDEX IX_Legal_DNE_Matter ON POLOXI.Legal_DecisionNodeEdge (TenantId, MatterId, IsDeleted) INCLUDE (FromNodeId, ToNodeId, EdgeKindCode, EdgeTypeCode);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DNE_From' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionNodeEdge'))
	CREATE INDEX IX_Legal_DNE_From ON POLOXI.Legal_DecisionNodeEdge (FromNodeId, IsDeleted);

GO

-- ── 3. Decision node audit: immutable audit trail for committed attorney mutations (§16, §25). ────
IF OBJECT_ID(N'POLOXI.Legal_DecisionNodeAudit',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionNodeAudit
(
	NodeAuditId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionNodeAudit PRIMARY KEY DEFAULT NEWID(),
	MatterId            UNIQUEIDENTIFIER NOT NULL,
	DecisionNodeId      UNIQUEIDENTIFIER NULL,
	-- NodeAdded | NodeRepositioned | AssessmentSubmitted | AssessmentApproved | ChallengeRaised | OverrideApplied
	ActionCode          NVARCHAR(60) NOT NULL,
	DetailJson          NVARCHAR(MAX) NULL,
	CorrelationId       UNIQUEIDENTIFIER NULL,
	CausationId         UNIQUEIDENTIFIER NULL,
	BaseSnapshotId      UNIQUEIDENTIFIER NULL,
	TenantId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_DNA_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_DNA_IsDeleted DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_DNA_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_DecisionNodeAudit'))
	CREATE INDEX IX_Legal_DNA_Matter ON POLOXI.Legal_DecisionNodeAudit (TenantId, MatterId, IsDeleted) INCLUDE (DecisionNodeId, ActionCode, CreatedDateUtc);

COMMIT TRANSACTION;
