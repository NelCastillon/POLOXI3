SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Personal Injury sample matter that does NOT trip the clarification preflight gate.
--
-- The clarification preflight gate (see 0303 + LegalDecisionService.TryBuildPreflightClarification)
-- only fires when an ESSENTIAL decision-contract input is missing — specifically when BOTH Posture
-- and MotionTarget are empty (and, with defaults, the query is also fact-thin). This migration seeds
-- a fully-specified Personal Injury matter so that path can be exercised end-to-end WITHOUT ever
-- landing in CLARIFICATION_REQUIRED:
--
--   • Posture      — explicit procedural relief / decision instruction is supplied.
--   • MotionTarget — the specific claim/motion target is supplied.
--   • Description  — rich, fact-bearing narrative (well above MinimumFactsQueryLength).
--
-- Modeled as a practice-area classification on the shared POLOXI.Legal_DecisionMatter aggregate
-- (PracticeAreaCode = 'Personal Injury') plus its one-to-one Legal_DecisionPIMatterProfile row.
-- Tenant-scoped to the development demo tenant; idempotent via a deterministic GUID.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- Deterministic IDs — Personal Injury matter (Ramirez v. Delta Freight — MSJ on liability)
DECLARE @PIM  UNIQUEIDENTIFIER = N'A3000000-0000-0000-0000-000000000001';
DECLARE @PIP  UNIQUEIDENTIFIER = N'A3000000-0000-0000-0000-000000000002';

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @PIM)
BEGIN
	INSERT POLOXI.Legal_DecisionMatter
		(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture,
		 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
		 PracticeAreaCode, ClaimTypeCode, Description, StatusCode, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@PIM,
		 N'Ramirez v. Delta Freight - Summary Judgment on Liability',
		 N'Motor Vehicle Accident',
		 N'Superior Court of California, County of Los Angeles',
		 N'Dispositive motion',
		 N'Plaintiff Maria Ramirez',
		 N'Defendant Delta Freight Logistics, Inc.',
		 N'Negligence per se claim (Vehicle Code § 22350 - unsafe speed)',
		 N'Summary judgment for plaintiff on liability, leaving only damages for trial',
		 N'PERSONAL_INJURY',
		 N'Negligence Per Se',
		 N'Plaintiff Maria Ramirez was stopped at a red light on Alameda Street when a Delta Freight ' +
		 N'tractor-trailer driven by defendant''s employee rear-ended her vehicle at approximately 40 mph. ' +
		 N'The police report cites the driver for unsafe speed under Vehicle Code section 22350, and the ' +
		 N'driver admitted in deposition that he looked away to adjust the GPS immediately before impact. ' +
		 N'Dashcam footage from a following vehicle corroborates that Ramirez''s car was fully stopped for ' +
		 N'several seconds before the collision. Plaintiff sustained a C5-C6 herniation requiring an ' +
		 N'epidural injection series and ongoing physical therapy. Plaintiff moves for summary judgment on ' +
		 N'liability, contending the statutory violation establishes negligence per se and that no triable ' +
		 N'issue of comparative fault exists on this record.',
		 N'OPEN', @Tenant, DATEADD(DAY, -3, @Now), @User);
END

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
		(@PIP, @PIM, N'Motor Vehicle Accident', CAST(DATEADD(MONTH, -8, @Now) AS DATE), N'17:42:00',
		 N'Alameda Street at 7th Street', N'Los Angeles', N'Los Angeles', N'California',
		 N'Rear-end collision at a controlled intersection; plaintiff stopped at a red light struck by ' +
		 N'defendant''s tractor-trailer traveling at unsafe speed.',
		 N'Defendant driver cited for Vehicle Code § 22350; admitted GPS distraction; dashcam confirms ' +
		 N'plaintiff was fully stopped. Negligence per se supported; comparative fault not evidenced.',
		 N'C5-C6 cervical disc herniation with radiculopathy.',
		 N'Emergency evaluation, cervical MRI, epidural steroid injection series, ongoing physical therapy.',
		 N'Special damages for medical treatment plus general damages; surgery consult pending.',
		 N'LITIGATION', N'MOTION_PENDING', N'DEMAND_SENT', N'NOT_SETTLED',
		 @Tenant, DATEADD(DAY, -3, @Now), @User);
END

COMMIT TRANSACTION;
