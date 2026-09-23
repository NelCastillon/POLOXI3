SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — User Management Phase A: membership lifecycle +
-- authorization core (Table layer).
--
-- Extends SaaS_TenantMembership with the frozen lifecycle/authorization metadata:
--   • AuthorizationVersion — bumped on every access-affecting mutation so cached
--     authorization can detect staleness without evicting every key.
--   • RowVersion — optimistic-concurrency token (enforcement deferred to a later
--     phase; the column is added now so there is no future migration churn).
--   • ProvisioningSource — how the membership was created (SelfSignup, Invitation,
--     PlatformAdmin, Sso, Scim). DB stays the source of truth for these values.
--
-- StatusCode semantics are widened to the frozen membership lifecycle:
--   Active, Suspended, Disabled, Removed. (No enum-only logic in code — the DB
--   remains authoritative; the service validates against these values.)
--
-- Also seeds the frozen User Administration permission namespaces
-- (admin.users.*, admin.groups.*, admin.roles.*) and grants them to the tenant
-- OWNER/ADMIN roles and the platform SUPERADMIN role. Idempotent / safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. Lifecycle columns on SaaS_TenantMembership ───────────────────────────────
IF COL_LENGTH(N'SaaS.SaaS_TenantMembership', N'AuthorizationVersion') IS NULL
	ALTER TABLE SaaS.SaaS_TenantMembership
		ADD AuthorizationVersion INT NOT NULL CONSTRAINT DF_SaaS_TenantMembership_AuthVersion DEFAULT 1;

IF COL_LENGTH(N'SaaS.SaaS_TenantMembership', N'ProvisioningSource') IS NULL
	ALTER TABLE SaaS.SaaS_TenantMembership
		ADD ProvisioningSource NVARCHAR(40) NOT NULL CONSTRAINT DF_SaaS_TenantMembership_ProvSource DEFAULT N'Invitation';

IF COL_LENGTH(N'SaaS.SaaS_TenantMembership', N'RowVersion') IS NULL
	ALTER TABLE SaaS.SaaS_TenantMembership ADD RowVersion ROWVERSION;

-- 2. Frozen User Administration permissions ───────────────────────────────────
INSERT INTO SaaS.SaaS_Permission (Code, DisplayName, Category, Description)
SELECT v.Code, v.DisplayName, v.Category, v.Description
FROM (VALUES
	(N'admin.users.read',       N'View Users',            N'User Administration', N'View tenant members and their access.'),
	(N'admin.users.invite',     N'Invite Users',          N'User Administration', N'Invite new members to the tenant.'),
	(N'admin.users.manage',     N'Manage Users',          N'User Administration', N'Create members and change their roles.'),
	(N'admin.users.disable',    N'Disable Users',         N'User Administration', N'Suspend or disable a member''s access.'),
	(N'admin.users.remove',     N'Remove Users',          N'User Administration', N'Remove a member from the tenant (history retained).'),
	(N'admin.users.assign_roles', N'Assign Roles',        N'User Administration', N'Assign tenant roles to members.'),
	(N'admin.groups.read',      N'View Groups',           N'User Administration', N'View tenant groups.'),
	(N'admin.groups.manage',    N'Manage Groups',         N'User Administration', N'Create and manage tenant groups.'),
	(N'admin.roles.read',       N'View Roles',            N'User Administration', N'View roles and permissions.'),
	(N'admin.roles.manage',     N'Manage Roles',          N'User Administration', N'Create and manage tenant roles.')
) AS v(Code, DisplayName, Category, Description)
WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_Permission p WHERE p.Code = v.Code AND p.IsDeleted = 0);

-- 3. Grant the new permissions to tenant OWNER/ADMIN ──────────────────────────
INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM SaaS.SaaS_Role r
JOIN SaaS.SaaS_Permission p ON p.Code IN (
	N'admin.users.read', N'admin.users.invite', N'admin.users.manage', N'admin.users.disable',
	N'admin.users.remove', N'admin.users.assign_roles', N'admin.groups.read', N'admin.groups.manage',
	N'admin.roles.read', N'admin.roles.manage')
WHERE r.TenantId IS NULL AND r.IsDeleted = 0 AND p.IsDeleted = 0
  AND r.Code IN (N'OWNER', N'ADMIN')
  AND NOT EXISTS (SELECT 1 FROM SaaS.SaaS_RolePermission rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);

-- 4. Grant the new permissions to the platform SUPERADMIN role (if present) ────
DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER =
	(SELECT TOP 1 RoleId FROM SaaS.SaaS_Role WHERE Code = N'SUPERADMIN' AND TenantId IS NULL AND IsDeleted = 0);

IF @SuperAdminRoleId IS NOT NULL
BEGIN
	INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
	SELECT @SuperAdminRoleId, p.PermissionId
	FROM SaaS.SaaS_Permission p
	WHERE p.Code LIKE N'admin.%' AND p.IsDeleted = 0
	  AND NOT EXISTS (
		SELECT 1 FROM SaaS.SaaS_RolePermission rp
		WHERE rp.RoleId = @SuperAdminRoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);
END

COMMIT TRANSACTION;
