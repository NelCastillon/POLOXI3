SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Activity surface demo/seed data (Table layer).
--
-- Seeds a small, realistic set of rows into the three tables the tenant-admin
-- Activity page reads from, so a freshly provisioned workspace shows data:
--   • SaaS.Platform_AuditEvent    — recent audit events
--   • SaaS.Commerce_UsageLedger   — recent metered usage
--   • SaaS.Identity_LoginHistory  — successful and failed sign-in attempts
--
-- For every active tenant we anchor the rows to that tenant's earliest active
-- member (its de-facto owner) and that user's email. All inserts are idempotent:
-- each block is skipped when the tenant already has activity rows, so this is
-- safe to re-run and never duplicates. OccurredAtUtc values are back-dated
-- relative to SYSUTCDATETIME() so the default 30-day window surfaces them.
-- ─────────────────────────────────────────────────────────────────────────────

-- Anchor: one representative (tenant, user, email) per active tenant. ───────────
DECLARE @Anchors TABLE
(
	TenantId UNIQUEIDENTIFIER NOT NULL,
	UserId   UNIQUEIDENTIFIER NOT NULL,
	Email    NVARCHAR(320) NULL
);

INSERT INTO @Anchors (TenantId, UserId, Email)
SELECT t.TenantId, x.UserId, u.Email
FROM SaaS.SaaS_Tenant t
CROSS APPLY (
	SELECT TOP 1 m.UserId
	FROM SaaS.SaaS_TenantMembership m
	WHERE m.TenantId = t.TenantId AND m.IsDeleted = 0 AND m.StatusCode = N'Active'
	ORDER BY m.JoinedAtUtc ASC
) x
LEFT JOIN dbo.AspNetUsers u ON u.Id = x.UserId
WHERE t.IsDeleted = 0;

-- 1. Audit events ─────────────────────────────────────────────────────────────
INSERT INTO SaaS.Platform_AuditEvent
	(TenantId, UserId, EventType, ResourceType, ResourceId, CorrelationId, OccurredAtUtc, CreatedByUserId)
SELECT a.TenantId, a.UserId, e.EventType, e.ResourceType, NEWID(), CONVERT(NVARCHAR(120), NEWID()),
	   DATEADD(HOUR, -e.HoursAgo, SYSUTCDATETIME()), a.UserId
FROM @Anchors a
CROSS JOIN (VALUES
	(N'TENANT_CREATED',    N'Tenant',           72),
	(N'MEMBERSHIP_CREATED',N'TenantMembership', 70),
	(N'MEMBER_INVITED',    N'TenantMembership', 48),
	(N'CONFIGURATION_CHANGED', N'Configuration',24),
	(N'MEMBER_ROLE_CHANGED',   N'TenantMembership', 6)
) AS e(EventType, ResourceType, HoursAgo)
WHERE NOT EXISTS (
	SELECT 1 FROM SaaS.Platform_AuditEvent ae
	WHERE ae.TenantId = a.TenantId AND ae.IsDeleted = 0);

-- 2. Usage ledger ─────────────────────────────────────────────────────────────
INSERT INTO SaaS.Commerce_UsageLedger
	(TenantId, UserId, MeterCode, UsageClass, Quantity, Provider, Model, OccurredAtUtc, CreatedByUserId)
SELECT a.TenantId, a.UserId, m.MeterCode, m.UsageClass, m.Quantity, m.Provider, m.Model,
	   DATEADD(HOUR, -m.HoursAgo, SYSUTCDATETIME()), a.UserId
FROM @Anchors a
CROSS JOIN (VALUES
	(N'intelligence.execution', N'Customer', CAST(1  AS DECIMAL(18,4)), N'AzureOpenAI', N'gpt-4.1',      50),
	(N'intelligence.execution', N'Customer', CAST(1  AS DECIMAL(18,4)), N'AzureOpenAI', N'gpt-4.1',      26),
	(N'intelligence.execution', N'Customer', CAST(1  AS DECIMAL(18,4)), N'AzureOpenAI', N'gpt-4.1-mini', 5),
	(N'tokens.total',           N'Customer', CAST(1840 AS DECIMAL(18,4)), N'AzureOpenAI', N'gpt-4.1',     50),
	(N'tokens.total',           N'Customer', CAST(920  AS DECIMAL(18,4)), N'AzureOpenAI', N'gpt-4.1-mini',5)
) AS m(MeterCode, UsageClass, Quantity, Provider, Model, HoursAgo)
WHERE NOT EXISTS (
	SELECT 1 FROM SaaS.Commerce_UsageLedger ul
	WHERE ul.TenantId = a.TenantId AND ul.IsDeleted = 0);

-- 3. Login history (successes + failures) ─────────────────────────────────────
INSERT INTO SaaS.Identity_LoginHistory
	(UserId, TenantId, Email, OutcomeCode, IsSuccess, IpAddress, UserAgent, OccurredAtUtc, CreatedByUserId)
SELECT
	CASE WHEN l.OutcomeCode = N'Success' THEN a.UserId ELSE a.UserId END,
	a.TenantId,
	a.Email,
	l.OutcomeCode,
	l.IsSuccess,
	l.IpAddress,
	N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36',
	DATEADD(HOUR, -l.HoursAgo, SYSUTCDATETIME()),
	a.UserId
FROM @Anchors a
CROSS JOIN (VALUES
	(N'Success',            CAST(1 AS BIT), N'203.0.113.24', 96),
	(N'InvalidCredentials', CAST(0 AS BIT), N'198.51.100.7', 52),
	(N'InvalidCredentials', CAST(0 AS BIT), N'198.51.100.7', 51),
	(N'Success',            CAST(1 AS BIT), N'203.0.113.24', 24),
	(N'Success',            CAST(1 AS BIT), N'203.0.113.24', 2)
) AS l(OutcomeCode, IsSuccess, IpAddress, HoursAgo)
WHERE a.Email IS NOT NULL
  AND NOT EXISTS (
	SELECT 1 FROM SaaS.Identity_LoginHistory lh
	WHERE lh.TenantId = a.TenantId AND lh.IsDeleted = 0);

COMMIT TRANSACTION;
