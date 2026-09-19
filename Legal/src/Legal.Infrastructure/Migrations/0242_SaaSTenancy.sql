SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Tenancy tables (Table layer, phase 2).
--
-- A user belongs to zero-or-more tenants through TenantMembership (no TenantId on
-- the user). Authorization ultimately resolves to permissions; roles are a
-- grouping convenience. Legal work adds a second boundary via MatterAccess.
-- TenantPlacement records the physical/logical placement of a tenant's data.
-- All tables carry the standard base/audit fields. Reference values are seeded
-- separately (0245) so the database stays the source of truth.
-- ─────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'SaaS') IS NULL
	EXEC(N'CREATE SCHEMA SaaS AUTHORIZATION dbo;');

-- Tenant ───────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_Tenant',N'U') IS NULL
CREATE TABLE SaaS.SaaS_Tenant
(
	TenantId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_Tenant PRIMARY KEY DEFAULT NEWID(),
	Name              NVARCHAR(200) NOT NULL,
	Slug              NVARCHAR(100) NOT NULL,
	StatusCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_SaaS_Tenant_Status DEFAULT N'Active',
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_Tenant_Created DEFAULT SYSUTCDATETIME(),
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_Tenant_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_SaaS_Tenant_Slug',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_Tenant_Slug ON SaaS.SaaS_Tenant (Slug) WHERE IsDeleted = 0;

-- Role ─────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_Role',N'U') IS NULL
CREATE TABLE SaaS.SaaS_Role
(
	RoleId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_Role PRIMARY KEY DEFAULT NEWID(),
	Code              NVARCHAR(60) NOT NULL,
	DisplayName       NVARCHAR(120) NOT NULL,
	Description       NVARCHAR(400) NULL,
	IsSystem          BIT NOT NULL CONSTRAINT DF_SaaS_Role_IsSystem DEFAULT 1,
	SortOrder         INT NOT NULL CONSTRAINT DF_SaaS_Role_SortOrder DEFAULT 0,
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_Role_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_Role_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_SaaS_Role_Code',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_Role_Code ON SaaS.SaaS_Role (Code, TenantId) WHERE IsDeleted = 0;

-- Permission ───────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_Permission',N'U') IS NULL
CREATE TABLE SaaS.SaaS_Permission
(
	PermissionId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_Permission PRIMARY KEY DEFAULT NEWID(),
	Code              NVARCHAR(80) NOT NULL,
	DisplayName       NVARCHAR(160) NOT NULL,
	Category          NVARCHAR(60) NULL,
	Description       NVARCHAR(400) NULL,
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_Permission_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_Permission_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_SaaS_Permission_Code',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_Permission_Code ON SaaS.SaaS_Permission (Code) WHERE IsDeleted = 0;

-- RolePermission ───────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_RolePermission',N'U') IS NULL
CREATE TABLE SaaS.SaaS_RolePermission
(
	RolePermissionId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_RolePermission PRIMARY KEY DEFAULT NEWID(),
	RoleId            UNIQUEIDENTIFIER NOT NULL,
	PermissionId      UNIQUEIDENTIFIER NOT NULL,
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_RolePermission_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_RolePermission_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_RolePermission_Role FOREIGN KEY (RoleId) REFERENCES SaaS.SaaS_Role (RoleId),
	CONSTRAINT FK_SaaS_RolePermission_Permission FOREIGN KEY (PermissionId) REFERENCES SaaS.SaaS_Permission (PermissionId)
);

IF OBJECT_ID(N'UX_SaaS_RolePermission',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_RolePermission ON SaaS.SaaS_RolePermission (RoleId, PermissionId) WHERE IsDeleted = 0;

-- TenantMembership ─────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_TenantMembership',N'U') IS NULL
CREATE TABLE SaaS.SaaS_TenantMembership
(
	MembershipId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_TenantMembership PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	UserId            UNIQUEIDENTIFIER NOT NULL,
	RoleId            UNIQUEIDENTIFIER NOT NULL,
	StatusCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_SaaS_TenantMembership_Status DEFAULT N'Active',
	JoinedAtUtc       DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantMembership_Joined DEFAULT SYSUTCDATETIME(),
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantMembership_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_TenantMembership_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_TenantMembership_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId),
	CONSTRAINT FK_SaaS_TenantMembership_Role FOREIGN KEY (RoleId) REFERENCES SaaS.SaaS_Role (RoleId)
);

IF OBJECT_ID(N'UX_SaaS_TenantMembership_TenantUser',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_TenantMembership_TenantUser ON SaaS.SaaS_TenantMembership (TenantId, UserId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_SaaS_TenantMembership_UserId',N'IX') IS NULL
	CREATE INDEX IX_SaaS_TenantMembership_UserId ON SaaS.SaaS_TenantMembership (UserId) WHERE IsDeleted = 0;

-- MatterAccess (second boundary for Legal) ─────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_MatterAccess',N'U') IS NULL
CREATE TABLE SaaS.SaaS_MatterAccess
(
	MatterAccessId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_MatterAccess PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	MatterId          UNIQUEIDENTIFIER NOT NULL,
	UserId            UNIQUEIDENTIFIER NOT NULL,
	AccessLevelCode   NVARCHAR(40) NOT NULL CONSTRAINT DF_SaaS_MatterAccess_Level DEFAULT N'Owner',
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_MatterAccess_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_MatterAccess_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_MatterAccess_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'UX_SaaS_MatterAccess',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_MatterAccess ON SaaS.SaaS_MatterAccess (TenantId, MatterId, UserId) WHERE IsDeleted = 0;

-- TenantPlacement ──────────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_TenantPlacement',N'U') IS NULL
CREATE TABLE SaaS.SaaS_TenantPlacement
(
	PlacementId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_TenantPlacement PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	RegionCode        NVARCHAR(60) NOT NULL CONSTRAINT DF_SaaS_TenantPlacement_Region DEFAULT N'default',
	ShardKey          NVARCHAR(120) NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantPlacement_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_TenantPlacement_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_TenantPlacement_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

IF OBJECT_ID(N'UX_SaaS_TenantPlacement_TenantId',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_TenantPlacement_TenantId ON SaaS.SaaS_TenantPlacement (TenantId) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
