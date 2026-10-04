-- ============================================================================
-- 0385: Judz Matter Lifecycle — first-class, domain-configurable lifecycle engine.
--
-- Judz Matter Lifecycle is a PLATFORM subsystem (not Clio-specific, not part of
-- POLOXI Core). It supplies OPERATIONAL STAGE CONTEXT to Judz Decision
-- Intelligence. The architectural invariant is:
--
--     Matter Stage  ≠  Decision State  ≠  POLOXI Branch State
--
-- Matter Stage answers "where is the matter in its operational journey"; it does
-- NOT own candidate competition, ambiguity, frontier, or readiness (POLOXI Core),
-- and it does NOT copy POLOXI state. Lifecycle may *reference* a DecisionId's
-- readiness (future group) but never decides a winner.
--
-- This migration establishes the reusable ENGINE CORE as a directed lifecycle
-- GRAPH (not StageOrder=1,2,3). The 8 Personal Injury stages become the first
-- versioned Domain Lifecycle Definition (PI_STANDARD v1).
--
-- GROUPS IN THIS SLICE:
--   Definition : Legal_MatterLifecycleDefinition / *Version / *StageDefinition
--                / *StageTransitionDefinition / *StageRequirementDefinition
--   Runtime    : Legal_MatterLifecycle / *StageHistory / *StageRequirementInstance
--                / *StageEvent
--   Interop    : Legal_ExternalSystem / Legal_ExternalStageMapping
--
-- DEFERRED (follow-on versioned additions on this backbone): Matter Resource
-- abstraction, Treatment/Financial entities, Intake Decision persistence, Stage
-- Decision links, Sharing projections.
--
-- Conventions: POLOXI schema, Legal_* prefix, base/audit fields, filtered indexes
-- WHERE IsDeleted = 0, global (TenantId NULL) seed defaults overridable per tenant.
-- Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');
GO

-- ════════════════════════════════════════════════════════════════════════════
-- GROUP 1 — DEFINITION (configuration, not case data)
-- ════════════════════════════════════════════════════════════════════════════

-- ── Legal_MatterLifecycleDefinition: what a lifecycle MEANS for a domain. ──
-- Examples: PI_STANDARD, PI_NEW_YORK, EMPLOYMENT_LITIGATION, COMMERCIAL_LITIGATION.
IF OBJECT_ID(N'POLOXI.Legal_MatterLifecycleDefinition', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterLifecycleDefinition
(
	MatterLifecycleDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterLifecycleDefinition PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId        UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterLifecycleDefinition_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	Code                        NVARCHAR(100) NOT NULL,
	Name                        NVARCHAR(200) NOT NULL,
	Description                 NVARCHAR(MAX) NULL,
	MatterTypeCode              NVARCHAR(100) NULL,
	JurisdictionCode            NVARCHAR(100) NULL,
	IsDefault                   BIT NOT NULL CONSTRAINT DF_Legal_MatterLifecycleDefinition_IsDefault DEFAULT 0,
	IsActive                    BIT NOT NULL CONSTRAINT DF_Legal_MatterLifecycleDefinition_IsActive DEFAULT 1,
	SortOrder                   INT NOT NULL CONSTRAINT DF_Legal_MatterLifecycleDefinition_SortOrder DEFAULT 0,
	TenantId                    UNIQUEIDENTIFIER NULL,
	CreatedDateUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterLifecycleDefinition_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId             UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc             DATETIME2 NULL,
	ModifiedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                   BIT NOT NULL CONSTRAINT DF_Legal_MatterLifecycleDefinition_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterLifecycleDefinition') AND name = N'IX_Legal_MatterLifecycleDefinition_Lookup')
	CREATE INDEX IX_Legal_MatterLifecycleDefinition_Lookup
		ON POLOXI.Legal_MatterLifecycleDefinition (MatterTypeCode, IsActive, IsDefault, SortOrder) INCLUDE (Code) WHERE IsDeleted = 0;
GO

-- ── Legal_MatterLifecycleVersion: an immutable, publishable version of a lifecycle. ──
-- Publishing v2 must NEVER silently rewrite active matters still on v1 (auditability).
IF OBJECT_ID(N'POLOXI.Legal_MatterLifecycleVersion', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterLifecycleVersion
(
	MatterLifecycleVersionId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterLifecycleVersion PRIMARY KEY DEFAULT NEWID(),
	MatterLifecycleDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterLifecycleVersion_Definition REFERENCES POLOXI.Legal_MatterLifecycleDefinition (MatterLifecycleDefinitionId),
	VersionNumber               INT NOT NULL,
	VersionLabel                NVARCHAR(50) NULL,
	StatusCode                  NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_MatterLifecycleVersion_Status DEFAULT N'DRAFT', -- DRAFT | PUBLISHED | RETIRED
	EffectiveFromUtc            DATETIME2 NULL,
	EffectiveToUtc              DATETIME2 NULL,
	ConfigurationJson           NVARCHAR(MAX) NULL,
	PublishedUtc                DATETIME2 NULL,
	PublishedByUserId           UNIQUEIDENTIFIER NULL,
	TenantId                    UNIQUEIDENTIFIER NULL,
	CreatedDateUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterLifecycleVersion_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId             UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc             DATETIME2 NULL,
	ModifiedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                   BIT NOT NULL CONSTRAINT DF_Legal_MatterLifecycleVersion_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterLifecycleVersion') AND name = N'IX_Legal_MatterLifecycleVersion_Definition')
	CREATE INDEX IX_Legal_MatterLifecycleVersion_Definition
		ON POLOXI.Legal_MatterLifecycleVersion (MatterLifecycleDefinitionId, StatusCode, VersionNumber) WHERE IsDeleted = 0;
GO

-- ── Legal_MatterStageDefinition: a stage within a lifecycle version. ──
-- DisplayOrder is for presentation ONLY; transitions are the authority (group below).
IF OBJECT_ID(N'POLOXI.Legal_MatterStageDefinition', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterStageDefinition
(
	MatterStageDefinitionId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterStageDefinition PRIMARY KEY DEFAULT NEWID(),
	MatterLifecycleVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageDefinition_Version REFERENCES POLOXI.Legal_MatterLifecycleVersion (MatterLifecycleVersionId),
	Code                     NVARCHAR(100) NOT NULL,
	Name                     NVARCHAR(200) NOT NULL,
	Description              NVARCHAR(MAX) NULL,
	StageCategory            NVARCHAR(100) NULL,
	DisplayOrder             INT NOT NULL CONSTRAINT DF_Legal_MatterStageDefinition_DisplayOrder DEFAULT 0,
	IsInitial                BIT NOT NULL CONSTRAINT DF_Legal_MatterStageDefinition_IsInitial DEFAULT 0,
	IsTerminal               BIT NOT NULL CONSTRAINT DF_Legal_MatterStageDefinition_IsTerminal DEFAULT 0,
	IsOptional               BIT NOT NULL CONSTRAINT DF_Legal_MatterStageDefinition_IsOptional DEFAULT 0,
	AllowReentry             BIT NOT NULL CONSTRAINT DF_Legal_MatterStageDefinition_AllowReentry DEFAULT 0,
	DefaultSlaDays           INT NULL,
	ConfigurationJson        NVARCHAR(MAX) NULL,
	IsActive                 BIT NOT NULL CONSTRAINT DF_Legal_MatterStageDefinition_IsActive DEFAULT 1,
	TenantId                 UNIQUEIDENTIFIER NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterStageDefinition_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_MatterStageDefinition_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterStageDefinition') AND name = N'IX_Legal_MatterStageDefinition_Version')
	CREATE INDEX IX_Legal_MatterStageDefinition_Version
		ON POLOXI.Legal_MatterStageDefinition (MatterLifecycleVersionId, IsActive, DisplayOrder) INCLUDE (Code) WHERE IsDeleted = 0;
GO

-- ── Legal_MatterStageTransitionDefinition: the directed lifecycle GRAPH edges. ──
-- Transitions are explicit; never inferred from DisplayOrder. Supports branching
-- and legitimate re-entry (e.g. Litigation → Treatment) without destroying history.
IF OBJECT_ID(N'POLOXI.Legal_MatterStageTransitionDefinition', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterStageTransitionDefinition
(
	MatterStageTransitionDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterStageTransitionDefinition PRIMARY KEY DEFAULT NEWID(),
	MatterLifecycleVersionId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageTransitionDefinition_Version REFERENCES POLOXI.Legal_MatterLifecycleVersion (MatterLifecycleVersionId),
	FromStageDefinitionId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageTransitionDefinition_From REFERENCES POLOXI.Legal_MatterStageDefinition (MatterStageDefinitionId),
	ToStageDefinitionId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageTransitionDefinition_To REFERENCES POLOXI.Legal_MatterStageDefinition (MatterStageDefinitionId),
	Code                              NVARCHAR(100) NOT NULL,
	Name                              NVARCHAR(200) NOT NULL,
	TransitionType                    NVARCHAR(50) NULL, -- ADVANCE | BRANCH | REENTRY | CLOSE
	RequiresApproval                  BIT NOT NULL CONSTRAINT DF_Legal_MatterStageTransitionDefinition_RequiresApproval DEFAULT 0,
	IsAutomaticAllowed                BIT NOT NULL CONSTRAINT DF_Legal_MatterStageTransitionDefinition_AutoAllowed DEFAULT 0,
	Priority                          INT NOT NULL CONSTRAINT DF_Legal_MatterStageTransitionDefinition_Priority DEFAULT 0,
	GuardExpressionJson               NVARCHAR(MAX) NULL,
	IsActive                          BIT NOT NULL CONSTRAINT DF_Legal_MatterStageTransitionDefinition_IsActive DEFAULT 1,
	TenantId                          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                    DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterStageTransitionDefinition_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                   DATETIME2 NULL,
	ModifiedByUserId                  UNIQUEIDENTIFIER NULL,
	IsDeleted                         BIT NOT NULL CONSTRAINT DF_Legal_MatterStageTransitionDefinition_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterStageTransitionDefinition') AND name = N'IX_Legal_MatterStageTransitionDefinition_From')
	CREATE INDEX IX_Legal_MatterStageTransitionDefinition_From
		ON POLOXI.Legal_MatterStageTransitionDefinition (MatterLifecycleVersionId, FromStageDefinitionId, IsActive, Priority) INCLUDE (ToStageDefinitionId) WHERE IsDeleted = 0;
GO

-- ── Legal_MatterStageRequirementDefinition: what SHOULD be true during a stage. ──
-- Makes the lifecycle intelligent (not a colored progress bar). Domain-configurable;
-- no PI logic hardcoded in the application.
IF OBJECT_ID(N'POLOXI.Legal_MatterStageRequirementDefinition', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterStageRequirementDefinition
(
	MatterStageRequirementDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterStageRequirementDefinition PRIMARY KEY DEFAULT NEWID(),
	MatterStageDefinitionId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageRequirementDefinition_Stage REFERENCES POLOXI.Legal_MatterStageDefinition (MatterStageDefinitionId),
	Code                               NVARCHAR(100) NOT NULL,
	Name                               NVARCHAR(250) NOT NULL,
	Description                        NVARCHAR(MAX) NULL,
	RequirementType                    NVARCHAR(50) NOT NULL,  -- FIELD|DOCUMENT|EVENT|TASK|DECISION|EVIDENCE|MILESTONE|PARTICIPANT|APPROVAL|FINANCIAL|TREATMENT
	RequirementLevel                   NVARCHAR(30) NOT NULL,  -- REQUIRED|EXPECTED|OPTIONAL|ADVISORY
	EvaluationMode                     NVARCHAR(30) NULL,      -- MANUAL|AUTOMATIC|HYBRID
	RuleJson                           NVARCHAR(MAX) NULL,
	DisplayOrder                       INT NOT NULL CONSTRAINT DF_Legal_MatterStageRequirementDefinition_DisplayOrder DEFAULT 0,
	IsBlocking                         BIT NOT NULL CONSTRAINT DF_Legal_MatterStageRequirementDefinition_IsBlocking DEFAULT 0,
	IsActive                           BIT NOT NULL CONSTRAINT DF_Legal_MatterStageRequirementDefinition_IsActive DEFAULT 1,
	TenantId                           UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                     DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterStageRequirementDefinition_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                    UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                    DATETIME2 NULL,
	ModifiedByUserId                   UNIQUEIDENTIFIER NULL,
	IsDeleted                          BIT NOT NULL CONSTRAINT DF_Legal_MatterStageRequirementDefinition_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterStageRequirementDefinition') AND name = N'IX_Legal_MatterStageRequirementDefinition_Stage')
	CREATE INDEX IX_Legal_MatterStageRequirementDefinition_Stage
		ON POLOXI.Legal_MatterStageRequirementDefinition (MatterStageDefinitionId, IsActive, DisplayOrder) INCLUDE (Code, RequirementLevel, IsBlocking) WHERE IsDeleted = 0;
GO

-- ════════════════════════════════════════════════════════════════════════════
-- GROUP 2 — INTEROP (CMS-independence: Judz never becomes dependent on Clio)
-- ════════════════════════════════════════════════════════════════════════════

-- ── Legal_ExternalSystem: source systems of operational truth (CLIO, FILEVINE, …, JUDZ). ──
IF OBJECT_ID(N'POLOXI.Legal_ExternalSystem', N'U') IS NULL
CREATE TABLE POLOXI.Legal_ExternalSystem
(
	ExternalSystemId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ExternalSystem PRIMARY KEY DEFAULT NEWID(),
	Code              NVARCHAR(60) NOT NULL,  -- CLIO | FILEVINE | LITIFY | MYCASE | JUDZ | OTHER
	Name              NVARCHAR(200) NOT NULL,
	IsActive          BIT NOT NULL CONSTRAINT DF_Legal_ExternalSystem_IsActive DEFAULT 1,
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Legal_ExternalSystem_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Legal_ExternalSystem_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_ExternalSystem') AND name = N'UX_Legal_ExternalSystem_Code')
	CREATE UNIQUE INDEX UX_Legal_ExternalSystem_Code
		ON POLOXI.Legal_ExternalSystem (Code) WHERE IsDeleted = 0 AND TenantId IS NULL;
GO

-- ── Legal_ExternalStageMapping: map an external CMS stage code → a Judz StageDefinition. ──
-- e.g. Clio "Litigation" → PI_STANDARD/LITIGATION. Judz stays CMS-independent.
IF OBJECT_ID(N'POLOXI.Legal_ExternalStageMapping', N'U') IS NULL
CREATE TABLE POLOXI.Legal_ExternalStageMapping
(
	ExternalStageMappingId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ExternalStageMapping PRIMARY KEY DEFAULT NEWID(),
	ExternalSystemId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_ExternalStageMapping_System REFERENCES POLOXI.Legal_ExternalSystem (ExternalSystemId),
	MatterLifecycleVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_ExternalStageMapping_Version REFERENCES POLOXI.Legal_MatterLifecycleVersion (MatterLifecycleVersionId),
	ExternalStageCode        NVARCHAR(100) NOT NULL,
	ExternalStageName        NVARCHAR(200) NULL,
	MatterStageDefinitionId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_ExternalStageMapping_Stage REFERENCES POLOXI.Legal_MatterStageDefinition (MatterStageDefinitionId),
	MappingPriority          INT NOT NULL CONSTRAINT DF_Legal_ExternalStageMapping_Priority DEFAULT 0,
	IsAuthoritative          BIT NOT NULL CONSTRAINT DF_Legal_ExternalStageMapping_IsAuthoritative DEFAULT 1,
	IsActive                 BIT NOT NULL CONSTRAINT DF_Legal_ExternalStageMapping_IsActive DEFAULT 1,
	TenantId                 UNIQUEIDENTIFIER NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_ExternalStageMapping_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_ExternalStageMapping_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_ExternalStageMapping') AND name = N'IX_Legal_ExternalStageMapping_Lookup')
	CREATE INDEX IX_Legal_ExternalStageMapping_Lookup
		ON POLOXI.Legal_ExternalStageMapping (ExternalSystemId, MatterLifecycleVersionId, ExternalStageCode, IsActive, MappingPriority) INCLUDE (MatterStageDefinitionId) WHERE IsDeleted = 0;
GO

-- ════════════════════════════════════════════════════════════════════════════
-- GROUP 3 — RUNTIME (where a specific matter actually is)
-- ════════════════════════════════════════════════════════════════════════════

-- ── Legal_MatterLifecycle: the active lifecycle instance for one matter. ──
-- Pinned to a specific LifecycleVersion (publishing v2 does not move live matters).
-- AuthorityMode prevents sync from fighting the attorney.
IF OBJECT_ID(N'POLOXI.Legal_MatterLifecycle', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterLifecycle
(
	MatterLifecycleId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterLifecycle PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterLifecycle_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	MatterLifecycleVersionId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterLifecycle_Version REFERENCES POLOXI.Legal_MatterLifecycleVersion (MatterLifecycleVersionId),
	CurrentStageDefinitionId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterLifecycle_CurrentStage REFERENCES POLOXI.Legal_MatterStageDefinition (MatterStageDefinitionId),
	StatusCode                 NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_MatterLifecycle_Status DEFAULT N'ACTIVE', -- ACTIVE | COMPLETED | SUSPENDED
	AuthorityMode              NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterLifecycle_Authority DEFAULT N'JUDZ_AUTHORITATIVE', -- EXTERNAL_AUTHORITATIVE|JUDZ_AUTHORITATIVE|MANUAL_ATTORNEY|DERIVED
	ExternalSystemId           UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterLifecycle_ExternalSystem REFERENCES POLOXI.Legal_ExternalSystem (ExternalSystemId),
	ExternalStageCode          NVARCHAR(100) NULL,
	StartedUtc                 DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterLifecycle_Started DEFAULT SYSUTCDATETIME(),
	CurrentStageEnteredUtc     DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterLifecycle_StageEntered DEFAULT SYSUTCDATETIME(),
	CompletedUtc               DATETIME2 NULL,
	LastEvaluatedUtc           DATETIME2 NULL,
	LastSyncedUtc              DATETIME2 NULL,
	IsPrimary                  BIT NOT NULL CONSTRAINT DF_Legal_MatterLifecycle_IsPrimary DEFAULT 1,
	TenantId                   UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterLifecycle_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_MatterLifecycle_IsDeleted DEFAULT 0,
	RowVersion                 ROWVERSION
);

-- One active PRIMARY lifecycle per matter (model allows secondary lifecycles later).
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterLifecycle') AND name = N'UX_Legal_MatterLifecycle_PrimaryPerMatter')
	CREATE UNIQUE INDEX UX_Legal_MatterLifecycle_PrimaryPerMatter
		ON POLOXI.Legal_MatterLifecycle (DecisionMatterId) WHERE IsDeleted = 0 AND IsPrimary = 1;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterLifecycle') AND name = N'IX_Legal_MatterLifecycle_Tenant')
	CREATE INDEX IX_Legal_MatterLifecycle_Tenant
		ON POLOXI.Legal_MatterLifecycle (TenantId, StatusCode) INCLUDE (DecisionMatterId, CurrentStageDefinitionId) WHERE IsDeleted = 0;
GO

-- ── Legal_MatterStageHistory: IMMUTABLE record of every stage occupancy. Never overwritten. ──
IF OBJECT_ID(N'POLOXI.Legal_MatterStageHistory', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterStageHistory
(
	MatterStageHistoryId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterStageHistory PRIMARY KEY DEFAULT NEWID(),
	MatterLifecycleId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageHistory_Lifecycle REFERENCES POLOXI.Legal_MatterLifecycle (MatterLifecycleId),
	MatterStageDefinitionId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageHistory_Stage REFERENCES POLOXI.Legal_MatterStageDefinition (MatterStageDefinitionId),
	EnteredUtc               DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterStageHistory_Entered DEFAULT SYSUTCDATETIME(),
	ExitedUtc                DATETIME2 NULL,
	EntryReasonCode          NVARCHAR(60) NULL,
	ExitReasonCode           NVARCHAR(60) NULL,
	TransitionDefinitionId   UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterStageHistory_Transition REFERENCES POLOXI.Legal_MatterStageTransitionDefinition (MatterStageTransitionDefinitionId),
	ExternalSystemId         UNIQUEIDENTIFIER NULL,
	ExternalObjectId         NVARCHAR(200) NULL,
	ChangedByType            NVARCHAR(40) NULL,  -- USER | SYNC | SYSTEM
	ChangedByUserId          UNIQUEIDENTIFIER NULL,
	IsExternalAuthoritative  BIT NOT NULL CONSTRAINT DF_Legal_MatterStageHistory_ExtAuth DEFAULT 0,
	Notes                    NVARCHAR(MAX) NULL,
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterStageHistory_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_MatterStageHistory_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterStageHistory') AND name = N'IX_Legal_MatterStageHistory_Lifecycle')
	CREATE INDEX IX_Legal_MatterStageHistory_Lifecycle
		ON POLOXI.Legal_MatterStageHistory (MatterLifecycleId, EnteredUtc DESC) WHERE IsDeleted = 0;
GO

-- ── Legal_MatterStageRequirementInstance: runtime satisfaction of a stage requirement. ──
IF OBJECT_ID(N'POLOXI.Legal_MatterStageRequirementInstance', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterStageRequirementInstance
(
	MatterStageRequirementInstanceId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterStageRequirementInstance PRIMARY KEY DEFAULT NEWID(),
	MatterLifecycleId                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageReqInstance_Lifecycle REFERENCES POLOXI.Legal_MatterLifecycle (MatterLifecycleId),
	MatterStageRequirementDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageReqInstance_Def REFERENCES POLOXI.Legal_MatterStageRequirementDefinition (MatterStageRequirementDefinitionId),
	StatusCode                        NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_MatterStageReqInstance_Status DEFAULT N'NOT_STARTED', -- NOT_STARTED|IN_PROGRESS|SATISFIED|WAIVED|BLOCKED|NOT_APPLICABLE
	SatisfiedUtc                      DATETIME2 NULL,
	SatisfiedByUserId                 UNIQUEIDENTIFIER NULL,
	EvidenceSummary                   NVARCHAR(MAX) NULL,
	Notes                             NVARCHAR(MAX) NULL,
	TenantId                          UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc                    DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterStageReqInstance_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                   DATETIME2 NULL,
	ModifiedByUserId                  UNIQUEIDENTIFIER NULL,
	IsDeleted                         BIT NOT NULL CONSTRAINT DF_Legal_MatterStageReqInstance_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterStageRequirementInstance') AND name = N'IX_Legal_MatterStageReqInstance_Lifecycle')
	CREATE INDEX IX_Legal_MatterStageReqInstance_Lifecycle
		ON POLOXI.Legal_MatterStageRequirementInstance (MatterLifecycleId, StatusCode) WHERE IsDeleted = 0;
GO

-- ── Legal_MatterStageEvent: append-only operational events (powers "Since Your Last Review"). ──
IF OBJECT_ID(N'POLOXI.Legal_MatterStageEvent', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterStageEvent
(
	MatterStageEventId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterStageEvent PRIMARY KEY DEFAULT NEWID(),
	MatterLifecycleId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterStageEvent_Lifecycle REFERENCES POLOXI.Legal_MatterLifecycle (MatterLifecycleId),
	MatterStageDefinitionId  UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterStageEvent_Stage REFERENCES POLOXI.Legal_MatterStageDefinition (MatterStageDefinitionId),
	EventType                NVARCHAR(60) NOT NULL, -- STAGE_ENTERED|STAGE_EXITED|DOCUMENT_RECEIVED|TASK_COMPLETED|MILESTONE_COMPLETED|DECISION_CHANGED|…
	OccurredUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterStageEvent_Occurred DEFAULT SYSUTCDATETIME(),
	Title                    NVARCHAR(300) NOT NULL,
	Description              NVARCHAR(MAX) NULL,
	ExternalSystemId         UNIQUEIDENTIFIER NULL,
	ExternalObjectId         NVARCHAR(200) NULL,
	MetadataJson             NVARCHAR(MAX) NULL,
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterStageEvent_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_MatterStageEvent_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterStageEvent') AND name = N'IX_Legal_MatterStageEvent_Lifecycle')
	CREATE INDEX IX_Legal_MatterStageEvent_Lifecycle
		ON POLOXI.Legal_MatterStageEvent (MatterLifecycleId, OccurredUtc DESC) INCLUDE (EventType) WHERE IsDeleted = 0;
GO

COMMIT TRANSACTION;
GO
