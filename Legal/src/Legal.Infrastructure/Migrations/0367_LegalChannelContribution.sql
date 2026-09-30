SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 0367: POLOXI Legal — Decision Channel Contribution (source-truth boundary).
--
-- A CHANNEL (DocumentEvidence, LegalAuthority, HumanIntelligence, Investigation, DecisionContract,
-- ExternalResearch) supplies validated information that targets a specific node of the AUTHORITATIVE
-- persisted hierarchy (migration 0366). This table is the immutable, QUALITATIVE source-truth of that
-- act: "channel X asserts relation R (with verification state V and full provenance) against node N".
--
-- STRICT INVARIANTS (the entire point of this layer):
--   * NO numeric ImpactScore / signal-delta column exists here BY DESIGN. A contribution never carries
--     a magnitude. POLOXI Wide2 remains the SOLE owner of candidate competition, uncertainty, IV,
--     convergence, and outcome. Verified contributions feed the EXISTING POLOXI signal-resolution;
--     the derived node signal/state is persisted SEPARATELY (existing DecisionSupportSignal), with its
--     own algorithm/configuration version and contributing ids for reproducibility.
--   * A contribution binds to exactly one run-scoped node: HierarchyNodeId is NEVER a cross-run
--     identity, so both HierarchyExecutionId and HierarchyNodeId are stored and FK-anchored.
--   * Each channel uses its OWN validator (DocumentEvidence → AER; others → their own verification);
--     this table stores the RESULTING qualitative relation/verification state, never re-validates.
--   * Append-only source-truth: rows are never rewritten to look like later contributions.
--
-- Standard base/audit fields on every row: TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc,
-- ModifiedByUserId, IsDeleted; RowVersion for optimistic concurrency. Additive and fail-soft: the
-- existing POLOXI Wide pipeline, CDC/CDI tables, and scoring are UNCHANGED. Idempotent via guards.
--
-- FK anchors (verified in 0366): POLOXI.Legal_HierarchyExecution(HierarchyExecutionId),
-- POLOXI.Legal_HierarchyNode(HierarchyNodeId), POLOXI.Legal_DecisionMatter(DecisionMatterId).
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── ChannelContribution: one qualitative, provenance-bearing contribution to a hierarchy node. ──────
IF OBJECT_ID(N'POLOXI.Legal_ChannelContribution',N'U') IS NULL
CREATE TABLE POLOXI.Legal_ChannelContribution
(
	ChannelContributionId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ChannelContribution PRIMARY KEY DEFAULT NEWID(),

	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL,

	-- Run-scoped authoritative binding. Both required (HierarchyNodeId is never cross-run identity).
	HierarchyExecutionId       UNIQUEIDENTIFIER NOT NULL,
	HierarchyNodeId            UNIQUEIDENTIFIER NOT NULL,

	-- Which channel produced this:
	-- DocumentEvidence | LegalAuthority | HumanIntelligence | Investigation | DecisionContract | ExternalResearch
	ChannelTypeCode            NVARCHAR(50) NOT NULL,

	-- The ONLY "strength" a contribution carries — qualitative, never numeric:
	-- SUPPORTS | CONTRADICTS | QUALIFIES | CHALLENGES | ESTABLISHES | INVALIDATES | CONTEXT_ONLY | INSUFFICIENT
	RelationCode               NVARCHAR(40) NOT NULL,

	-- Verification lifecycle as determined by the channel's OWN validator:
	-- Unverified | Verified | Refuted | Disputed. Only Verified may feed positive support to POLOXI.
	VerificationStateCode      NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_ChanContrib_Verif DEFAULT N'Unverified',

	-- Which existing POLOXI decision-support signal this contribution informs (POLOXI resolves value):
	-- EvidenceSupport | FactSupport | AuthoritySupport | LegalSupport | Uncertainty
	TargetSignalCode           NVARCHAR(60) NOT NULL,

	-- Optional qualitative qualifiers some channels record (e.g. how directly an authority governs).
	-- These are codes, NOT scores, and are null when a channel has no such notion.
	ApplicabilityCode          NVARCHAR(50) NULL,
	DirectnessCode             NVARCHAR(50) NULL,

	-- Provenance: the channel's own source object (opaque type + optional id) so the boundary stays
	-- channel-agnostic and WHY the node moved is always reconstructable.
	SourceTypeCode             NVARCHAR(80) NOT NULL,
	SourceId                   UNIQUEIDENTIFIER NULL,
	SourceLabel                NVARCHAR(400) NULL,

	-- DocumentEvidence provenance (null for non-document channels).
	LegalDocumentId            UNIQUEIDENTIFIER NULL,
	LegalDocumentVersionId     UNIQUEIDENTIFIER NULL,
	LegalDocumentPassageId     UNIQUEIDENTIFIER NULL,

	-- Model/prompt provenance when proposed by an LLM stage (advisory until verified).
	ProposedByModel            NVARCHAR(150) NULL,
	PromptRunId                NVARCHAR(100) NULL,
	VerificationReason         NVARCHAR(2000) NULL,

	-- Effectivity window for time-scoped contributions (e.g. superseded authority). Null = always.
	EffectiveFromUtc           DATETIME2 NULL,
	EffectiveToUtc             DATETIME2 NULL,

	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_ChanContrib_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_ChanContrib_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION NOT NULL,

	CONSTRAINT FK_Legal_ChanContrib_Matter    FOREIGN KEY (DecisionMatterId)     REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	CONSTRAINT FK_Legal_ChanContrib_Execution FOREIGN KEY (HierarchyExecutionId) REFERENCES POLOXI.Legal_HierarchyExecution (HierarchyExecutionId),
	CONSTRAINT FK_Legal_ChanContrib_Node      FOREIGN KEY (HierarchyNodeId)       REFERENCES POLOXI.Legal_HierarchyNode (HierarchyNodeId)
);

GO

-- Query path: "all contributions to a node" (drives node provenance) and "all contributions in a matter".
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_ChanContrib_Node' AND object_id=OBJECT_ID(N'POLOXI.Legal_ChannelContribution'))
	CREATE INDEX IX_Legal_ChanContrib_Node ON POLOXI.Legal_ChannelContribution (HierarchyNodeId, IsDeleted) INCLUDE (ChannelTypeCode, RelationCode, VerificationStateCode, TargetSignalCode);

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_ChanContrib_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_ChannelContribution'))
	CREATE INDEX IX_Legal_ChanContrib_Matter ON POLOXI.Legal_ChannelContribution (TenantId, DecisionMatterId, HierarchyExecutionId, IsDeleted);

GO

COMMIT;
