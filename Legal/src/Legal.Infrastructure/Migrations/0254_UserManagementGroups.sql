SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — User Management Phase C: tenant groups, group→role
-- mappings, group membership, and pending group assignments on invitations.
--
-- A group is a tenant-scoped collection that carries one or more platform roles.
-- Adding a user to a group grants that group's roles (resolved additively at
-- authorization time; the single-role TenantMembership invariant is preserved).
--
--   • SaaS_TenantGroup           — the tenant-scoped group definition.
--   • SaaS_TenantGroupRole       — roles carried by a group (group → role map).
--   • SaaS_TenantGroupMember     — user ↔ group membership.
--   • SaaS_TenantInvitationGroup — pending group assignments applied on accept
--                                  (mirrors SaaS_TenantInvitationRole).
--
-- StatusCode semantics (DB-authoritative, validated in the service):
--   Group : Active, Inactive
-- All tables carry the standard base/audit fields. Idempotent / safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. SaaS_TenantGroup ─────────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_TenantGroup', N'U') IS NULL
CREATE TABLE SaaS.SaaS_TenantGroup
(
	GroupId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_TenantGroup PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	Name              NVARCHAR(160) NOT NULL,
	Description       NVARCHAR(400) NULL,
	StatusCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_SaaS_TenantGroup_Status DEFAULT N'Active',
	SortOrder         INT NOT NULL CONSTRAINT DF_SaaS_TenantGroup_SortOrder DEFAULT 0,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantGroup_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_TenantGroup_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_TenantGroup_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId)
);

-- Group names are unique per tenant (case-insensitive by collation, active rows only).
IF OBJECT_ID(N'UX_SaaS_TenantGroup_Name', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_TenantGroup_Name
		ON SaaS.SaaS_TenantGroup (TenantId, Name)
		WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_SaaS_TenantGroup_Tenant', N'IX') IS NULL
	CREATE INDEX IX_SaaS_TenantGroup_Tenant
		ON SaaS.SaaS_TenantGroup (TenantId) WHERE IsDeleted = 0;

-- 2. SaaS_TenantGroupRole (group → role mapping) ──────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_TenantGroupRole', N'U') IS NULL
CREATE TABLE SaaS.SaaS_TenantGroupRole
(
	GroupRoleId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_TenantGroupRole PRIMARY KEY DEFAULT NEWID(),
	GroupId           UNIQUEIDENTIFIER NOT NULL,
	RoleId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantGroupRole_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_TenantGroupRole_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_TenantGroupRole_Group FOREIGN KEY (GroupId) REFERENCES SaaS.SaaS_TenantGroup (GroupId),
	CONSTRAINT FK_SaaS_TenantGroupRole_Role FOREIGN KEY (RoleId) REFERENCES SaaS.SaaS_Role (RoleId)
);

IF OBJECT_ID(N'UX_SaaS_TenantGroupRole', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_TenantGroupRole
		ON SaaS.SaaS_TenantGroupRole (GroupId, RoleId) WHERE IsDeleted = 0;

-- 3. SaaS_TenantGroupMember (user ↔ group membership) ─────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_TenantGroupMember', N'U') IS NULL
CREATE TABLE SaaS.SaaS_TenantGroupMember
(
	GroupMemberId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_TenantGroupMember PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	GroupId           UNIQUEIDENTIFIER NOT NULL,
	UserId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantGroupMember_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_TenantGroupMember_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_TenantGroupMember_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId),
	CONSTRAINT FK_SaaS_TenantGroupMember_Group FOREIGN KEY (GroupId) REFERENCES SaaS.SaaS_TenantGroup (GroupId)
);

IF OBJECT_ID(N'UX_SaaS_TenantGroupMember', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_TenantGroupMember
		ON SaaS.SaaS_TenantGroupMember (GroupId, UserId) WHERE IsDeleted = 0;
IF OBJECT_ID(N'IX_SaaS_TenantGroupMember_User', N'IX') IS NULL
	CREATE INDEX IX_SaaS_TenantGroupMember_User
		ON SaaS.SaaS_TenantGroupMember (TenantId, UserId) WHERE IsDeleted = 0;

-- 4. SaaS_TenantInvitationGroup (pending group assignments) ────────────────────
IF OBJECT_ID(N'SaaS.SaaS_TenantInvitationGroup', N'U') IS NULL
CREATE TABLE SaaS.SaaS_TenantInvitationGroup
(
	InvitationGroupId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_TenantInvitationGroup PRIMARY KEY DEFAULT NEWID(),
	InvitationId      UNIQUEIDENTIFIER NOT NULL,
	GroupId           UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantInvitationGroup_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_TenantInvitationGroup_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_TenantInvitationGroup_Invitation FOREIGN KEY (InvitationId) REFERENCES SaaS.SaaS_TenantInvitation (InvitationId),
	CONSTRAINT FK_SaaS_TenantInvitationGroup_Group FOREIGN KEY (GroupId) REFERENCES SaaS.SaaS_TenantGroup (GroupId)
);

IF OBJECT_ID(N'UX_SaaS_TenantInvitationGroup', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_TenantInvitationGroup
		ON SaaS.SaaS_TenantInvitationGroup (InvitationId, GroupId) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
