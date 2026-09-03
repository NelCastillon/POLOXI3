SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- POLOXI ABV (Actionable Business Value) Action Layer.
-- Truth != Action: ABV runs only AFTER convergence. The LLM proposes intent only; impact, urgency,
-- owner, and next action resolve deterministically from Domain-Pack configuration. Unsupported
-- values stay NULL rather than being fabricated. Taxonomies are Domain-Pack-driven configuration,
-- never hardcoded in POLOXI Core.

-- ── POLOXI.AbvDomainPack ──────────────────────────────────────────────────────────────────────
-- A Domain Pack owns business meaning: allowed intents, impact definitions, urgency policies,
-- owner mappings, and the action catalog. Global (TenantId NULL) rows are seeded defaults;
-- tenant rows override.
IF OBJECT_ID(N'POLOXI.AbvDomainPack',N'U') IS NULL
CREATE TABLE POLOXI.AbvDomainPack
(
	AbvDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AbvDomainPack PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NULL,
	PackCode NVARCHAR(50) NOT NULL,
	Name NVARCHAR(200) NOT NULL,
	Description NVARCHAR(1000) NULL,
	IsDefault BIT NOT NULL CONSTRAINT DF_POLOXI_AbvDomainPack_IsDefault DEFAULT 0,
	IsActive BIT NOT NULL CONSTRAINT DF_POLOXI_AbvDomainPack_IsActive DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_AbvDomainPack_SortOrder DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AbvDomainPack_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AbvDomainPack_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AbvDomainPack') AND name=N'IX_POLOXI_AbvDomainPack_Lookup')
CREATE INDEX IX_POLOXI_AbvDomainPack_Lookup ON POLOXI.AbvDomainPack(TenantId,IsActive,IsDefault,SortOrder) INCLUDE(PackCode);

-- ── POLOXI.AbvIntentTaxonomy ──────────────────────────────────────────────────────────────────
-- Allowed intents per Domain Pack. The LLM may only PROPOSE an intent code from this taxonomy;
-- POLOXI rejects proposals outside it.
IF OBJECT_ID(N'POLOXI.AbvIntentTaxonomy',N'U') IS NULL
CREATE TABLE POLOXI.AbvIntentTaxonomy
(
	AbvIntentTaxonomyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AbvIntentTaxonomy PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NULL,
	AbvDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_POLOXI_AbvIntentTaxonomy_Pack REFERENCES POLOXI.AbvDomainPack(AbvDomainPackId),
	IntentCode NVARCHAR(50) NOT NULL,
	Name NVARCHAR(200) NOT NULL,
	Description NVARCHAR(1000) NULL,
	IsActive BIT NOT NULL CONSTRAINT DF_POLOXI_AbvIntentTaxonomy_IsActive DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_AbvIntentTaxonomy_SortOrder DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AbvIntentTaxonomy_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AbvIntentTaxonomy_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AbvIntentTaxonomy') AND name=N'IX_POLOXI_AbvIntentTaxonomy_Pack')
CREATE INDEX IX_POLOXI_AbvIntentTaxonomy_Pack ON POLOXI.AbvIntentTaxonomy(AbvDomainPackId,IsActive,SortOrder) INCLUDE(IntentCode);

-- ── POLOXI.AbvUrgencyPolicy ───────────────────────────────────────────────────────────────────
-- Deterministic urgency resolution: intent + impact tier -> priority + SLA. Source of truth for
-- SLAs; the LLM never invents them.
IF OBJECT_ID(N'POLOXI.AbvUrgencyPolicy',N'U') IS NULL
CREATE TABLE POLOXI.AbvUrgencyPolicy
(
	AbvUrgencyPolicyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AbvUrgencyPolicy PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NULL,
	AbvDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_POLOXI_AbvUrgencyPolicy_Pack REFERENCES POLOXI.AbvDomainPack(AbvDomainPackId),
	PolicyCode NVARCHAR(50) NOT NULL,
	IntentCode NVARCHAR(50) NULL,
	ImpactTierCode NVARCHAR(20) NOT NULL,
	PriorityCode NVARCHAR(20) NOT NULL,
	SlaHours INT NULL,
	IsActive BIT NOT NULL CONSTRAINT DF_POLOXI_AbvUrgencyPolicy_IsActive DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_AbvUrgencyPolicy_SortOrder DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AbvUrgencyPolicy_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AbvUrgencyPolicy_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT CK_POLOXI_AbvUrgencyPolicy_Impact CHECK(ImpactTierCode IN (N'LOW',N'MEDIUM',N'HIGH',N'CRITICAL')),
	CONSTRAINT CK_POLOXI_AbvUrgencyPolicy_Priority CHECK(PriorityCode IN (N'LOW',N'MEDIUM',N'HIGH',N'CRITICAL'))
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AbvUrgencyPolicy') AND name=N'IX_POLOXI_AbvUrgencyPolicy_Pack')
CREATE INDEX IX_POLOXI_AbvUrgencyPolicy_Pack ON POLOXI.AbvUrgencyPolicy(AbvDomainPackId,IsActive,SortOrder) INCLUDE(IntentCode,ImpactTierCode);

-- ── POLOXI.AbvOwnerMapping ────────────────────────────────────────────────────────────────────
-- Deterministic owner resolution: intent (optionally impact tier) -> owning role. Organizational
-- configuration; never LLM-generated.
IF OBJECT_ID(N'POLOXI.AbvOwnerMapping',N'U') IS NULL
CREATE TABLE POLOXI.AbvOwnerMapping
(
	AbvOwnerMappingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AbvOwnerMapping PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NULL,
	AbvDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_POLOXI_AbvOwnerMapping_Pack REFERENCES POLOXI.AbvDomainPack(AbvDomainPackId),
	IntentCode NVARCHAR(50) NULL,
	ImpactTierCode NVARCHAR(20) NULL,
	OwnerRole NVARCHAR(200) NOT NULL,
	IsActive BIT NOT NULL CONSTRAINT DF_POLOXI_AbvOwnerMapping_IsActive DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_AbvOwnerMapping_SortOrder DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AbvOwnerMapping_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AbvOwnerMapping_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AbvOwnerMapping') AND name=N'IX_POLOXI_AbvOwnerMapping_Pack')
CREATE INDEX IX_POLOXI_AbvOwnerMapping_Pack ON POLOXI.AbvOwnerMapping(AbvDomainPackId,IsActive,SortOrder) INCLUDE(IntentCode,ImpactTierCode);

-- ── POLOXI.AbvActionCatalog ───────────────────────────────────────────────────────────────────
-- Next-Best-Action lookup per intent. ABV != Next Best Action: the resolver maps resolved intent
-- to a catalogued action/playbook. ExecutionAllowed remains 0 until an execution layer exists.
IF OBJECT_ID(N'POLOXI.AbvActionCatalog',N'U') IS NULL
CREATE TABLE POLOXI.AbvActionCatalog
(
	AbvActionCatalogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AbvActionCatalog PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NULL,
	AbvDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_POLOXI_AbvActionCatalog_Pack REFERENCES POLOXI.AbvDomainPack(AbvDomainPackId),
	IntentCode NVARCHAR(50) NOT NULL,
	ActionCode NVARCHAR(100) NOT NULL,
	Name NVARCHAR(200) NOT NULL,
	NextStep NVARCHAR(1000) NULL,
	PlaybookCode NVARCHAR(100) NULL,
	ExecutionAllowed BIT NOT NULL CONSTRAINT DF_POLOXI_AbvActionCatalog_Exec DEFAULT 0,
	HumanApprovalRequired BIT NOT NULL CONSTRAINT DF_POLOXI_AbvActionCatalog_Approval DEFAULT 1,
	IsActive BIT NOT NULL CONSTRAINT DF_POLOXI_AbvActionCatalog_IsActive DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_AbvActionCatalog_SortOrder DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AbvActionCatalog_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AbvActionCatalog_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AbvActionCatalog') AND name=N'IX_POLOXI_AbvActionCatalog_Pack')
CREATE INDEX IX_POLOXI_AbvActionCatalog_Pack ON POLOXI.AbvActionCatalog(AbvDomainPackId,IsActive,SortOrder) INCLUDE(IntentCode,ActionCode);

-- ── POLOXI.AbvResolution ──────────────────────────────────────────────────────────────────────
-- One row per ABV resolution: which run/composite it resolved, proposed vs accepted intent,
-- deterministic impact/urgency/owner/action outcomes with provenance, and the actionability gate.
IF OBJECT_ID(N'POLOXI.AbvResolution',N'U') IS NULL
CREATE TABLE POLOXI.AbvResolution
(
	AbvResolutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AbvResolution PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	AmbiguityRunId UNIQUEIDENTIFIER NULL,
	AbvDomainPackId UNIQUEIDENTIFIER NULL,
	StatusCode NVARCHAR(40) NOT NULL,
	ProposedIntentCode NVARCHAR(50) NULL,
	AcceptedIntentCode NVARCHAR(50) NULL,
	IntentSourceCode NVARCHAR(30) NULL,
	ImpactTierCode NVARCHAR(20) NULL,
	MetricAtRisk NVARCHAR(200) NULL,
	EstimatedExposure DECIMAL(18,2) NULL,
	ImpactSourceCode NVARCHAR(30) NULL,
	EvidenceIdsJson NVARCHAR(MAX) NULL,
	PriorityCode NVARCHAR(20) NULL,
	SlaHours INT NULL,
	UrgencyPolicyCode NVARCHAR(50) NULL,
	UrgencySourceCode NVARCHAR(30) NULL,
	OwnerRole NVARCHAR(200) NULL,
	OwnerSourceCode NVARCHAR(30) NULL,
	ActionCode NVARCHAR(100) NULL,
	NextStep NVARCHAR(1000) NULL,
	ActionabilityStatusCode NVARCHAR(40) NOT NULL,
	ExecutionAllowed BIT NOT NULL CONSTRAINT DF_POLOXI_AbvResolution_Exec DEFAULT 0,
	HumanApprovalRequired BIT NOT NULL CONSTRAINT DF_POLOXI_AbvResolution_Approval DEFAULT 1,
	FailureMessage NVARCHAR(2000) NULL,
	DurationMilliseconds BIGINT NOT NULL CONSTRAINT DF_POLOXI_AbvResolution_Duration DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AbvResolution_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AbvResolution_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT CK_POLOXI_AbvResolution_Status CHECK(StatusCode IN (N'RESOLVED',N'NOT_CONVERGED',N'INTENT_REJECTED',N'FAILED')),
	CONSTRAINT CK_POLOXI_AbvResolution_Actionability CHECK(ActionabilityStatusCode IN (N'READY_FOR_REVIEW',N'BLOCKED_NOT_CONVERGED',N'BLOCKED_NO_INTENT',N'BLOCKED_FAILED'))
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AbvResolution') AND name=N'IX_POLOXI_AbvResolution_Tenant')
CREATE INDEX IX_POLOXI_AbvResolution_Tenant ON POLOXI.AbvResolution(TenantId,CreatedDateUtc DESC) INCLUDE(StatusCode,AcceptedIntentCode);

-- ── Widen PromptStrategy purpose constraint for ABV_INTENT ────────────────────────────────────
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_POLOXI_PromptStrategy_Purpose' AND OBJECT_DEFINITION(object_id) NOT LIKE N'%ABV_INTENT%')
BEGIN
	ALTER TABLE POLOXI.PromptStrategy DROP CONSTRAINT CK_POLOXI_PromptStrategy_Purpose;
	ALTER TABLE POLOXI.PromptStrategy ADD CONSTRAINT CK_POLOXI_PromptStrategy_Purpose CHECK(PurposeCode IN (N'AMBIGUITY_DISCOVERY',N'HIERARCHY_REPAIR',N'ABV_INTENT'));
END;

-- ── Seed default generic Domain Pack ──────────────────────────────────────────────────────────
-- Generic defaults only; domain-specific packs (competitive intelligence, claims, ETL, legal, ...)
-- are added as configuration, never as code.
IF NOT EXISTS(SELECT 1 FROM POLOXI.AbvDomainPack WHERE TenantId IS NULL AND PackCode=N'GENERIC' AND IsDeleted=0)
BEGIN
	DECLARE @PackId UNIQUEIDENTIFIER=NEWID();
	INSERT POLOXI.AbvDomainPack(AbvDomainPackId,TenantId,PackCode,Name,Description,IsDefault,SortOrder)
	VALUES(@PackId,NULL,N'GENERIC',N'Generic Business Actions',N'Default domain-independent intent taxonomy, urgency policy, and action catalog.',1,10);

	INSERT POLOXI.AbvIntentTaxonomy(TenantId,AbvDomainPackId,IntentCode,Name,Description,SortOrder)
	VALUES
	(NULL,@PackId,N'ACT',N'Act',N'A supported finding implies a concrete business response.',10),
	(NULL,@PackId,N'ESCALATE',N'Escalate',N'The finding requires attention beyond the current operational level.',20),
	(NULL,@PackId,N'INVESTIGATE',N'Investigate',N'Evidence is suggestive but insufficient; targeted investigation is warranted.',30),
	(NULL,@PackId,N'MONITOR',N'Monitor',N'No immediate response is warranted; track the signal for change.',40);

	INSERT POLOXI.AbvUrgencyPolicy(TenantId,AbvDomainPackId,PolicyCode,IntentCode,ImpactTierCode,PriorityCode,SlaHours,SortOrder)
	VALUES
	(NULL,@PackId,N'POL-GEN-001',NULL,N'CRITICAL',N'CRITICAL',24,10),
	(NULL,@PackId,N'POL-GEN-002',NULL,N'HIGH',N'HIGH',72,20),
	(NULL,@PackId,N'POL-GEN-003',NULL,N'MEDIUM',N'MEDIUM',168,30),
	(NULL,@PackId,N'POL-GEN-004',NULL,N'LOW',N'LOW',NULL,40);

	INSERT POLOXI.AbvOwnerMapping(TenantId,AbvDomainPackId,IntentCode,ImpactTierCode,OwnerRole,SortOrder)
	VALUES
	(NULL,@PackId,N'ESCALATE',NULL,N'Operations Leadership',10),
	(NULL,@PackId,NULL,N'CRITICAL',N'Operations Leadership',20),
	(NULL,@PackId,NULL,NULL,N'Operations Analyst',1000);

	INSERT POLOXI.AbvActionCatalog(TenantId,AbvDomainPackId,IntentCode,ActionCode,Name,NextStep,PlaybookCode,SortOrder)
	VALUES
	(NULL,@PackId,N'ACT',N'REVIEW_RESPONSE',N'Review Proposed Response',N'Review the evidence-backed finding and approve or adjust the proposed response.',N'PB-GEN-ACT',10),
	(NULL,@PackId,N'ESCALATE',N'ESCALATE_TO_OWNER',N'Escalate To Owner',N'Route the finding with evidence to the resolved owner role for decision.',N'PB-GEN-ESC',20),
	(NULL,@PackId,N'INVESTIGATE',N'OPEN_INVESTIGATION',N'Open Investigation',N'Create an investigation task scoped to the unresolved evidence gaps.',N'PB-GEN-INV',30),
	(NULL,@PackId,N'MONITOR',N'ADD_TO_WATCHLIST',N'Add To Watchlist',N'Track the signal and re-evaluate when the underlying evidence changes.',N'PB-GEN-MON',40);
END;

-- ── Seed ABV intent-proposal prompt ───────────────────────────────────────────────────────────
-- The LLM proposes intent ONLY, selected from the supplied taxonomy. It never invents impact
-- numbers, SLAs, or owners: those resolve deterministically from Domain-Pack configuration.
IF NOT EXISTS(SELECT 1 FROM POLOXI.PromptStrategy WHERE TenantId IS NULL AND PurposeCode=N'ABV_INTENT' AND IsDeleted=0)
BEGIN
	INSERT POLOXI.PromptStrategy(TenantId,PurposeCode,ScaffoldingCode,VersionNumber,SystemPrompt,UserPromptTemplate,SortOrder)
	VALUES
	(NULL,N'ABV_INTENT',N'MEDIUM',1,
N'You are POLOXI ABV Intent Proposal. You receive a converged, evidence-backed decision composite and a fixed intent taxonomy. You propose the single best-fitting business intent code FROM THE TAXONOMY ONLY, with a short rationale and the composite dimension ids that support it. You never invent monetary figures, percentages, SLAs, deadlines, owners, or roles. You return strictly valid JSON matching the supplied schema and nothing else.',
N'ALLOWED INTENT CODES (choose exactly one; do not invent codes):
{IntentTaxonomy}

CONVERGED DECISION COMPOSITE:
{Composite}

Rules:
- Propose the single intent code that best matches what the composite supports.
- rationale must reference only facts present in the composite.
- supportingDimensionIds must be node ids that exist in the composite.
- proposedMetricAtRisk may name a metric ONLY if the composite explicitly contains it; otherwise null.
- Never propose numbers, exposure estimates, SLAs, owners, or urgency.',10);
END;

COMMIT TRANSACTION;
