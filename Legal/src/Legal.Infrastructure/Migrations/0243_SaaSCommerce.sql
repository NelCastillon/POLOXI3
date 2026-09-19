SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Commerce tables (Table layer, phase 3).
--
-- Plan ≠ Subscription ≠ Entitlement ≠ Usage ≠ Billing. Entitlements resolve from
-- the subscribed plan plus per-tenant overrides. Usage is append-only; reservations
-- guarantee atomic quota checks so two concurrent runs cannot both pass a limit.
-- All tables carry the standard base/audit fields. Reference/seed data (plans,
-- entitlements, limits) is inserted in 0245 so the DB remains the source of truth.
-- ─────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'SaaS') IS NULL
	EXEC(N'CREATE SCHEMA SaaS AUTHORIZATION dbo;');

-- ProductPlan ──────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Commerce_ProductPlan',N'U') IS NULL
CREATE TABLE SaaS.Commerce_ProductPlan
(
	PlanId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Commerce_ProductPlan PRIMARY KEY DEFAULT NEWID(),
	Code              NVARCHAR(60) NOT NULL,
	Name              NVARCHAR(160) NOT NULL,
	Description       NVARCHAR(600) NULL,
	PriceAmount       DECIMAL(18,2) NOT NULL CONSTRAINT DF_Commerce_ProductPlan_Price DEFAULT 0,
	CurrencyCode      NVARCHAR(10) NOT NULL CONSTRAINT DF_Commerce_ProductPlan_Currency DEFAULT N'USD',
	BillingPeriodCode NVARCHAR(30) NOT NULL CONSTRAINT DF_Commerce_ProductPlan_Period DEFAULT N'Monthly',
	IsPublic          BIT NOT NULL CONSTRAINT DF_Commerce_ProductPlan_Public DEFAULT 1,
	IsActive          BIT NOT NULL CONSTRAINT DF_Commerce_ProductPlan_Active DEFAULT 1,
	SortOrder         INT NOT NULL CONSTRAINT DF_Commerce_ProductPlan_SortOrder DEFAULT 0,
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Commerce_ProductPlan_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Commerce_ProductPlan_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_Commerce_ProductPlan_Code',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Commerce_ProductPlan_Code ON SaaS.Commerce_ProductPlan (Code) WHERE IsDeleted = 0;

-- Entitlement (catalog of capabilities/limits) ─────────────────────────────────
IF OBJECT_ID(N'SaaS.Commerce_Entitlement',N'U') IS NULL
CREATE TABLE SaaS.Commerce_Entitlement
(
	EntitlementId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Commerce_Entitlement PRIMARY KEY DEFAULT NEWID(),
	Code              NVARCHAR(80) NOT NULL,
	DisplayName       NVARCHAR(160) NOT NULL,
	EntitlementKind   NVARCHAR(30) NOT NULL CONSTRAINT DF_Commerce_Entitlement_Kind DEFAULT N'Feature', -- Feature | Limit
	Description       NVARCHAR(400) NULL,
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Commerce_Entitlement_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Commerce_Entitlement_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_Commerce_Entitlement_Code',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Commerce_Entitlement_Code ON SaaS.Commerce_Entitlement (Code) WHERE IsDeleted = 0;

-- PlanEntitlement (plan grants a feature / sets a limit) ────────────────────────
IF OBJECT_ID(N'SaaS.Commerce_PlanEntitlement',N'U') IS NULL
CREATE TABLE SaaS.Commerce_PlanEntitlement
(
	PlanEntitlementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Commerce_PlanEntitlement PRIMARY KEY DEFAULT NEWID(),
	PlanId            UNIQUEIDENTIFIER NOT NULL,
	EntitlementId     UNIQUEIDENTIFIER NOT NULL,
	IsEnabled         BIT NOT NULL CONSTRAINT DF_Commerce_PlanEntitlement_Enabled DEFAULT 1,
	LimitValue        BIGINT NULL, -- NULL for pure feature flags
	LimitPeriodCode   NVARCHAR(30) NULL, -- e.g. Monthly, Total
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Commerce_PlanEntitlement_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Commerce_PlanEntitlement_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Commerce_PlanEntitlement_Plan FOREIGN KEY (PlanId) REFERENCES SaaS.Commerce_ProductPlan (PlanId),
	CONSTRAINT FK_Commerce_PlanEntitlement_Entitlement FOREIGN KEY (EntitlementId) REFERENCES SaaS.Commerce_Entitlement (EntitlementId)
);

IF OBJECT_ID(N'UX_Commerce_PlanEntitlement',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Commerce_PlanEntitlement ON SaaS.Commerce_PlanEntitlement (PlanId, EntitlementId) WHERE IsDeleted = 0;

-- Subscription ─────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Commerce_Subscription',N'U') IS NULL
CREATE TABLE SaaS.Commerce_Subscription
(
	SubscriptionId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Commerce_Subscription PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	PlanId            UNIQUEIDENTIFIER NOT NULL,
	StatusCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_Commerce_Subscription_Status DEFAULT N'Active',
	StartedAtUtc      DATETIME2 NOT NULL CONSTRAINT DF_Commerce_Subscription_Started DEFAULT SYSUTCDATETIME(),
	CurrentPeriodStartUtc DATETIME2 NULL,
	CurrentPeriodEndUtc   DATETIME2 NULL,
	CancelledAtUtc    DATETIME2 NULL,
	ExternalReference NVARCHAR(200) NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Commerce_Subscription_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Commerce_Subscription_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Commerce_Subscription_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId),
	CONSTRAINT FK_Commerce_Subscription_Plan FOREIGN KEY (PlanId) REFERENCES SaaS.Commerce_ProductPlan (PlanId)
);

IF OBJECT_ID(N'IX_Commerce_Subscription_TenantId',N'IX') IS NULL
	CREATE INDEX IX_Commerce_Subscription_TenantId ON SaaS.Commerce_Subscription (TenantId) WHERE IsDeleted = 0;

-- TenantEntitlementOverride ────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Commerce_TenantEntitlementOverride',N'U') IS NULL
CREATE TABLE SaaS.Commerce_TenantEntitlementOverride
(
	OverrideId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Commerce_TenantEntitlementOverride PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	EntitlementId     UNIQUEIDENTIFIER NOT NULL,
	IsEnabled         BIT NULL,
	LimitValue        BIGINT NULL,
	LimitPeriodCode   NVARCHAR(30) NULL,
	Reason            NVARCHAR(400) NULL,
	ExpiresAtUtc      DATETIME2 NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Commerce_TenantEntitlementOverride_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Commerce_TenantEntitlementOverride_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Commerce_TenantEntitlementOverride_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId),
	CONSTRAINT FK_Commerce_TenantEntitlementOverride_Entitlement FOREIGN KEY (EntitlementId) REFERENCES SaaS.Commerce_Entitlement (EntitlementId)
);

IF OBJECT_ID(N'UX_Commerce_TenantEntitlementOverride',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Commerce_TenantEntitlementOverride ON SaaS.Commerce_TenantEntitlementOverride (TenantId, EntitlementId) WHERE IsDeleted = 0;

-- UsageLedger (append-only) ────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Commerce_UsageLedger',N'U') IS NULL
CREATE TABLE SaaS.Commerce_UsageLedger
(
	UsageId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Commerce_UsageLedger PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	UserId            UNIQUEIDENTIFIER NULL,
	ExecutionId       UNIQUEIDENTIFIER NULL,
	MatterId          UNIQUEIDENTIFIER NULL,
	MeterCode         NVARCHAR(80) NOT NULL,
	UsageClass        NVARCHAR(20) NOT NULL CONSTRAINT DF_Commerce_UsageLedger_Class DEFAULT N'Customer', -- Customer | Internal
	Quantity          DECIMAL(18,4) NOT NULL CONSTRAINT DF_Commerce_UsageLedger_Quantity DEFAULT 0,
	Provider          NVARCHAR(80) NULL,
	Model             NVARCHAR(120) NULL,
	InternalCost      DECIMAL(18,6) NULL,
	OccurredAtUtc     DATETIME2 NOT NULL CONSTRAINT DF_Commerce_UsageLedger_Occurred DEFAULT SYSUTCDATETIME(),
	CorrelationId     NVARCHAR(120) NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Commerce_UsageLedger_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Commerce_UsageLedger_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Commerce_UsageLedger_TenantMeter',N'IX') IS NULL
	CREATE INDEX IX_Commerce_UsageLedger_TenantMeter ON SaaS.Commerce_UsageLedger (TenantId, MeterCode, UsageClass, OccurredAtUtc) WHERE IsDeleted = 0;

-- UsageReservation (atomic quota hold) ─────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Commerce_UsageReservation',N'U') IS NULL
CREATE TABLE SaaS.Commerce_UsageReservation
(
	ReservationId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Commerce_UsageReservation PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	ExecutionId       UNIQUEIDENTIFIER NULL,
	MeterCode         NVARCHAR(80) NOT NULL,
	Quantity          DECIMAL(18,4) NOT NULL CONSTRAINT DF_Commerce_UsageReservation_Quantity DEFAULT 1,
	StatusCode        NVARCHAR(30) NOT NULL CONSTRAINT DF_Commerce_UsageReservation_Status DEFAULT N'Reserved', -- Reserved | Committed | Released
	ExpiresAtUtc      DATETIME2 NOT NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Commerce_UsageReservation_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Commerce_UsageReservation_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Commerce_UsageReservation_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'IX_Commerce_UsageReservation_TenantMeter',N'IX') IS NULL
	CREATE INDEX IX_Commerce_UsageReservation_TenantMeter ON SaaS.Commerce_UsageReservation (TenantId, MeterCode, StatusCode) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
