SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Grant Intelligence.Search to standard roles.
--
-- The /legal/search page calls api/intelligence_wide/*, gated by the
-- Intelligence.Search authorization policy. Its handler succeeds only when the
-- signed-in principal carries a `permission=Intelligence.Search` claim (or the
-- NAV_ALL master override, or the SYSTEM_ADMIN/TENANT_ADMIN role). Login forwards
-- SaaS.SaaS_Permission Codes as `permission` claims, but the SaaS role seeds
-- (0245) only granted execution codes such as Search.Execute / Research.Execute —
-- never Intelligence.Search. As a result, ordinary OWNER/ADMIN/MEMBER accounts
-- authenticate successfully yet receive 403 on the search endpoints, which the
-- web AuthRedirectHandler bounces back to /login.
--
-- This migration seeds the Intelligence.Search SaaS permission and grants it to
-- the platform OWNER, ADMIN, and MEMBER roles so every real member can run
-- intelligent search. Idempotent and safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. Seed the Intelligence.Search permission ─────────────────────────────────
INSERT INTO SaaS.SaaS_Permission (Code, DisplayName, Category, Description)
SELECT N'Intelligence.Search', N'Use Intelligent Search', N'Research', N'Run permission-aware intelligent search (POLOXI Wide and related intelligence endpoints).'
WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_Permission p WHERE p.Code = N'Intelligence.Search' AND p.IsDeleted = 0);

-- 2. Grant Intelligence.Search to OWNER, ADMIN, and MEMBER platform roles ─────
INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM SaaS.SaaS_Role r
JOIN SaaS.SaaS_Permission p ON p.Code = N'Intelligence.Search' AND p.IsDeleted = 0
WHERE r.TenantId IS NULL AND r.IsDeleted = 0
  AND r.Code IN (N'OWNER', N'ADMIN', N'MEMBER')
  AND NOT EXISTS (
	  SELECT 1 FROM SaaS.SaaS_RolePermission rp
	  WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);

COMMIT TRANSACTION;
