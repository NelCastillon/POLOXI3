SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Execution & Platform tables (Table layer, phase 4).
--
-- Every public intelligence capability executes through one boundary:
--   authorization → entitlement → quota reservation → idempotency → execution.
-- Platform_IntelligenceExecution is the durable record of a queued/running/finished
-- capability run. Platform_IdempotencyRecord de-duplicates expensive POSTs per
-- tenant+operation. Platform_AuditEvent and Platform_Feedback provide the
-- operational/quality instrumentation. All carry the standard base/audit fields.
-- Capability registry values are seeded in 0245 (DB-backed, not hardcoded).
-- ─────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'SaaS') IS NULL
	EXEC(N'CREATE SCHEMA SaaS AUTHORIZATION dbo;');

-- Capability registry (DB-backed capability definitions) ───────────────────────
IF OBJECT_ID(N'SaaS.Platform_Capability',N'U') IS NULL
CREATE TABLE SaaS.Platform_Capability
(
	CapabilityId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_Capability PRIMARY KEY DEFAULT NEWID(),
	Code              NVARCHAR(80) NOT NULL,
	DisplayName       NVARCHAR(160) NOT NULL,
	EntitlementCode   NVARCHAR(80) NOT NULL,
	Permission        NVARCHAR(80) NULL,
	CustomerMeterCode NVARCHAR(80) NULL,
	RequiresMatter    BIT NOT NULL CONSTRAINT DF_Platform_Capability_RequiresMatter DEFAULT 0,
	IsMetered         BIT NOT NULL CONSTRAINT DF_Platform_Capability_IsMetered DEFAULT 1,
	IsAsync           BIT NOT NULL CONSTRAINT DF_Platform_Capability_IsAsync DEFAULT 0,
	SortOrder         INT NOT NULL CONSTRAINT DF_Platform_Capability_SortOrder DEFAULT 0,
	IsActive          BIT NOT NULL CONSTRAINT DF_Platform_Capability_Active DEFAULT 1,
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Platform_Capability_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Platform_Capability_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_Platform_Capability_Code',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Platform_Capability_Code ON SaaS.Platform_Capability (Code) WHERE IsDeleted = 0;

-- IntelligenceExecution ────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Platform_IntelligenceExecution',N'U') IS NULL
CREATE TABLE SaaS.Platform_IntelligenceExecution
(
	ExecutionId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_IntelligenceExecution PRIMARY KEY DEFAULT NEWID(),
	TenantId           UNIQUEIDENTIFIER NOT NULL,
	RequestedByUserId  UNIQUEIDENTIFIER NOT NULL,
	MatterId           UNIQUEIDENTIFIER NULL,
	CapabilityCode     NVARCHAR(80) NOT NULL,
	StatusCode         NVARCHAR(40) NOT NULL CONSTRAINT DF_Platform_IntelligenceExecution_Status DEFAULT N'Created',
	CorrelationId      NVARCHAR(120) NOT NULL,
	FailureCode        NVARCHAR(120) NULL,
	CreatedDateUtc     DATETIME2 NOT NULL CONSTRAINT DF_Platform_IntelligenceExecution_Created DEFAULT SYSUTCDATETIME(),
	StartedAtUtc       DATETIME2 NULL,
	CompletedAtUtc     DATETIME2 NULL,
	CreatedByUserId    UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc    DATETIME2 NULL,
	ModifiedByUserId   UNIQUEIDENTIFIER NULL,
	IsDeleted          BIT NOT NULL CONSTRAINT DF_Platform_IntelligenceExecution_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Platform_IntelligenceExecution_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'IX_Platform_IntelligenceExecution_TenantStatus',N'IX') IS NULL
	CREATE INDEX IX_Platform_IntelligenceExecution_TenantStatus ON SaaS.Platform_IntelligenceExecution (TenantId, StatusCode, CreatedDateUtc) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Platform_IntelligenceExecution_Correlation',N'IX') IS NULL
	CREATE INDEX IX_Platform_IntelligenceExecution_Correlation ON SaaS.Platform_IntelligenceExecution (CorrelationId) WHERE IsDeleted = 0;

-- IdempotencyRecord ────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Platform_IdempotencyRecord',N'U') IS NULL
CREATE TABLE SaaS.Platform_IdempotencyRecord
(
	IdempotencyRecordId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_IdempotencyRecord PRIMARY KEY DEFAULT NEWID(),
	TenantId            UNIQUEIDENTIFIER NOT NULL,
	IdempotencyKey      NVARCHAR(200) NOT NULL,
	Operation           NVARCHAR(120) NOT NULL,
	ExecutionId         UNIQUEIDENTIFIER NULL,
	ResponseCode        INT NULL,
	ExpiresAtUtc        DATETIME2 NOT NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Platform_IdempotencyRecord_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Platform_IdempotencyRecord_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Platform_IdempotencyRecord_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'UX_Platform_IdempotencyRecord',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Platform_IdempotencyRecord ON SaaS.Platform_IdempotencyRecord (TenantId, Operation, IdempotencyKey) WHERE IsDeleted = 0;

-- AuditEvent ───────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Platform_AuditEvent',N'U') IS NULL
CREATE TABLE SaaS.Platform_AuditEvent
(
	AuditEventId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_AuditEvent PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NULL,
	UserId            UNIQUEIDENTIFIER NULL,
	EventType         NVARCHAR(80) NOT NULL,
	ExecutionId       UNIQUEIDENTIFIER NULL,
	ResourceType      NVARCHAR(80) NULL,
	ResourceId        UNIQUEIDENTIFIER NULL,
	DataJson          NVARCHAR(MAX) NULL,
	CorrelationId     NVARCHAR(120) NULL,
	OccurredAtUtc     DATETIME2 NOT NULL CONSTRAINT DF_Platform_AuditEvent_Occurred DEFAULT SYSUTCDATETIME(),
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Platform_AuditEvent_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Platform_AuditEvent_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Platform_AuditEvent_TenantType',N'IX') IS NULL
	CREATE INDEX IX_Platform_AuditEvent_TenantType ON SaaS.Platform_AuditEvent (TenantId, EventType, OccurredAtUtc) WHERE IsDeleted = 0;

-- Feedback ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Platform_Feedback',N'U') IS NULL
CREATE TABLE SaaS.Platform_Feedback
(
	FeedbackId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_Feedback PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	UserId            UNIQUEIDENTIFIER NOT NULL,
	ExecutionId       UNIQUEIDENTIFIER NULL,
	CapabilityCode    NVARCHAR(80) NULL,
	ResultId          UNIQUEIDENTIFIER NULL,
	IsUseful          BIT NULL,
	ReasonableCode    NVARCHAR(20) NULL,  -- Yes | Partial | No
	MissedSomething   BIT NULL,
	UnsupportedClaim  BIT NULL,
	Comments          NVARCHAR(MAX) NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Platform_Feedback_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Platform_Feedback_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Platform_Feedback_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'IX_Platform_Feedback_TenantCapability',N'IX') IS NULL
	CREATE INDEX IX_Platform_Feedback_TenantCapability ON SaaS.Platform_Feedback (TenantId, CapabilityCode, CreatedDateUtc) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
