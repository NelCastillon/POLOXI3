SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — back-fill the Evidence Verification Provenance test matter for EVERY real tenant
-- (SaaS signup tenants + Core tenants).
--
-- ROOT CAUSE OF "no users see it": there are TWO tenant tables. Real signup users are provisioned into
-- SaaS.SaaS_Tenant (SaasRepository.CreateTenantAsync) and their effective TenantId comes from
-- SaaS.SaaS_TenantMembership. The cockpit lists matters with GetMattersAsync filtering on
-- Legal_DecisionMatter.TenantId = <the user's SaaS tenant>. But 0264/0265 enumerated ONLY Core.Tenant,
-- which does NOT contain SaaS signup tenants — so NO real user ever received a matter row for their
-- tenant. Only the demo tenant (present in Core) got it.
--
-- FIX: enumerate tenants from BOTH SaaS.SaaS_Tenant (the real gap) AND Core.Tenant, deduped, and seed a
-- matter copy for any that lack one. A new migration number is required because the runner records
-- applied scripts in dbo._LegalMigrations by filename and never re-runs them.
--
-- Idempotent: keyed on (Title + TenantId + IsDeleted = 0), so tenants already holding the matter are
-- skipped and re-running never duplicates. Falls back to the development demo tenant if neither tenant
-- table is present. Seeds ONLY Legal_DecisionMatter rows — decisions are generated live.
--
-- NOTE: this closes the gap for tenants that exist NOW. For EVERY future signup to always see this
-- matter, the durable fix is to seed it at tenant-provisioning time in SaasRepository.CreateTenantAsync
-- rather than via one-shot migrations; that is intentionally out of scope for this back-fill.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @DemoTenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Title      NVARCHAR(400)    = N'Delgado v. Harbor Freight Logistics - Motion for Summary Judgment';

-- Resolve the set of tenants to seed: every ACTIVE SaaS signup tenant + every Core tenant, deduped.
DECLARE @Tenants TABLE (TenantId UNIQUEIDENTIFIER PRIMARY KEY);

IF OBJECT_ID(N'SaaS.SaaS_Tenant', N'U') IS NOT NULL
	INSERT @Tenants (TenantId)
	SELECT TenantId FROM SaaS.SaaS_Tenant WHERE IsDeleted = 0;

IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
	INSERT @Tenants (TenantId)
	SELECT TenantId FROM Core.Tenant
	WHERE TenantId NOT IN (SELECT TenantId FROM @Tenants);

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
