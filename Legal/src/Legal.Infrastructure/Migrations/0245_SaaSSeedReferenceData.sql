SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Reference/seed data (DB-backed, not hardcoded).
--
-- Seeds the system roles, permissions, role-permission grants, the EARLY_ACCESS
-- ProductPlan, the entitlement catalog (capability features + monthly/total
-- limits), the plan-entitlement grants/limits, and the capability registry.
-- All inserts are idempotent (WHERE NOT EXISTS on natural keys). These are the
-- launch settings; limits stay configurable in the DB. No operational data is
-- hardcoded in application code.
-- ─────────────────────────────────────────────────────────────────────────────

-- Roles ────────────────────────────────────────────────────────────────────────
INSERT INTO SaaS.SaaS_Role (Code, DisplayName, Description, IsSystem, SortOrder)
SELECT v.Code, v.DisplayName, v.Description, 1, v.SortOrder
FROM (VALUES
	(N'OWNER',  N'Owner',  N'Full control of the tenant workspace.', 1),
	(N'ADMIN',  N'Admin',  N'Administer members, settings and configuration.', 2),
	(N'MEMBER', N'Member', N'Execute intelligence capabilities and manage own matters.', 3),
	(N'VIEWER', N'Viewer', N'Read-only access to shared results.', 4)
) AS v(Code, DisplayName, Description, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_Role r WHERE r.Code = v.Code AND r.TenantId IS NULL AND r.IsDeleted = 0);

-- Permissions ──────────────────────────────────────────────────────────────────
INSERT INTO SaaS.SaaS_Permission (Code, DisplayName, Category, Description)
SELECT v.Code, v.DisplayName, v.Category, v.Description
FROM (VALUES
	(N'Search.Execute',       N'Run Search',                N'Research', N'Execute general search.'),
	(N'Research.Execute',     N'Run POLOXI Research',       N'Research', N'Execute POLOXI research.'),
	(N'LegalSearch.Execute',  N'Run Legal Search',          N'Legal',    N'Execute legal search.'),
	(N'LegalResearch.Execute',N'Run Legal Research',        N'Legal',    N'Execute legal research.'),
	(N'Matter.Manage',        N'Manage Matters',            N'Legal',    N'Create and manage legal matters.'),
	(N'Decision.Execute',     N'Run Decision Intelligence', N'Legal',    N'Execute legal decision intelligence.'),
	(N'MathFormalization.Execute', N'Run Formalization',    N'Math',     N'Execute math formalization.'),
	(N'MathSolver.Execute',   N'Run Math Solver',           N'Math',     N'Execute math solver.'),
	(N'Platform.Configure',   N'Configure Platform',        N'Platform', N'Manage platform configuration.'),
	(N'Members.Manage',       N'Manage Members',            N'Platform', N'Invite and manage tenant members.')
) AS v(Code, DisplayName, Category, Description)
WHERE NOT EXISTS (SELECT 1 FROM SaaS.SaaS_Permission p WHERE p.Code = v.Code AND p.IsDeleted = 0);

-- Role → Permission grants ─────────────────────────────────────────────────────
-- OWNER and ADMIN get every permission; MEMBER gets execution + matter management; VIEWER gets none by default.
INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM SaaS.SaaS_Role r
CROSS JOIN SaaS.SaaS_Permission p
WHERE r.TenantId IS NULL AND r.IsDeleted = 0 AND p.IsDeleted = 0
  AND r.Code IN (N'OWNER', N'ADMIN')
  AND NOT EXISTS (SELECT 1 FROM SaaS.SaaS_RolePermission rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);

INSERT INTO SaaS.SaaS_RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM SaaS.SaaS_Role r
JOIN SaaS.SaaS_Permission p ON p.Code IN (
	N'Search.Execute', N'Research.Execute', N'LegalSearch.Execute', N'LegalResearch.Execute',
	N'Matter.Manage', N'Decision.Execute', N'MathFormalization.Execute', N'MathSolver.Execute')
WHERE r.TenantId IS NULL AND r.IsDeleted = 0 AND p.IsDeleted = 0
  AND r.Code = N'MEMBER'
  AND NOT EXISTS (SELECT 1 FROM SaaS.SaaS_RolePermission rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0);

-- EARLY_ACCESS ProductPlan ─────────────────────────────────────────────────────
INSERT INTO SaaS.Commerce_ProductPlan (Code, Name, Description, PriceAmount, CurrencyCode, BillingPeriodCode, IsPublic, IsActive, SortOrder)
SELECT N'EARLY_ACCESS', N'Judz Early Access', N'Free early-access plan for Judz.ai.', 0, N'USD', N'Monthly', 1, 1, 1
WHERE NOT EXISTS (SELECT 1 FROM SaaS.Commerce_ProductPlan WHERE Code = N'EARLY_ACCESS' AND IsDeleted = 0);

-- Entitlement catalog ──────────────────────────────────────────────────────────
INSERT INTO SaaS.Commerce_Entitlement (Code, DisplayName, EntitlementKind, Description)
SELECT v.Code, v.DisplayName, v.Kind, v.Description
FROM (VALUES
	(N'research.search',        N'General Search',          N'Feature', N'Access to general search.'),
	(N'research.poloxi',        N'POLOXI Research',         N'Feature', N'Access to POLOXI research.'),
	(N'legal.search',           N'Legal Search',            N'Feature', N'Access to legal search.'),
	(N'legal.research',         N'Legal Research',          N'Feature', N'Access to legal research.'),
	(N'legal.matters',          N'Matters',                 N'Feature', N'Manage legal matters.'),
	(N'legal.decision',         N'Decision Intelligence',   N'Feature', N'Access to legal decision intelligence.'),
	(N'math.formalization',     N'Math Formalization',      N'Feature', N'Access to math formalization.'),
	(N'math.solver',            N'Math Solver',             N'Feature', N'Access to math solver.'),
	(N'platform.configuration', N'Platform Configuration',  N'Feature', N'Manage platform configuration.'),
	(N'max.users',              N'Max Users',               N'Limit',   N'Maximum number of tenant users.'),
	(N'max.matters',            N'Max Matters',             N'Limit',   N'Maximum number of matters.'),
	(N'monthly.search',         N'Monthly Search',          N'Limit',   N'Monthly general search runs.'),
	(N'monthly.research',       N'Monthly Research',        N'Limit',   N'Monthly research runs.'),
	(N'monthly.legal_search',   N'Monthly Legal Search',    N'Limit',   N'Monthly legal search runs.'),
	(N'monthly.legal_decision', N'Monthly Legal Decision',  N'Limit',   N'Monthly legal decision runs.'),
	(N'monthly.math',           N'Monthly Math',            N'Limit',   N'Monthly math runs.')
) AS v(Code, DisplayName, Kind, Description)
WHERE NOT EXISTS (SELECT 1 FROM SaaS.Commerce_Entitlement e WHERE e.Code = v.Code AND e.IsDeleted = 0);

-- Plan → Entitlement grants + limits (EARLY_ACCESS) ────────────────────────────
DECLARE @PlanId UNIQUEIDENTIFIER = (SELECT TOP 1 PlanId FROM SaaS.Commerce_ProductPlan WHERE Code = N'EARLY_ACCESS' AND IsDeleted = 0);

-- Feature grants (enabled, no numeric limit)
INSERT INTO SaaS.Commerce_PlanEntitlement (PlanId, EntitlementId, IsEnabled, LimitValue, LimitPeriodCode)
SELECT @PlanId, e.EntitlementId, 1, NULL, NULL
FROM SaaS.Commerce_Entitlement e
WHERE e.EntitlementKind = N'Feature' AND e.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM SaaS.Commerce_PlanEntitlement pe WHERE pe.PlanId = @PlanId AND pe.EntitlementId = e.EntitlementId AND pe.IsDeleted = 0);

-- Limit grants (launch quotas)
INSERT INTO SaaS.Commerce_PlanEntitlement (PlanId, EntitlementId, IsEnabled, LimitValue, LimitPeriodCode)
SELECT @PlanId, e.EntitlementId, 1, v.LimitValue, v.LimitPeriodCode
FROM (VALUES
	(N'max.users',              CAST(1   AS BIGINT), N'Total'),
	(N'max.matters',            CAST(10  AS BIGINT), N'Total'),
	(N'monthly.search',         CAST(100 AS BIGINT), N'Monthly'),
	(N'monthly.research',       CAST(20  AS BIGINT), N'Monthly'),
	(N'monthly.legal_search',   CAST(50  AS BIGINT), N'Monthly'),
	(N'monthly.legal_decision', CAST(20  AS BIGINT), N'Monthly'),
	(N'monthly.math',           CAST(20  AS BIGINT), N'Monthly')
) AS v(Code, LimitValue, LimitPeriodCode)
JOIN SaaS.Commerce_Entitlement e ON e.Code = v.Code AND e.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM SaaS.Commerce_PlanEntitlement pe WHERE pe.PlanId = @PlanId AND pe.EntitlementId = e.EntitlementId AND pe.IsDeleted = 0);

-- Capability registry ──────────────────────────────────────────────────────────
INSERT INTO SaaS.Platform_Capability (Code, DisplayName, EntitlementCode, Permission, CustomerMeterCode, RequiresMatter, IsMetered, IsAsync, SortOrder)
SELECT v.Code, v.DisplayName, v.EntitlementCode, v.Permission, v.MeterCode, v.RequiresMatter, v.IsMetered, v.IsAsync, v.SortOrder
FROM (VALUES
	(N'research.search',    N'General Search',        N'research.search',    N'Search.Execute',        N'research.search.run',    CAST(0 AS BIT), CAST(1 AS BIT), CAST(0 AS BIT), 1),
	(N'research.poloxi',    N'POLOXI Research',       N'research.poloxi',    N'Research.Execute',      N'research.poloxi.run',    CAST(0 AS BIT), CAST(1 AS BIT), CAST(1 AS BIT), 2),
	(N'legal.search',       N'Legal Search',          N'legal.search',      N'LegalSearch.Execute',   N'legal.search.run',       CAST(0 AS BIT), CAST(1 AS BIT), CAST(0 AS BIT), 3),
	(N'legal.research',     N'Legal Research',        N'legal.research',    N'LegalResearch.Execute', N'legal.research.run',     CAST(0 AS BIT), CAST(1 AS BIT), CAST(1 AS BIT), 4),
	(N'legal.matters',      N'Matters',               N'legal.matters',     N'Matter.Manage',         NULL,                      CAST(0 AS BIT), CAST(0 AS BIT), CAST(0 AS BIT), 5),
	(N'legal.decision',     N'Decision Intelligence', N'legal.decision',    N'Decision.Execute',      N'legal.decision.run',     CAST(1 AS BIT), CAST(1 AS BIT), CAST(1 AS BIT), 6),
	(N'math.formalization', N'Math Formalization',    N'math.formalization',N'MathFormalization.Execute', N'math.formalization.run', CAST(0 AS BIT), CAST(1 AS BIT), CAST(0 AS BIT), 7),
	(N'math.solver',        N'Math Solver',           N'math.solver',       N'MathSolver.Execute',    N'math.solver.run',        CAST(0 AS BIT), CAST(1 AS BIT), CAST(1 AS BIT), 8)
) AS v(Code, DisplayName, EntitlementCode, Permission, MeterCode, RequiresMatter, IsMetered, IsAsync, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM SaaS.Platform_Capability c WHERE c.Code = v.Code AND c.IsDeleted = 0);

COMMIT TRANSACTION;
