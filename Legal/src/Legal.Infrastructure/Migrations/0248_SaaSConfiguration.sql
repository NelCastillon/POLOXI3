SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Configuration control plane (Table layer).
--
-- Metadata-driven, DB-backed configuration:
--   Config_Definition  : authoritative registry of every configurable key
--                        (type, default, scope flags, override policy, sensitivity,
--                        validation, required permission).
--   Config_PlatformValue / Config_TenantValue / Config_MatterValue :
--                        scoped values. Effective value resolves via precedence
--                        Execution → Matter → Tenant → Plan → Platform → Code default,
--                        but only when the definition permits the lower scope AND
--                        TenantCanOverride allows it.
--   Config_FeatureFlag / Config_FeatureFlagTarget : feature flags kept separate
--                        from normal configuration (freeze §17).
--   Config_ChangeHistory : append-only record of every configuration mutation
--                        (freeze §23/§24). Secrets record only Changed=true.
--
-- Configuration NEVER grants access. Entitlement/permission/policy/resource-access
-- remain the gates; configuration only shapes how a permitted capability behaves.
-- All tables carry the standard base/audit fields. Idempotent (safe to re-run).
-- ─────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'SaaS') IS NULL
	EXEC(N'CREATE SCHEMA SaaS AUTHORIZATION dbo;');

-- Config_Definition ───────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Config_Definition', N'U') IS NULL
CREATE TABLE SaaS.Config_Definition
(
	ConfigDefinitionId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Config_Definition PRIMARY KEY DEFAULT NEWID(),
	ConfigKey            NVARCHAR(160) NOT NULL,
	Category             NVARCHAR(60) NOT NULL,
	DisplayName          NVARCHAR(200) NOT NULL,
	Description          NVARCHAR(1000) NULL,
	ValueType            NVARCHAR(30) NOT NULL,          -- String | Integer | Boolean | Decimal | Enum | Json | SecretReference
	DefaultValueJson     NVARCHAR(MAX) NOT NULL,
	Scope                NVARCHAR(20) NOT NULL CONSTRAINT DF_Config_Definition_Scope DEFAULT N'Platform', -- Platform | Tenant
	TenantConfigurable   BIT NOT NULL CONSTRAINT DF_Config_Definition_TenantCfg DEFAULT 0,
	MatterConfigurable   BIT NOT NULL CONSTRAINT DF_Config_Definition_MatterCfg DEFAULT 0,
	ExecutionConfigurable BIT NOT NULL CONSTRAINT DF_Config_Definition_ExecCfg DEFAULT 0,
	TenantCanOverride    BIT NOT NULL CONSTRAINT DF_Config_Definition_CanOverride DEFAULT 1,
	Sensitivity          NVARCHAR(20) NOT NULL CONSTRAINT DF_Config_Definition_Sensitivity DEFAULT N'Public', -- Public | Internal | Sensitive | Secret
	RequiresRestart      BIT NOT NULL CONSTRAINT DF_Config_Definition_Restart DEFAULT 0,
	RequiredPermission   NVARCHAR(120) NULL,
	ValidationJson       NVARCHAR(MAX) NULL,             -- { "min":1, "max":100, "options":[...] }
	SortOrder            INT NOT NULL CONSTRAINT DF_Config_Definition_Sort DEFAULT 0,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Config_Definition_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Config_Definition_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_Config_Definition_Key', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Config_Definition_Key ON SaaS.Config_Definition (ConfigKey) WHERE IsDeleted = 0;

-- Config_PlatformValue ────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Config_PlatformValue', N'U') IS NULL
CREATE TABLE SaaS.Config_PlatformValue
(
	ConfigPlatformValueId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Config_PlatformValue PRIMARY KEY DEFAULT NEWID(),
	ConfigKey            NVARCHAR(160) NOT NULL,
	ValueJson            NVARCHAR(MAX) NULL,
	SecretReference      NVARCHAR(400) NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Config_PlatformValue_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Config_PlatformValue_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_Config_PlatformValue_Key', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Config_PlatformValue_Key ON SaaS.Config_PlatformValue (ConfigKey) WHERE IsDeleted = 0;

-- Config_TenantValue ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Config_TenantValue', N'U') IS NULL
CREATE TABLE SaaS.Config_TenantValue
(
	ConfigTenantValueId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Config_TenantValue PRIMARY KEY DEFAULT NEWID(),
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	ConfigKey            NVARCHAR(160) NOT NULL,
	ValueJson            NVARCHAR(MAX) NULL,
	SecretReference      NVARCHAR(400) NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Config_TenantValue_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Config_TenantValue_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Config_TenantValue_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'UX_Config_TenantValue_Key', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Config_TenantValue_Key ON SaaS.Config_TenantValue (TenantId, ConfigKey) WHERE IsDeleted = 0;

-- Config_MatterValue ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Config_MatterValue', N'U') IS NULL
CREATE TABLE SaaS.Config_MatterValue
(
	ConfigMatterValueId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Config_MatterValue PRIMARY KEY DEFAULT NEWID(),
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	MatterId             UNIQUEIDENTIFIER NOT NULL,
	ConfigKey            NVARCHAR(160) NOT NULL,
	ValueJson            NVARCHAR(MAX) NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Config_MatterValue_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Config_MatterValue_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Config_MatterValue_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'UX_Config_MatterValue_Key', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Config_MatterValue_Key ON SaaS.Config_MatterValue (TenantId, MatterId, ConfigKey) WHERE IsDeleted = 0;

-- Config_FeatureFlag ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Config_FeatureFlag', N'U') IS NULL
CREATE TABLE SaaS.Config_FeatureFlag
(
	ConfigFeatureFlagId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Config_FeatureFlag PRIMARY KEY DEFAULT NEWID(),
	FlagKey              NVARCHAR(160) NOT NULL,
	DisplayName          NVARCHAR(200) NOT NULL,
	Description          NVARCHAR(1000) NULL,
	IsEnabledGlobally    BIT NOT NULL CONSTRAINT DF_Config_FeatureFlag_Global DEFAULT 0,
	RolloutPercentage    INT NULL,                       -- 0..100
	Environment          NVARCHAR(40) NULL,
	StartsAtUtc          DATETIME2 NULL,
	EndsAtUtc            DATETIME2 NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Config_FeatureFlag_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Config_FeatureFlag_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_Config_FeatureFlag_Key', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Config_FeatureFlag_Key ON SaaS.Config_FeatureFlag (FlagKey) WHERE IsDeleted = 0;

-- Config_FeatureFlagTarget ────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Config_FeatureFlagTarget', N'U') IS NULL
CREATE TABLE SaaS.Config_FeatureFlagTarget
(
	ConfigFeatureFlagTargetId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Config_FeatureFlagTarget PRIMARY KEY DEFAULT NEWID(),
	ConfigFeatureFlagId  UNIQUEIDENTIFIER NOT NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	IsEnabled            BIT NOT NULL CONSTRAINT DF_Config_FeatureFlagTarget_Enabled DEFAULT 1,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Config_FeatureFlagTarget_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Config_FeatureFlagTarget_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Config_FeatureFlagTarget_Flag FOREIGN KEY (ConfigFeatureFlagId) REFERENCES SaaS.Config_FeatureFlag (ConfigFeatureFlagId),
	CONSTRAINT FK_Config_FeatureFlagTarget_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'UX_Config_FeatureFlagTarget', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Config_FeatureFlagTarget ON SaaS.Config_FeatureFlagTarget (ConfigFeatureFlagId, TenantId) WHERE IsDeleted = 0;

-- Config_ChangeHistory (append-only) ──────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Config_ChangeHistory', N'U') IS NULL
CREATE TABLE SaaS.Config_ChangeHistory
(
	ConfigChangeHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Config_ChangeHistory PRIMARY KEY DEFAULT NEWID(),
	TenantId             UNIQUEIDENTIFIER NULL,
	MatterId             UNIQUEIDENTIFIER NULL,
	ConfigKey            NVARCHAR(160) NOT NULL,
	Scope                NVARCHAR(20) NOT NULL,          -- Platform | Tenant | Matter
	OldValueJson         NVARCHAR(MAX) NULL,             -- [REDACTED] for secrets
	NewValueJson         NVARCHAR(MAX) NULL,             -- [REDACTED] for secrets
	ActorUserId          UNIQUEIDENTIFIER NULL,
	CorrelationId        NVARCHAR(120) NULL,
	OccurredAtUtc        DATETIME2 NOT NULL CONSTRAINT DF_Config_ChangeHistory_Occurred DEFAULT SYSUTCDATETIME(),
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Config_ChangeHistory_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Config_ChangeHistory_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Config_ChangeHistory_KeyScope', N'IX') IS NULL
	CREATE INDEX IX_Config_ChangeHistory_KeyScope ON SaaS.Config_ChangeHistory (ConfigKey, Scope, OccurredAtUtc) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_Config_ChangeHistory_Tenant', N'IX') IS NULL
	CREATE INDEX IX_Config_ChangeHistory_Tenant ON SaaS.Config_ChangeHistory (TenantId, OccurredAtUtc) WHERE IsDeleted = 0;

-- ─────────────────────────────────────────────────────────────────────────────
-- Seed (DB-backed, idempotent) — configuration permissions, definitions, and
-- platform defaults. These are the launch defaults; values stay editable in DB.
-- ─────────────────────────────────────────────────────────────────────────────

-- Granular configuration permissions (freeze §25). admin.configuration.* is
-- tenant-scoped; platform.configuration.* is Super Admin only. admin.* can never
-- grant platform.* — enforced by role grants below.
INSERT INTO SaaS.SaaS_Permission (Code, DisplayName, Category, Description)
SELECT v.Code, v.DisplayName, v.Category, v.Description
FROM (VALUES
	(N'admin.configuration.read',                 N'Read Tenant Configuration',        N'Configuration', N'View tenant configuration values.'),
	(N'admin.configuration.general.manage',       N'Manage General Config',            N'Configuration', N'Manage tenant general configuration.'),
	(N'admin.configuration.research.manage',      N'Manage Research Config',           N'Configuration', N'Manage tenant research configuration.'),
	(N'admin.configuration.legal.manage',         N'Manage Legal Config',              N'Configuration', N'Manage tenant legal configuration.'),
	(N'admin.configuration.math.manage',          N'Manage Math Config',               N'Configuration', N'Manage tenant math configuration.'),
	(N'admin.configuration.notifications.manage', N'Manage Notifications Config',       N'Configuration', N'Manage tenant notifications configuration.'),
	(N'admin.configuration.advanced.manage',      N'Manage Advanced Config',           N'Configuration', N'Manage tenant advanced configuration.'),
	(N'platform.configuration.read',              N'Read Platform Configuration',      N'PlatformConfiguration', N'View platform configuration values.'),
	(N'platform.configuration.general.manage',    N'Manage Platform General',          N'PlatformConfiguration', N'Manage platform general configuration.'),
	(N'platform.configuration.identity.manage',   N'Manage Platform Identity',         N'PlatformConfiguration', N'Manage platform identity configuration.'),
	(N'platform.configuration.tenancy.manage',    N'Manage Platform Tenancy',          N'PlatformConfiguration', N'Manage platform tenancy configuration.'),
	(N'platform.configuration.research.manage',   N'Manage Platform Research',         N'PlatformConfiguration', N'Manage platform research configuration.'),
	(N'platform.configuration.legal.manage',      N'Manage Platform Legal',            N'PlatformConfiguration', N'Manage platform legal configuration.'),
	(N'platform.configuration.math.manage',       N'Manage Platform Math',             N'PlatformConfiguration', N'Manage platform math configuration.'),
	(N'platform.configuration.poloxi.manage',     N'Manage POLOXI Config',             N'PlatformConfiguration', N'Manage POLOXI configuration.'),
	(N'platform.configuration.models.manage',     N'Manage AI Providers & Models',     N'PlatformConfiguration', N'Manage AI provider and model configuration.'),
	(N'platform.configuration.execution.manage',  N'Manage Execution Config',          N'PlatformConfiguration', N'Manage platform execution configuration.'),
	(N'platform.configuration.feature_flags.manage', N'Manage Feature Flags',          N'PlatformConfiguration', N'Manage platform feature flags.')
) AS v(Code, DisplayName, Category, Description)
WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_Permission p WHERE p.Code = v.Code AND p.IsDeleted = 0);

-- OWNER/ADMIN get the tenant admin.configuration.* permissions (NOT platform.*).
INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM SaaS.SaaS_Role r
JOIN SaaS.SaaS_Permission p ON p.Category = N'Configuration'
WHERE r.TenantId IS NULL AND r.IsDeleted = 0 AND p.IsDeleted = 0
  AND r.Code IN (N'OWNER', N'ADMIN')
  AND NOT EXISTS (SELECT 1 FROM SaaS.SaaS_RolePermission rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);

-- Config_Definition registry seed (starter set; freeze §4–§10).
INSERT INTO SaaS.Config_Definition
	(ConfigKey, Category, DisplayName, Description, ValueType, DefaultValueJson, Scope,
	 TenantConfigurable, MatterConfigurable, ExecutionConfigurable, TenantCanOverride,
	 Sensitivity, RequiresRestart, RequiredPermission, ValidationJson, SortOrder)
SELECT v.ConfigKey, v.Category, v.DisplayName, v.Description, v.ValueType, v.DefaultValueJson, v.Scope,
	v.TenantConfigurable, v.MatterConfigurable, v.ExecutionConfigurable, v.TenantCanOverride,
	v.Sensitivity, v.RequiresRestart, v.RequiredPermission, v.ValidationJson, v.SortOrder
FROM (VALUES
	-- General
	(N'general.default_timezone',    N'General',  N'Default Time Zone',   N'Tenant default IANA time zone.',           N'String',  N'"America/Los_Angeles"', N'Tenant', 1, 0, 0, 1, N'Public', 0, N'admin.configuration.general.manage', CAST(NULL AS NVARCHAR(MAX)), 10),
	(N'general.default_culture',     N'General',  N'Default Locale',      N'Tenant default culture/locale.',           N'String',  N'"en-US"',               N'Tenant', 1, 0, 0, 1, N'Public', 0, N'admin.configuration.general.manage', NULL, 20),
	(N'general.results_page_size',   N'General',  N'Default Results Page Size', N'Default table/results page size.',    N'Integer', N'25',                    N'Tenant', 1, 0, 0, 1, N'Public', 0, N'admin.configuration.general.manage', N'{"min":10,"max":100}', 30),
	-- Research
	(N'research.default_mode',       N'Research', N'Default Research Mode', N'Default POLOXI research mode.',           N'Enum',    N'"Standard"',            N'Tenant', 1, 0, 1, 1, N'Public', 0, N'admin.configuration.research.manage', N'{"options":["Quick","Standard","Deep"]}', 10),
	(N'research.max_sources',        N'Research', N'Maximum Sources',      N'Maximum research sources.',                N'Integer', N'50',                    N'Tenant', 1, 0, 1, 1, N'Public', 0, N'admin.configuration.research.manage', N'{"min":1,"max":100}', 20),
	(N'research.require_verification', N'Research', N'Require Source Verification', N'Require source verification.',     N'Boolean', N'true',                  N'Tenant', 1, 0, 0, 1, N'Internal', 0, N'admin.configuration.research.manage', NULL, 30),
	-- Legal
	(N'legal.default_jurisdiction',  N'Legal',    N'Default Jurisdiction', N'Default legal jurisdiction.',             N'String',  N'"United States"',       N'Tenant', 1, 1, 0, 1, N'Public', 0, N'admin.configuration.legal.manage', NULL, 10),
	(N'legal.default_state',         N'Legal',    N'Default State',        N'Default legal state.',                     N'String',  N'"California"',          N'Tenant', 1, 1, 0, 1, N'Public', 0, N'admin.configuration.legal.manage', NULL, 20),
	(N'legal.require_human_review',  N'Legal',    N'Require Attorney Review', N'Mandatory human/attorney review.',      N'Boolean', N'true',                  N'Platform', 0, 0, 0, 0, N'Internal', 0, N'platform.configuration.legal.manage', NULL, 30),
	-- Math
	(N'math.default_solver_mode',    N'Math',     N'Default Solver Mode',  N'Default math solver mode.',                N'Enum',    N'"Deep"',                N'Tenant', 1, 0, 1, 1, N'Public', 0, N'admin.configuration.math.manage', N'{"options":["Quick","Standard","Deep"]}', 10),
	(N'math.run_formalization_auto', N'Math',     N'Run Formalization Automatically', N'Auto-run formalization.',       N'Boolean', N'true',                  N'Tenant', 1, 0, 0, 1, N'Public', 0, N'admin.configuration.math.manage', NULL, 20)
) AS v(ConfigKey, Category, DisplayName, Description, ValueType, DefaultValueJson, Scope,
	TenantConfigurable, MatterConfigurable, ExecutionConfigurable, TenantCanOverride,
	Sensitivity, RequiresRestart, RequiredPermission, ValidationJson, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM SaaS.Config_Definition d WHERE d.ConfigKey = v.ConfigKey AND d.IsDeleted = 0);

-- Platform-scope values that differ from code default (freeze §22 precedence).
-- legal.require_human_review is a mandatory platform control (TenantCanOverride=0).
INSERT INTO SaaS.Config_PlatformValue (ConfigKey, ValueJson)
SELECT v.ConfigKey, v.ValueJson
FROM (VALUES
	(N'legal.require_human_review', N'true')
) AS v(ConfigKey, ValueJson)
WHERE NOT EXISTS (SELECT 1 FROM SaaS.Config_PlatformValue pv WHERE pv.ConfigKey = v.ConfigKey AND pv.IsDeleted = 0);

COMMIT TRANSACTION;
