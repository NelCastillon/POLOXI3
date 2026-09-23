SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Seed the platform.users.manage permission.
--
-- Gates the Platform User Management control plane (/platform/users, Super Admin
-- only). Tenant User Management (/admin/users) reuses the existing Members.Manage
-- permission. This migration:
--   1. seeds the platform.users.manage permission (idempotent), and
--   2. grants it to the SUPERADMIN system role (TenantId NULL) if that role exists.
--
-- Configuration never grants access — this is an authorization change consistent
-- with the freeze. Safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────

INSERT INTO SaaS.SaaS_Permission (Code, DisplayName, Category, Description)
SELECT N'platform.users.manage', N'Manage Platform Users', N'Platform', N'Manage members across every tenant, including granting the Super Admin role.'
WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_Permission p WHERE p.Code = N'platform.users.manage' AND p.IsDeleted = 0);

DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER =
	(SELECT TOP 1 RoleId FROM SaaS.SaaS_Role WHERE Code = N'SUPERADMIN' AND TenantId IS NULL AND IsDeleted = 0);

IF @SuperAdminRoleId IS NOT NULL
BEGIN
	INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
	SELECT @SuperAdminRoleId, p.PermissionId
	FROM SaaS.SaaS_Permission p
	WHERE p.Code = N'platform.users.manage' AND p.IsDeleted = 0
	  AND NOT EXISTS (
		SELECT 1 FROM SaaS.SaaS_RolePermission rp
		WHERE rp.RoleId = @SuperAdminRoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);
END

COMMIT TRANSACTION;
