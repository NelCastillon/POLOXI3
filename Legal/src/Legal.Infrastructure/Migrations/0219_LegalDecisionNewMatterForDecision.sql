SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal - seed a NEW, undecided matter (M3) so the attorney can run the decision engine
-- against it live from the cockpit ("Decide in this matter"). Unlike 0212, this migration seeds
-- ONLY the Legal_DecisionMatter row (fully populated with structured Type / Jurisdiction / Posture
-- metadata) and NO session/candidate/branch/evidence rows - the decision is intentionally left
-- for the user to generate. Idempotent: keyed on a deterministic GUID so re-running never
-- duplicates the row. Tenant-scoped to the development demo tenant.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();
DECLARE @M3     UNIQUEIDENTIFIER = N'A3000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M3 AND TenantId = @Tenant)
	INSERT POLOXI.Legal_DecisionMatter
		(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
		 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
		 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
		 TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@M3,
		 N'Nakamura v. Vertex Robotics - Motion to Dismiss',
		 N'Civil Litigation - Employment / Trade Secret Dispute',
		 N'United States - California State - Superior Court - Santa Clara County - Governing law: California',
		 N'Motion to Dismiss - Movant: Vertex Robotics - Target: Trade Secret Misappropriation Claim - Requested disposition: Dismissal with prejudice',
		 N'Type: Civil Litigation (Employment / Trade Secret Dispute).' + NCHAR(10)
		 + N'Jurisdiction: United States, California State Court, Santa Clara County Superior Court; governing substantive law: California (CUTSA).' + NCHAR(10)
		 + N'Posture: Motion to Dismiss (demurrer). Moving party: Vertex Robotics. Responding party: Nakamura. Motion target: the trade-secret misappropriation claim. Requested disposition: dismissal with prejudice.' + NCHAR(10)
		 + N'Core question: whether the complaint identifies the alleged trade secrets with sufficient particularity under Cal. Civ. Proc. Code 2019.210 and states a viable CUTSA claim, or whether the claim is preempted and the allegations are too conclusory to survive demurrer.',
		 N'OPEN',
		 N'Employment / Trade Secret Dispute',
		 N'United States - State',
		 N'California',
		 N'Superior Court',
		 N'Santa Clara County',
		 N'California',
		 N'Vertex Robotics',
		 N'Nakamura',
		 N'Trade secret misappropriation claim',
		 N'Dismissal with prejudice',
		 @Tenant, @Now, @User);

COMMIT TRANSACTION;
