SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — User Management Phase C/D: default tenant groups.
--
-- Seeds a small, opinionated set of tenant-scoped groups for every existing
-- active tenant, plus their group → role mappings. Groups are additive role
-- carriers (see 0254_UserManagementGroups.sql / GetEffectivePermissionsAsync):
--
--   • Firm Administrators → OWNER, ADMIN   (full workspace + administration)
--   • Attorneys           → MEMBER         (execute capabilities, manage matters)
--   • Paralegals          → MEMBER         (execute capabilities, manage matters)
--   • Read-Only           → VIEWER         (read-only access)
--
-- All inserts are idempotent (WHERE NOT EXISTS on natural keys) and safe to
-- re-run. No default memberships are created; admins assign users via the UI.
-- Role codes resolve to the platform-catalog roles (TenantId IS NULL) so the
-- database stays the source of truth.
-- ─────────────────────────────────────────────────────────────────────────────

-- Catalog of the default groups and the platform role each one carries.
DECLARE @Defaults TABLE (Name NVARCHAR(160), Description NVARCHAR(400), SortOrder INT, RoleCode NVARCHAR(40));
INSERT INTO @Defaults (Name, Description, SortOrder, RoleCode) VALUES
	(N'Firm Administrators', N'Full workspace control and administration.', 1, N'OWNER'),
	(N'Firm Administrators', N'Full workspace control and administration.', 1, N'ADMIN'),
	(N'Attorneys',           N'Execute capabilities and manage matters.',   2, N'MEMBER'),
	(N'Paralegals',          N'Execute capabilities and manage matters.',   3, N'MEMBER'),
	(N'Read-Only',           N'Read-only access to shared results.',        4, N'VIEWER');

-- 1. Create the default groups for every active tenant (one row per distinct name).
INSERT INTO SaaS.SaaS_TenantGroup (TenantId, Name, Description, StatusCode, SortOrder)
SELECT t.TenantId, d.Name, MIN(d.Description), N'Active', MIN(d.SortOrder)
FROM SaaS.SaaS_Tenant t
CROSS JOIN (SELECT DISTINCT Name, Description, SortOrder FROM @Defaults) d
WHERE t.IsDeleted = 0
  AND NOT EXISTS (
		SELECT 1 FROM SaaS.SaaS_TenantGroup g
		WHERE g.TenantId = t.TenantId AND g.Name = d.Name AND g.IsDeleted = 0)
GROUP BY t.TenantId, d.Name;

-- 2. Map each seeded group to the platform role(s) it carries.
INSERT INTO SaaS.SaaS_TenantGroupRole (GroupId, RoleId)
SELECT g.GroupId, r.RoleId
FROM SaaS.SaaS_TenantGroup g
JOIN @Defaults d ON d.Name = g.Name
JOIN SaaS.SaaS_Role r ON r.Code = d.RoleCode AND r.TenantId IS NULL AND r.IsDeleted = 0
WHERE g.IsDeleted = 0
  AND NOT EXISTS (
		SELECT 1 FROM SaaS.SaaS_TenantGroupRole gr
		WHERE gr.GroupId = g.GroupId AND gr.RoleId = r.RoleId AND gr.IsDeleted = 0);

COMMIT TRANSACTION;
