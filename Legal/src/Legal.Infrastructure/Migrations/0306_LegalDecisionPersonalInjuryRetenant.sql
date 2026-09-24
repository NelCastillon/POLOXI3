SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Retenant the 0304 Personal Injury sample matter to the active application tenant.
--
-- 0304 seeded the matter under the development demo tenant (00000000-…-0001), which is only used by
-- the DevelopmentAuthenticationHandler. Real sign-ins resolve their own tenant, and the
-- /legal/personalinjury endpoint scopes matters to the authenticated tenant — so the demo-tenant row
-- was never visible to a real user. Tenant DA988708-62B2-4C14-956E-0BAA43C9E902 is the active
-- application tenant that already owns Personal Injury matters, so we move the seed row (and its
-- one-to-one PI profile) onto that tenant.
--
-- Idempotent: only moves the deterministic seed row when it still points at the demo tenant.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @DemoTenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @AppTenant  UNIQUEIDENTIFIER = N'DA988708-62B2-4C14-956E-0BAA43C9E902';
DECLARE @User       UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @PIM        UNIQUEIDENTIFIER = N'A3000000-0000-0000-0000-000000000001';
DECLARE @PIP        UNIQUEIDENTIFIER = N'A3000000-0000-0000-0000-000000000002';

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
	UPDATE POLOXI.Legal_DecisionMatter
	SET TenantId = @AppTenant, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @User
	WHERE DecisionMatterId = @PIM AND TenantId = @DemoTenant;

IF OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterProfile', N'U') IS NOT NULL
	UPDATE POLOXI.Legal_DecisionPIMatterProfile
	SET TenantId = @AppTenant, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @User
	WHERE DecisionPIMatterProfileId = @PIP AND TenantId = @DemoTenant;

COMMIT TRANSACTION;
