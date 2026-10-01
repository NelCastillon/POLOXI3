SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- POLOXI Legal Decision Intelligence — Domain Pack entity/event taxonomy,
-- terminology, and evidence-type→signal map (the formal resolution surface the
-- Decision Channels and the document semantic interpreter resolve through), plus
-- a per-document extraction store for domain entities/events.
--
-- This EXTENDS migration 0284 (Legal_DecisionDomainPack). It supplies DOMAIN
-- SEMANTICS ONLY: entity/event vocabularies, synonym terminology for conservative
-- node matching, and a qualitative evidence-type→POLOXI TargetSignal/Relation map.
-- It assigns NO numeric scores — POLOXI Core remains the sole owner of candidate
-- competition, uncertainty, IV, and recompetition. Global (TenantId NULL) rows are
-- seeded defaults; tenant rows override (mirrors 0284). All objects live in POLOXI,
-- are prefixed Legal_Decision*/Legal_Document*, and carry base/audit fields.
-- Idempotent; safe to re-run.
-- ============================================================================

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Legal_DecisionDomainEntityType: domain entity vocabulary (Claimant, Defendant, Provider, Vehicle, …). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainEntityType', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainEntityType
(
	DecisionDomainEntityTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainEntityType PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainEntityType_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	EntityTypeCode             NVARCHAR(60) NOT NULL,
	Name                       NVARCHAR(200) NOT NULL,
	DimensionCode              NVARCHAR(60) NULL,
	Description                NVARCHAR(1000) NULL,
	SortOrder                  INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainEntityType_SortOrder DEFAULT 0,
	IsActive                   BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainEntityType_IsActive DEFAULT 1,
	TenantId                   UNIQUEIDENTIFIER NULL,
	CreatedDateUtc             DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainEntityType_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId            UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc            DATETIME2 NULL,
	ModifiedByUserId           UNIQUEIDENTIFIER NULL,
	IsDeleted                  BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainEntityType_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainEntityType') AND name = N'IX_Legal_DecisionDomainEntityType_Pack')
	CREATE INDEX IX_Legal_DecisionDomainEntityType_Pack
		ON POLOXI.Legal_DecisionDomainEntityType (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (EntityTypeCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionDomainEventType: domain event vocabulary (Collision, Impact, Treatment, Surgery, …). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainEventType', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainEventType
(
	DecisionDomainEventTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainEventType PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainEventType_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	EventTypeCode             NVARCHAR(60) NOT NULL,
	Name                      NVARCHAR(200) NOT NULL,
	DimensionCode             NVARCHAR(60) NULL,
	Description               NVARCHAR(1000) NULL,
	SortOrder                 INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainEventType_SortOrder DEFAULT 0,
	IsActive                  BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainEventType_IsActive DEFAULT 1,
	TenantId                  UNIQUEIDENTIFIER NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainEventType_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainEventType_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainEventType') AND name = N'IX_Legal_DecisionDomainEventType_Pack')
	CREATE INDEX IX_Legal_DecisionDomainEventType_Pack
		ON POLOXI.Legal_DecisionDomainEventType (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (EventTypeCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionDomainTerm: terminology/synonyms used for conservative, domain-aware node matching. ──
-- TermText is a surface form; CanonicalCode is the concept/entity/event/dimension it maps to. This lets
-- the ChannelNodeTextMatcher expand a node/assertion with pack synonyms instead of raw token overlap only.
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainTerm', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainTerm
(
	DecisionDomainTermId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainTerm PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainTerm_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	TermText             NVARCHAR(200) NOT NULL,
	CanonicalCode        NVARCHAR(60) NOT NULL,
	TermKindCode         NVARCHAR(40) NOT NULL,   -- CONCEPT | ENTITY | EVENT | DIMENSION
	Weight               INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainTerm_Weight DEFAULT 1,
	IsActive             BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainTerm_IsActive DEFAULT 1,
	TenantId             UNIQUEIDENTIFIER NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainTerm_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainTerm_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainTerm') AND name = N'IX_Legal_DecisionDomainTerm_Pack')
	CREATE INDEX IX_Legal_DecisionDomainTerm_Pack
		ON POLOXI.Legal_DecisionDomainTerm (DecisionDomainPackId, IsActive) INCLUDE (CanonicalCode, TermKindCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionDomainSignalMap: qualitative evidence-type → POLOXI TargetSignal/Relation map. ──
-- Supplies the channels with a domain-pack-driven answer to "which POLOXI signal does this evidence
-- type inform, and with what relation?" — replacing the hardcoded EvidenceSupport/SUPPORTS defaults.
-- No numeric level is stored: POLOXI resolves the effective value.
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainSignalMap', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainSignalMap
(
	DecisionDomainSignalMapId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainSignalMap PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainSignalMap_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	EvidenceTypeCode          NVARCHAR(60) NOT NULL,
	TargetSignalCode          NVARCHAR(60) NOT NULL,   -- EvidenceSupport | FactSupport | AuthoritySupport | LegalSupport | Uncertainty
	RelationCode              NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionDomainSignalMap_Relation DEFAULT N'SUPPORTS',
	DimensionCode             NVARCHAR(60) NULL,
	SortOrder                 INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainSignalMap_SortOrder DEFAULT 0,
	IsActive                  BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainSignalMap_IsActive DEFAULT 1,
	TenantId                  UNIQUEIDENTIFIER NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainSignalMap_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainSignalMap_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainSignalMap') AND name = N'IX_Legal_DecisionDomainSignalMap_Pack')
	CREATE INDEX IX_Legal_DecisionDomainSignalMap_Pack
		ON POLOXI.Legal_DecisionDomainSignalMap (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (EvidenceTypeCode, TargetSignalCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DocumentDomainEntity: per-document-version extracted domain entities (provenance-preserving). ──
IF OBJECT_ID(N'POLOXI.Legal_DocumentDomainEntity', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DocumentDomainEntity
(
	DocumentDomainEntityId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DocumentDomainEntity PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId         UNIQUEIDENTIFIER NOT NULL,
	LegalDocumentId          UNIQUEIDENTIFIER NOT NULL,
	LegalDocumentVersionId   UNIQUEIDENTIFIER NOT NULL,
	LegalDocumentPassageId   UNIQUEIDENTIFIER NULL,
	DomainPackCode           NVARCHAR(60) NULL,
	EntityTypeCode           NVARCHAR(60) NOT NULL,
	DimensionCode            NVARCHAR(60) NULL,
	EntityText               NVARCHAR(400) NOT NULL,
	NormalizedValue          NVARCHAR(400) NULL,
	Confidence               DECIMAL(5,4) NULL,
	ProposedByModel          NVARCHAR(120) NULL,
	PromptRunId              NVARCHAR(120) NULL,
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentDomainEntity_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_DocumentDomainEntity_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DocumentDomainEntity') AND name = N'IX_Legal_DocumentDomainEntity_Matter')
	CREATE INDEX IX_Legal_DocumentDomainEntity_Matter
		ON POLOXI.Legal_DocumentDomainEntity (TenantId, DecisionMatterId, LegalDocumentVersionId) INCLUDE (EntityTypeCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DocumentDomainEvent: per-document-version extracted domain events (provenance-preserving). ──
IF OBJECT_ID(N'POLOXI.Legal_DocumentDomainEvent', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DocumentDomainEvent
(
	DocumentDomainEventId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DocumentDomainEvent PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId         UNIQUEIDENTIFIER NOT NULL,
	LegalDocumentId          UNIQUEIDENTIFIER NOT NULL,
	LegalDocumentVersionId   UNIQUEIDENTIFIER NOT NULL,
	LegalDocumentPassageId   UNIQUEIDENTIFIER NULL,
	DomainPackCode           NVARCHAR(60) NULL,
	EventTypeCode            NVARCHAR(60) NOT NULL,
	DimensionCode            NVARCHAR(60) NULL,
	Summary                  NVARCHAR(1000) NOT NULL,
	EventDateUtc             DATETIME2 NULL,
	Confidence               DECIMAL(5,4) NULL,
	ProposedByModel          NVARCHAR(120) NULL,
	PromptRunId              NVARCHAR(120) NULL,
	TenantId                 UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentDomainEvent_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_DocumentDomainEvent_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DocumentDomainEvent') AND name = N'IX_Legal_DocumentDomainEvent_Matter')
	CREATE INDEX IX_Legal_DocumentDomainEvent_Matter
		ON POLOXI.Legal_DocumentDomainEvent (TenantId, DecisionMatterId, LegalDocumentVersionId) INCLUDE (EventTypeCode) WHERE IsDeleted = 0;

GO

-- ════════════════════════════════════════════════════════════════════════════
-- Seed PI_GENERAL / PERSONAL_INJURY defaults (global, TenantId NULL). Idempotent.
-- ════════════════════════════════════════════════════════════════════════════
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
BEGIN
	-- ── Entity types ──
	MERGE POLOXI.Legal_DecisionDomainEntityType AS target
	USING (VALUES
		(N'CLAIMANT',   N'Claimant / Plaintiff',      N'LIABILITY', N'The injured party asserting the claim.',          10),
		(N'DEFENDANT',  N'Defendant / Tortfeasor',    N'LIABILITY', N'The party alleged to be at fault.',               20),
		(N'PROVIDER',   N'Medical Provider',          N'INJURY',    N'Treating physician, hospital, or clinic.',        30),
		(N'INSURER',    N'Insurer / Carrier',         N'INSURANCE', N'Insurance carrier relevant to coverage/limits.',  40),
		(N'VEHICLE',    N'Vehicle',                   N'LIABILITY', N'A vehicle involved in the incident.',             50),
		(N'WITNESS',    N'Witness',                   N'LIABILITY', N'A lay witness to the incident.',                  60),
		(N'EXPERT',     N'Expert',                    N'CAUSATION', N'A retained or testifying expert.',                70),
		(N'BODY_PART',  N'Injured Body Part',         N'INJURY',    N'An anatomical region of claimed injury.',         80),
		(N'EMPLOYER',   N'Employer',                  N'DAMAGES',   N'Employer relevant to lost-earnings damages.',     90)
	) AS source (EntityTypeCode, Name, DimensionCode, Description, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.EntityTypeCode = source.EntityTypeCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, EntityTypeCode, Name, DimensionCode, Description, SortOrder, TenantId)
		VALUES (@PiPackId, source.EntityTypeCode, source.Name, source.DimensionCode, source.Description, source.SortOrder, NULL);

	-- ── Event types ──
	MERGE POLOXI.Legal_DecisionDomainEventType AS target
	USING (VALUES
		(N'COLLISION',   N'Collision / Incident',   N'LIABILITY', N'The underlying accident or incident.',           10),
		(N'IMPACT',      N'Impact',                 N'LIABILITY', N'Point-of-contact event within the incident.',    20),
		(N'TREATMENT',   N'Medical Treatment',      N'INJURY',    N'A treatment encounter for the injury.',          30),
		(N'SURGERY',     N'Surgery',                N'INJURY',    N'A surgical procedure.',                          40),
		(N'DIAGNOSIS',   N'Diagnosis',              N'INJURY',    N'A clinical diagnosis of injury.',                50),
		(N'WORK_ABSENCE',N'Work Absence',           N'DAMAGES',   N'A period of missed work / lost earnings.',       60),
		(N'DEMAND',      N'Demand',                 N'SETTLEMENT',N'A settlement demand.',                           70),
		(N'OFFER',       N'Offer',                  N'SETTLEMENT',N'A settlement offer.',                            80)
	) AS source (EventTypeCode, Name, DimensionCode, Description, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.EventTypeCode = source.EventTypeCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, EventTypeCode, Name, DimensionCode, Description, SortOrder, TenantId)
		VALUES (@PiPackId, source.EventTypeCode, source.Name, source.DimensionCode, source.Description, source.SortOrder, NULL);

	-- ── Terminology / synonyms (surface form → canonical code) ──
	MERGE POLOXI.Legal_DecisionDomainTerm AS target
	USING (VALUES
		(N'plaintiff',       N'CLAIMANT',  N'ENTITY', 1),
		(N'injured party',   N'CLAIMANT',  N'ENTITY', 1),
		(N'tortfeasor',      N'DEFENDANT', N'ENTITY', 1),
		(N'at-fault driver', N'DEFENDANT', N'ENTITY', 1),
		(N'physician',       N'PROVIDER',  N'ENTITY', 1),
		(N'treating doctor', N'PROVIDER',  N'ENTITY', 1),
		(N'carrier',         N'INSURER',   N'ENTITY', 1),
		(N'crash',           N'COLLISION', N'EVENT',  1),
		(N'accident',        N'COLLISION', N'EVENT',  1),
		(N'wreck',           N'COLLISION', N'EVENT',  1),
		(N'rear-ended',      N'IMPACT',    N'EVENT',  1),
		(N'operation',       N'SURGERY',   N'EVENT',  1),
		(N'fault',           N'LIABILITY', N'DIMENSION', 1),
		(N'negligence',      N'LIABILITY', N'DIMENSION', 1),
		(N'proximate cause', N'CAUSATION', N'DIMENSION', 1)
	) AS source (TermText, CanonicalCode, TermKindCode, Weight)
	ON target.DecisionDomainPackId = @PiPackId AND target.TermText = source.TermText AND target.CanonicalCode = source.CanonicalCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, TermText, CanonicalCode, TermKindCode, Weight, TenantId)
		VALUES (@PiPackId, source.TermText, source.CanonicalCode, source.TermKindCode, source.Weight, NULL);

	-- ── Evidence-type → POLOXI TargetSignal / Relation map ──
	MERGE POLOXI.Legal_DecisionDomainSignalMap AS target
	USING (VALUES
		(N'MEDICAL_RECORDS',   N'EvidenceSupport', N'SUPPORTS',    N'INJURY',     10),
		(N'IMAGING',           N'EvidenceSupport', N'SUPPORTS',    N'INJURY',     20),
		(N'MEDICAL_BILLS',     N'FactSupport',     N'ESTABLISHES', N'DAMAGES',    30),
		(N'PHOTOS_VIDEO',      N'EvidenceSupport', N'SUPPORTS',    N'LIABILITY',  40),
		(N'WITNESS_STATEMENTS',N'FactSupport',     N'SUPPORTS',    N'LIABILITY',  50),
		(N'EXPERT_REPORTS',    N'AuthoritySupport',N'SUPPORTS',    N'CAUSATION',  60),
		(N'ACCIDENT_REPORT',   N'FactSupport',     N'ESTABLISHES', N'LIABILITY',  70),
		(N'DEPOSITION',        N'FactSupport',     N'SUPPORTS',    N'LIABILITY',  80),
		(N'WAGE_RECORDS',      N'FactSupport',     N'ESTABLISHES', N'DAMAGES',    90),
		(N'DEMAND_OFFER',      N'FactSupport',     N'CONTEXT_ONLY',N'SETTLEMENT',100)
	) AS source (EvidenceTypeCode, TargetSignalCode, RelationCode, DimensionCode, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.EvidenceTypeCode = source.EvidenceTypeCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, EvidenceTypeCode, TargetSignalCode, RelationCode, DimensionCode, SortOrder, TenantId)
		VALUES (@PiPackId, source.EvidenceTypeCode, source.TargetSignalCode, source.RelationCode, source.DimensionCode, source.SortOrder, NULL);
END

COMMIT TRANSACTION;
