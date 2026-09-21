SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — seed a NEW, undecided matter (M4) for exercising the Evidence Verification
-- Provenance pipeline (§14) and the Proposal Integrity Gate (shadow mode) end-to-end.
--
-- The attorney runs the decision engine live from the cockpit ("Decide in this matter"). The facts
-- are deliberately authority-dependent: the outcome turns on whether specific external sources can be
-- RETRIEVED and VERIFIED against a frontier objective, so VERIFIED evidence rows will carry non-null
-- SupportedObjective / SupportingPassage — the exact provenance surface added by 0263. It also poses a
-- genuinely two-sided question so the discovery LLM must propose ≥2 distinct candidate outcomes,
-- giving the Proposal Integrity Gate a real proposal to diagnose.
--
-- Seeds ONLY the Legal_DecisionMatter row (fully populated structured metadata) and NO
-- session/candidate/branch/evidence rows — the decision is intentionally left for the user to generate.
-- Idempotent: keyed on a deterministic GUID so re-running never duplicates the row. Tenant-scoped to
-- the development demo tenant. NOTE: 0265 back-fills this matter for ALL tenants so every user sees it.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();
DECLARE @M4     UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M4 AND TenantId = @Tenant)
	INSERT POLOXI.Legal_DecisionMatter
		(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
		 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
		 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
		 TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@M4,
		 N'Delgado v. Harbor Freight Logistics - Motion for Summary Judgment',
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
		 @Tenant, @Now, @User);

COMMIT TRANSACTION;
