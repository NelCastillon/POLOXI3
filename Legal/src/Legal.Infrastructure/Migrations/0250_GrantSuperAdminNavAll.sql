SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Grant NAV_ALL master override to SUPERADMIN.
--
-- Follow-up to 0249. Migrations are immutable once recorded in
-- dbo._LegalMigrations, so 0249 will not re-run. The /legal/configuration and
-- Intelligent Search pages call api/intelligence* / api/intelligence_wide*,
-- gated by the Intelligence.* authorization policies. Those handlers treat a
-- `permission=NAV_ALL` claim as a global grant, but the Intelligence.* codes
-- live in the IAM.Permission catalog and are never emitted as `permission`
-- claims (login forwards SaaS_Permission Codes only).
--
-- This migration seeds a NAV_ALL SaaS permission and grants it to SUPERADMIN so
-- the master-override claim is forwarded at sign-in, unlocking every capability
-- without weakening the per-capability gates for other roles. Idempotent and
-- safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────

DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER =
	(SELECT TOP 1 RoleId FROM SaaS.SaaS_Role WHERE Code = N'SUPERADMIN' AND TenantId IS NULL AND IsDeleted = 0);

-- 1. Seed the NAV_ALL master-override permission ──────────────────────────────
INSERT INTO SaaS.SaaS_Permission (Code, DisplayName, Category, Description)
SELECT N'NAV_ALL', N'Platform Master Access', N'Platform', N'Super Admin master override that grants every capability, including intelligence configuration and search.'
WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_Permission p WHERE p.Code = N'NAV_ALL' AND p.IsDeleted = 0);

-- 2. Grant NAV_ALL (and any other newly seeded permission) to SUPERADMIN ───────
IF @SuperAdminRoleId IS NOT NULL
BEGIN
	INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
	SELECT @SuperAdminRoleId, p.PermissionId
	FROM SaaS.SaaS_Permission p
	WHERE p.IsDeleted = 0
	  AND NOT EXISTS (
		SELECT 1 FROM SaaS.SaaS_RolePermission rp
		WHERE rp.RoleId = @SuperAdminRoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);
END

COMMIT TRANSACTION;
