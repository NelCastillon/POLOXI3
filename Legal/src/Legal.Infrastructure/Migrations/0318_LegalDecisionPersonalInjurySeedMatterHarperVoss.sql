SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Seed a NEW fully-specified Personal Injury matter with a first-class PI Profile.
--
-- Adds "Harper v. Voss Grocery" (a slip-and-fall / premises-liability matter with all fictional names)
-- so /legal/personalinjury and the PI Decision cockpit have a second self-contained PI scenario to
-- exercise the DECISION_DISCOVERY_V2 branch-first discovery, DecisionIntent, candidate scoring, and
-- ranking panels. The matter is seeded on the active application tenant (DA988708-…-C902) so the
-- clarification preflight gate is not tripped, exactly like the Ramirez seed in 0307.
--
-- Fresh, non-colliding deterministic ids are used so the insert is idempotent and never overwrites an
-- existing row. PI Profile fields are SUPPLIED ALLEGATIONS/CONTEXT, not verified evidence. Governing
-- law, jurisdiction, forum, and incident location are kept distinct.
--
-- Idempotent: guarded by deterministic ids and NOT EXISTS checks.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'DA988708-62B2-4C14-956E-0BAA43C9E902';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- New deterministic ids for the fictional Personal Injury matter (Harper v. Voss Grocery — premises liability).
DECLARE @PIM UNIQUEIDENTIFIER = N'A3000002-0000-0000-0000-000000000001';
DECLARE @PIP UNIQUEIDENTIFIER = N'A3000002-0000-0000-0000-000000000002';

-- 1) Seed the Personal Injury matter under a fresh id.
IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @PIM)
BEGIN
	INSERT POLOXI.Legal_DecisionMatter
		(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture,
		 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
		 PracticeAreaCode, ClaimTypeCode, Description, StatusCode, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@PIM,
		 N'Harper v. Voss Grocery - Premises Liability Slip and Fall',
		 N'Personal Injury',
		 N'Circuit Court of Cook County, Illinois - Law Division',
		 N'Pre-litigation policy-limits demand',
		 N'Plaintiff Danielle Harper',
		 N'Defendant Voss Grocery Markets, LLC',
		 N'Premises liability negligence claim (failure to maintain safe premises)',
		 N'Full-value settlement of all claims',
		 N'PERSONAL_INJURY',
		 N'Premises Liability',
		 N'Plaintiff Danielle Harper slipped and fell on an unmarked pool of leaked refrigerant water in ' +
		 N'the dairy aisle of a Voss Grocery Markets store. Store surveillance footage shows the spill was ' +
		 N'present for approximately thirty-eight minutes before the fall and that two employees walked past ' +
		 N'it without placing a warning cone or cleaning it up. A subsequent incident report notes a recurring ' +
		 N'condensation leak from the dairy case that had generated prior maintenance tickets. Plaintiff ' +
		 N'sustained a displaced left distal radius fracture requiring open reduction and internal fixation, ' +
		 N'followed by occupational therapy. Plaintiff seeks a full-value settlement of all claims; the matter ' +
		 N'is currently at the policy-limits demand stage. Governing law: Illinois. Auto-generated fictional ' +
		 N'test matter for validating the PI decision cockpit, dashboard, and ranking panels.',
		 N'OPEN', @Tenant, DATEADD(DAY, -2, @Now), @User);
END

-- 2) Seed the one-to-one PI Profile extension.
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterProfile', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionPIMatterProfile WHERE DecisionPIMatterProfileId = @PIP)
BEGIN
	INSERT POLOXI.Legal_DecisionPIMatterProfile
		(DecisionPIMatterProfileId, DecisionMatterId, IncidentTypeCode, IncidentDate, IncidentTime,
		 IncidentLocation, IncidentCity, IncidentCounty, IncidentState,
		 IncidentSummary, LiabilitySummary, InjurySummary, TreatmentSummary, DamagesSummary,
		 CurrentStageCode, LitigationStatusCode, DemandStatusCode, SettlementStatusCode,
		 TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@PIP, @PIM, N'Slip and Fall', CAST(DATEADD(MONTH, -6, @Now) AS DATE), N'14:15:00',
		 N'Voss Grocery Markets, 4820 North Kedzie Avenue, dairy aisle', N'Chicago', N'Cook', N'Illinois',
		 N'Slip and fall on an unmarked refrigerant-water spill in a grocery dairy aisle; surveillance shows ' +
		 N'the hazard was present for roughly thirty-eight minutes before the fall.',
		 N'Store had actual or constructive notice: footage shows two employees passing the spill without ' +
		 N'remediation, and prior maintenance tickets document a recurring dairy-case condensation leak. ' +
		 N'Comparative fault not evidenced on the current record.',
		 N'Displaced left distal radius (wrist) fracture with reduced grip strength.',
		 N'Emergency care, closed then open reduction with internal fixation (ORIF), casting, and ongoing ' +
		 N'occupational therapy.',
		 N'Special damages for surgery and therapy plus general damages; future hardware-removal consult pending.',
		 N'DEMAND', N'PRE_LITIGATION', N'DEMAND_SENT', N'NOT_SETTLED',
		 @Tenant, DATEADD(DAY, -2, @Now), @User);
END

COMMIT TRANSACTION;
