SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — make the Evidence Verification Provenance test matter available to ALL users.
--
-- Matters are strictly tenant-scoped (Legal_DecisionMatter.TenantId; the cockpit filters on it), so a
-- matter is only visible to a user whose tenant owns a copy. 0264 seeded the matter for the demo
-- tenant only; this migration back-fills a copy for EVERY tenant in Core.Tenant so any logged-in user
-- sees it. A separate migration number is used deliberately: the runner tracks applied migrations by
-- filename in dbo._LegalMigrations and skips ones already recorded, so editing 0264 in place would
-- never re-run.
--
-- Each tenant's copy gets its own row/PK. Idempotent: keyed on (Title + TenantId), so re-running never
-- duplicates and tenants already seeded (e.g. the demo tenant from 0264) are skipped. Falls back to the
-- development demo tenant if Core.Tenant is not present.
--
-- Seeds ONLY Legal_DecisionMatter rows (no session/candidate/branch/evidence) — the decision is left
-- for users to generate live from the cockpit.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @DemoTenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Title      NVARCHAR(400)    = N'Delgado v. Harbor Freight Logistics - Motion for Summary Judgment';

-- Resolve the set of tenants to seed: every tenant in Core.Tenant, or the demo tenant as a fallback.
DECLARE @Tenants TABLE (TenantId UNIQUEIDENTIFIER PRIMARY KEY);
IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
	INSERT @Tenants (TenantId)
	SELECT TenantId FROM Core.Tenant;

IF NOT EXISTS (SELECT 1 FROM @Tenants)
	INSERT @Tenants (TenantId) VALUES (@DemoTenant);

INSERT POLOXI.Legal_DecisionMatter
	(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
	 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
	 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
	 TenantId, CreatedDateUtc, CreatedByUserId)
SELECT
	NEWID(),
	@Title,
	N'Civil Litigation - Employment / Wage & Hour Dispute',
	N'United States - Federal - District Court - Northern District of California - Governing law: FLSA / California Labor Code',
	N'Motion for Summary Judgment - Movant: Harbor Freight Logistics - Target: FLSA overtime misclassification claim - Requested disposition: Judgment for defendant',
	N'Type: Civil Litigation (Employment / Wage & Hour Dispute).' + NCHAR(10)
	+ N'Jurisdiction: United States, Federal, Northern District of California; governing law: Fair Labor Standards Act and California Labor Code.' + NCHAR(10)
	+ N'Posture: Motion for Summary Judgment. Moving party: Harbor Freight Logistics (employer). Responding party: Delgado (driver). Motion target: the FLSA overtime misclassification claim. Requested disposition: judgment for the defendant.' + NCHAR(10)
	+ N'Core question: whether the plaintiff qualifies for the FLSA motor carrier exemption, or whether the small-vehicle exception under the SAFETEA-LU Technical Corrections Act of 2008 restores overtime eligibility. The outcome depends on authority-dependent facts — the exemption test from Supreme Court and Ninth Circuit precedent and the statutory small-vehicle threshold — that must be RETRIEVED and VERIFIED against the controlling standard rather than assumed. This makes it an ideal exercise for the evidence-verification provenance pipeline.',
	N'OPEN',
	N'Employment / Wage & Hour Dispute',
	N'United States - Federal',
	N'California',
	N'District Court',
	N'Northern District of California',
	N'FLSA / California Labor Code',
	N'Harbor Freight Logistics',
	N'Delgado',
	N'FLSA overtime misclassification claim',
	N'Judgment for defendant',
	t.TenantId, SYSUTCDATETIME(), @DemoTenant
FROM @Tenants t
WHERE NOT EXISTS (
	SELECT 1 FROM POLOXI.Legal_DecisionMatter m
	WHERE m.TenantId = t.TenantId AND m.Title = @Title AND m.IsDeleted = 0);

COMMIT TRANSACTION;
