SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Grant Super Admin to yanyel02@yahoo.com.
--
-- "Super Admin" is a system role (TenantId NULL) that holds EVERY seeded
-- permission, including the platform.configuration.* set that gates the Platform
-- Configuration control plane (Super Admin only). Permissions are resolved at
-- sign-in from the user's primary tenant-membership role and forwarded as
-- `permission` claims, so this migration:
--   1. ensures the SUPERADMIN role exists,
--   2. grants it every permission (idempotently),
--   3. repoints the target user's active membership to SUPERADMIN.
--
-- Configuration never grants access — this grant is an authorization change
-- (role/permission), consistent with the freeze. Safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────

DECLARE @Email NVARCHAR(256) = N'yanyel02@yahoo.com';
DECLARE @NormalizedEmail NVARCHAR(256) = UPPER(@Email);

-- 1. Ensure the SUPERADMIN system role (platform-wide, TenantId NULL) ──────────
INSERT INTO SaaS.SaaS_Role (Code, DisplayName, Description, IsSystem, SortOrder)
SELECT N'SUPERADMIN', N'Super Admin', N'Platform super administrator with every permission, including platform configuration.', 1, 0
WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_Role r WHERE r.Code = N'SUPERADMIN' AND r.TenantId IS NULL AND r.IsDeleted = 0);

DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER =
	(SELECT TOP 1 RoleId FROM SaaS.SaaS_Role WHERE Code = N'SUPERADMIN' AND TenantId IS NULL AND IsDeleted = 0);

-- 2. Grant SUPERADMIN every seeded permission ─────────────────────────────────
INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
SELECT @SuperAdminRoleId, p.PermissionId
FROM SaaS.SaaS_Permission p
WHERE p.IsDeleted = 0
  AND NOT EXISTS (
	SELECT 1 FROM SaaS.SaaS_RolePermission rp
	WHERE rp.RoleId = @SuperAdminRoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);

-- 3. Repoint the target user's active membership to SUPERADMIN ─────────────────
DECLARE @UserId UNIQUEIDENTIFIER =
	(SELECT TOP 1 Id FROM dbo.AspNetUsers WHERE NormalizedEmail = @NormalizedEmail);

IF @UserId IS NOT NULL AND @SuperAdminRoleId IS NOT NULL
BEGIN
	-- Update the user's primary (oldest active) membership to the SUPERADMIN role.
	;WITH primary_membership AS (
		SELECT TOP 1 m.MembershipId
		FROM SaaS.SaaS_TenantMembership m
		WHERE m.UserId = @UserId AND m.IsDeleted = 0 AND m.StatusCode = N'Active'
		ORDER BY m.JoinedAtUtc ASC
	)
	UPDATE m
	SET m.RoleId = @SuperAdminRoleId,
		m.ModifiedDateUtc = SYSUTCDATETIME()
	FROM SaaS.SaaS_TenantMembership m
	JOIN primary_membership pm ON pm.MembershipId = m.MembershipId
	WHERE m.RoleId <> @SuperAdminRoleId;
END

COMMIT TRANSACTION;
