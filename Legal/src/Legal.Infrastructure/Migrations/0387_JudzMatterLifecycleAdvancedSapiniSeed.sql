-- ============================================================================
-- 0387: Judz Matter Lifecycle — PI_ADVANCED v1 + Sapini matter seed (data only).
--
-- WHAT THIS DOES (two things, both DATA on the 0385 engine core):
--   1. Publishes PI_ADVANCED v1 — an enriched, AI-era Personal Injury lifecycle
--      that improves on PI_STANDARD (0386). It becomes the DEFAULT PI lifecycle;
--      PI_STANDARD is demoted (kept active for matters already pinned to it).
--   2. Seeds the "Sapini" matter (hackathon Clio case) PURELY AS SEED DATA and
--      runs it entirely on the Judz Matter Lifecycle. There is NO live Clio
--      integration here — the Clio field values are just the source of the seed.
--      AuthorityMode = JUDZ_AUTHORITATIVE: the workflow is Judz.
--
-- WHY A RICHER GRAPH (ideas from leading legal-AI platforms today):
--   Modern PI case management (EvenUp, Eve, Supio, Parrot, Clio Duo, Filevine)
--   treats a matter as an intelligent pipeline, not a colored progress bar:
--     • CASE_QUALIFICATION — merit/liability/damages triage + SOL docketing before
--       spend (AI case scoring).
--     • RECORDS_COLLECTION  — provider/records/bills retrieval + lien identification
--       as a first-class stage (records orchestration is where PI firms live).
--     • DAMAGES_ANALYSIS    — AI damages valuation / specials tabulation / exposure
--       modeling feeding the demand (the "AI demand" wave).
--     • DISCOVERY & EXPERT_WORKUP — explicit litigation sub-stages (e-discovery,
--       IME, expert disclosures) instead of one opaque LITIGATION box.
--     • MEDIATION — explicit ADR branch (most PI cases resolve at ADR).
--   Requirements carry EvaluationMode = AUTOMATIC/HYBRID so Judz Decision
--   Intelligence can evaluate them (e.g. "specials totaled", "SOL docketed")
--   rather than relying on a human checkbox. Still fully domain-configurable —
--   no PI logic is hardcoded in the application layer.
--
-- INVARIANT (unchanged): Matter Stage ≠ Decision State ≠ POLOXI Branch State.
-- Directed graph with branches + legitimate re-entry; history is never destroyed.
-- Global defaults (TenantId NULL) for the definition; the Sapini runtime instance
-- is seeded on the active application tenant (like the Harper/Voss seed in 0318).
-- Idempotent / safe to re-run (deterministic ids + NOT EXISTS / upsert-by-code).
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

-- ── Definition: PI_ADVANCED (new default PI lifecycle) ───────────────────────
DECLARE @DefId UNIQUEIDENTIFIER =
	(SELECT TOP 1 MatterLifecycleDefinitionId FROM POLOXI.Legal_MatterLifecycleDefinition
	 WHERE Code = N'PI_ADVANCED' AND TenantId IS NULL AND IsDeleted = 0);
IF @DefId IS NULL
BEGIN
	SET @DefId = NEWID();
	INSERT INTO POLOXI.Legal_MatterLifecycleDefinition
		(MatterLifecycleDefinitionId, DecisionDomainPackId, Code, Name, Description, MatterTypeCode, IsDefault, IsActive, SortOrder, TenantId)
	VALUES
		(@DefId, @PiPackId, N'PI_ADVANCED', N'Personal Injury — AI-Augmented Lifecycle',
		 N'Enriched personal-injury lifecycle with case qualification, records collection, AI damages analysis, explicit discovery/expert/mediation litigation sub-stages, and automatically-evaluable stage requirements. Default for personal-injury matters.',
		 N'PERSONAL_INJURY', 1, 1, 0, NULL);
END

-- Demote PI_STANDARD so PI_ADVANCED is the single default for PERSONAL_INJURY
-- (PI_STANDARD stays active; matters already pinned to it are untouched).
UPDATE POLOXI.Legal_MatterLifecycleDefinition
	SET IsDefault = 0, ModifiedDateUtc = SYSUTCDATETIME()
	WHERE Code = N'PI_STANDARD' AND TenantId IS NULL AND IsDeleted = 0 AND IsDefault = 1;

-- ── Version v1 (PUBLISHED) ──────────────────────────────────────────────────
DECLARE @VerId UNIQUEIDENTIFIER =
	(SELECT TOP 1 MatterLifecycleVersionId FROM POLOXI.Legal_MatterLifecycleVersion
	 WHERE MatterLifecycleDefinitionId = @DefId AND VersionNumber = 1 AND IsDeleted = 0);
IF @VerId IS NULL
BEGIN
	SET @VerId = NEWID();
	INSERT INTO POLOXI.Legal_MatterLifecycleVersion
		(MatterLifecycleVersionId, MatterLifecycleDefinitionId, VersionNumber, VersionLabel, StatusCode, EffectiveFromUtc, PublishedUtc, TenantId)
	VALUES
		(@VerId, @DefId, 1, N'v1', N'PUBLISHED', SYSUTCDATETIME(), SYSUTCDATETIME(), NULL);
END

-- ── Stage definitions (upsert-by-code within version) ───────────────────────
DECLARE @Stages TABLE (Code NVARCHAR(100), Name NVARCHAR(200), DisplayOrder INT, IsInitial BIT, IsTerminal BIT, AllowReentry BIT, Category NVARCHAR(100), SlaDays INT NULL, Descr NVARCHAR(400));
INSERT INTO @Stages (Code, Name, DisplayOrder, IsInitial, IsTerminal, AllowReentry, Category, SlaDays, Descr) VALUES
	(N'INTAKE',             N'Intake',               1,  1, 0, 0, N'ONBOARDING', 7,    N'Client onboarding, conflict check, retainer execution.'),
	(N'CASE_QUALIFICATION', N'Case Qualification',   2,  0, 0, 0, N'ASSESSMENT', 10,   N'Merit, liability and damages triage; statute-of-limitations docketed; AI case score.'),
	(N'TREATMENT',          N'Treatment',            3,  0, 0, 1, N'MEDICAL',    NULL, N'Active medical treatment monitoring and gap detection; re-entrant while care is ongoing.'),
	(N'RECORDS_COLLECTION', N'Records & Bills',      4,  0, 0, 1, N'MEDICAL',    45,   N'Provider records and itemized bills retrieval; lien and no-fault identification.'),
	(N'DAMAGES_ANALYSIS',   N'Damages Analysis',     5,  0, 0, 0, N'VALUATION',  14,   N'Specials tabulation, wage-loss and future-care modeling; AI case-value and exposure analysis.'),
	(N'DEMAND',             N'Demand',               6,  0, 0, 0, N'PRE_SUIT',   30,   N'Demand package assembly and transmission to the carrier.'),
	(N'NEGOTIATION',        N'Negotiation',          7,  0, 0, 0, N'PRE_SUIT',   45,   N'Offer / counter tracking and settlement-authority management.'),
	(N'LITIGATION',         N'Litigation',           8,  0, 0, 0, N'LITIGATION', NULL, N'Suit filed; pleadings and motion practice.'),
	(N'DISCOVERY',          N'Discovery',            9,  0, 0, 1, N'LITIGATION', NULL, N'Written discovery, depositions and e-discovery.'),
	(N'EXPERT_WORKUP',      N'Expert Workup',        10, 0, 0, 0, N'LITIGATION', NULL, N'Expert disclosures, IME, and expert exchange.'),
	(N'MEDIATION',          N'Mediation / ADR',      11, 0, 0, 0, N'RESOLUTION', NULL, N'Mediation or other alternative dispute resolution.'),
	(N'TRIAL',              N'Trial',                12, 0, 0, 0, N'LITIGATION', NULL, N'Trial and verdict.'),
	(N'DISBURSEMENT',       N'Disbursement',         13, 0, 0, 0, N'RESOLUTION', 30,   N'Lien resolution and signed settlement disbursement statement.'),
	(N'CLOSED',             N'Closed',               14, 0, 1, 0, N'CLOSED',     NULL, N'Matter closed and archived.');

INSERT INTO POLOXI.Legal_MatterStageDefinition
	(MatterStageDefinitionId, MatterLifecycleVersionId, Code, Name, Description, StageCategory, DisplayOrder, IsInitial, IsTerminal, AllowReentry, DefaultSlaDays, IsActive, TenantId)
SELECT NEWID(), @VerId, s.Code, s.Name, s.Descr, s.Category, s.DisplayOrder, s.IsInitial, s.IsTerminal, s.AllowReentry, s.SlaDays, 1, NULL
FROM @Stages s
WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterStageDefinition d
				  WHERE d.MatterLifecycleVersionId = @VerId AND d.Code = s.Code AND d.IsDeleted = 0);

-- Resolve stage ids for transition wiring.
DECLARE @Intake UNIQUEIDENTIFIER, @Qual UNIQUEIDENTIFIER, @Treatment UNIQUEIDENTIFIER, @Records UNIQUEIDENTIFIER,
		@Damages UNIQUEIDENTIFIER, @Demand UNIQUEIDENTIFIER, @Negotiation UNIQUEIDENTIFIER, @Litigation UNIQUEIDENTIFIER,
		@Discovery UNIQUEIDENTIFIER, @Expert UNIQUEIDENTIFIER, @Mediation UNIQUEIDENTIFIER, @Trial UNIQUEIDENTIFIER,
		@Disbursement UNIQUEIDENTIFIER, @Closed UNIQUEIDENTIFIER;
SELECT @Intake       = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'INTAKE'             AND IsDeleted=0;
SELECT @Qual         = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'CASE_QUALIFICATION' AND IsDeleted=0;
SELECT @Treatment    = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'TREATMENT'          AND IsDeleted=0;
SELECT @Records      = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'RECORDS_COLLECTION' AND IsDeleted=0;
SELECT @Damages      = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'DAMAGES_ANALYSIS'   AND IsDeleted=0;
SELECT @Demand       = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'DEMAND'             AND IsDeleted=0;
SELECT @Negotiation  = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'NEGOTIATION'        AND IsDeleted=0;
SELECT @Litigation   = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'LITIGATION'         AND IsDeleted=0;
SELECT @Discovery    = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'DISCOVERY'          AND IsDeleted=0;
SELECT @Expert       = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'EXPERT_WORKUP'      AND IsDeleted=0;
SELECT @Mediation    = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'MEDIATION'          AND IsDeleted=0;
SELECT @Trial        = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'TRIAL'              AND IsDeleted=0;
SELECT @Disbursement = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'DISBURSEMENT'       AND IsDeleted=0;
SELECT @Closed       = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'CLOSED'             AND IsDeleted=0;

-- ── Directed transition graph (branches + re-entry; never inferred from order) ─
DECLARE @Trans TABLE (Code NVARCHAR(100), Name NVARCHAR(200), FromId UNIQUEIDENTIFIER, ToId UNIQUEIDENTIFIER, Type NVARCHAR(50), Priority INT);
INSERT INTO @Trans (Code, Name, FromId, ToId, Type, Priority) VALUES
	-- main advance path
	(N'INTAKE_TO_QUALIFICATION',       N'Qualify Case',            @Intake,       @Qual,         N'ADVANCE', 10),
	(N'QUALIFICATION_TO_TREATMENT',    N'Begin Treatment',         @Qual,         @Treatment,    N'ADVANCE', 10),
	(N'TREATMENT_TO_RECORDS',          N'Collect Records',         @Treatment,    @Records,      N'ADVANCE', 10),
	(N'RECORDS_TO_DAMAGES',            N'Analyze Damages',         @Records,      @Damages,      N'ADVANCE', 10),
	(N'DAMAGES_TO_DEMAND',             N'Prepare Demand',          @Damages,      @Demand,       N'ADVANCE', 10),
	(N'DEMAND_TO_NEGOTIATION',         N'Open Negotiation',        @Demand,       @Negotiation,  N'ADVANCE', 10),
	(N'NEGOTIATION_TO_LITIGATION',     N'File Suit',               @Negotiation,  @Litigation,   N'ADVANCE', 10),
	(N'LITIGATION_TO_DISCOVERY',       N'Enter Discovery',         @Litigation,   @Discovery,    N'ADVANCE', 10),
	(N'DISCOVERY_TO_EXPERT',           N'Expert Workup',           @Discovery,    @Expert,       N'ADVANCE', 10),
	(N'EXPERT_TO_MEDIATION',           N'Proceed to Mediation',    @Expert,       @Mediation,    N'ADVANCE', 10),
	(N'MEDIATION_TO_TRIAL',            N'Proceed to Trial',        @Mediation,    @Trial,        N'ADVANCE', 10),
	(N'TRIAL_TO_DISBURSEMENT',         N'Resolve After Trial',     @Trial,        @Disbursement, N'ADVANCE', 10),
	(N'DISBURSEMENT_TO_CLOSED',        N'Close Matter',            @Disbursement, @Closed,       N'CLOSE',   10),
	-- early / alternate settlement branches (PI cases resolve at many points)
	(N'NEGOTIATION_TO_DISBURSEMENT',   N'Settle (Pre-Suit)',       @Negotiation,  @Disbursement, N'BRANCH',  20),
	(N'DISCOVERY_TO_MEDIATION',        N'Early Mediation',         @Discovery,    @Mediation,    N'BRANCH',  20),
	(N'LITIGATION_TO_MEDIATION',       N'Direct to Mediation',     @Litigation,   @Mediation,    N'BRANCH',  25),
	(N'MEDIATION_TO_DISBURSEMENT',     N'Settle at Mediation',     @Mediation,    @Disbursement, N'BRANCH',  15),
	(N'EXPERT_TO_DISBURSEMENT',        N'Settle After Workup',     @Expert,       @Disbursement, N'BRANCH',  20),
	(N'DISCOVERY_TO_DISBURSEMENT',     N'Settle in Discovery',     @Discovery,    @Disbursement, N'BRANCH',  25),
	-- legitimate re-entry (history preserved): ongoing care or supplemental records
	(N'LITIGATION_TO_TREATMENT',       N'Return to Treatment',     @Litigation,   @Treatment,    N'REENTRY', 30),
	(N'DISCOVERY_TO_RECORDS',          N'Supplemental Records',    @Discovery,    @Records,      N'REENTRY', 35),
	(N'DAMAGES_TO_RECORDS',            N'Re-open Records',         @Damages,      @Records,      N'REENTRY', 35);

INSERT INTO POLOXI.Legal_MatterStageTransitionDefinition
	(MatterStageTransitionDefinitionId, MatterLifecycleVersionId, FromStageDefinitionId, ToStageDefinitionId, Code, Name, TransitionType, Priority, IsActive, TenantId)
SELECT NEWID(), @VerId, t.FromId, t.ToId, t.Code, t.Name, t.Type, t.Priority, 1, NULL
FROM @Trans t
WHERE t.FromId IS NOT NULL AND t.ToId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterStageTransitionDefinition x
				  WHERE x.MatterLifecycleVersionId = @VerId AND x.Code = t.Code AND x.IsDeleted = 0);

-- ── Stage requirements (intelligent lifecycle; AI-evaluable where it adds value) ─
DECLARE @Reqs TABLE (StageId UNIQUEIDENTIFIER, Code NVARCHAR(100), Name NVARCHAR(250), ReqType NVARCHAR(50), ReqLevel NVARCHAR(30), EvalMode NVARCHAR(30), IsBlocking BIT, DisplayOrder INT);
INSERT INTO @Reqs (StageId, Code, Name, ReqType, ReqLevel, EvalMode, IsBlocking, DisplayOrder) VALUES
	(@Intake,      N'INTAKE_RETAINER',         N'Signed retainer agreement on file',            N'DOCUMENT',    N'REQUIRED', N'HYBRID',    1, 1),
	(@Intake,      N'INTAKE_CONFLICT_CHECK',   N'Conflict check cleared',                       N'APPROVAL',    N'REQUIRED', N'MANUAL',    1, 2),
	(@Intake,      N'INTAKE_CLIENT_CONTACT',   N'Client contact details captured',              N'FIELD',       N'REQUIRED', N'AUTOMATIC', 1, 3),
	(@Qual,        N'QUAL_SOL_DOCKETED',       N'Statute of limitations calendared',            N'MILESTONE',   N'REQUIRED', N'HYBRID',    1, 1),
	(@Qual,        N'QUAL_LIABILITY_ASSESSED', N'Liability assessment completed',               N'DECISION',    N'REQUIRED', N'HYBRID',    1, 2),
	(@Qual,        N'QUAL_CASE_SCORE',         N'AI case-merit score recorded',                 N'DECISION',    N'EXPECTED', N'AUTOMATIC', 0, 3),
	(@Treatment,   N'TREATMENT_PROVIDERS',     N'At least one treating provider recorded',      N'PARTICIPANT', N'EXPECTED', N'AUTOMATIC', 0, 1),
	(@Treatment,   N'TREATMENT_GAP_REVIEW',    N'Treatment-gap review current',                 N'EVENT',       N'ADVISORY', N'AUTOMATIC', 0, 2),
	(@Records,     N'RECORDS_REQUESTED',       N'Records and bills requested from all providers',N'DOCUMENT',   N'REQUIRED', N'HYBRID',    1, 1),
	(@Records,     N'RECORDS_LIENS_IDENTIFIED',N'Liens and no-fault sources identified',        N'FINANCIAL',   N'REQUIRED', N'HYBRID',    1, 2),
	(@Damages,     N'DAMAGES_SPECIALS',        N'Medical specials totaled',                     N'FINANCIAL',   N'REQUIRED', N'AUTOMATIC', 1, 1),
	(@Damages,     N'DAMAGES_WAGE_LOSS',       N'Wage-loss documentation assembled',            N'FINANCIAL',   N'EXPECTED', N'HYBRID',    0, 2),
	(@Damages,     N'DAMAGES_VALUATION',       N'AI case-value / exposure model recorded',      N'DECISION',    N'EXPECTED', N'AUTOMATIC', 0, 3),
	(@Demand,      N'DEMAND_PACKAGE',          N'Demand package assembled',                     N'DOCUMENT',    N'REQUIRED', N'HYBRID',    1, 1),
	(@Demand,      N'DEMAND_SENT',             N'Demand transmitted to carrier',                N'EVENT',       N'REQUIRED', N'AUTOMATIC', 1, 2),
	(@Negotiation, N'NEGOTIATION_OFFER',       N'At least one carrier offer logged',            N'EVENT',       N'EXPECTED', N'AUTOMATIC', 0, 1),
	(@Negotiation, N'NEGOTIATION_AUTHORITY',   N'Client settlement authority documented',       N'APPROVAL',    N'EXPECTED', N'MANUAL',    0, 2),
	(@Litigation,  N'LITIGATION_COMPLAINT',    N'Complaint filed',                              N'DOCUMENT',    N'REQUIRED', N'HYBRID',    1, 1),
	(@Litigation,  N'LITIGATION_ANSWER',       N'Defendant answer / demands received',          N'DOCUMENT',    N'EXPECTED', N'AUTOMATIC', 0, 2),
	(@Discovery,   N'DISCOVERY_WRITTEN',       N'Written discovery responses served',           N'DOCUMENT',    N'REQUIRED', N'HYBRID',    1, 1),
	(@Discovery,   N'DISCOVERY_DEPOSITIONS',   N'Key depositions scheduled or taken',           N'EVENT',       N'EXPECTED', N'MANUAL',    0, 2),
	(@Expert,      N'EXPERT_DISCLOSURES',      N'Expert disclosures exchanged',                 N'DOCUMENT',    N'REQUIRED', N'HYBRID',    1, 1),
	(@Expert,      N'EXPERT_IME',              N'IME completed or waived',                       N'MILESTONE',   N'EXPECTED', N'MANUAL',    0, 2),
	(@Mediation,   N'MEDIATION_SCHEDULED',     N'Mediation scheduled with a neutral',           N'EVENT',       N'REQUIRED', N'MANUAL',    1, 1),
	(@Mediation,   N'MEDIATION_BRIEF',         N'Mediation brief / position statement filed',   N'DOCUMENT',    N'EXPECTED', N'HYBRID',    0, 2),
	(@Trial,       N'TRIAL_READINESS',         N'Pretrial readiness confirmed',                 N'MILESTONE',   N'REQUIRED', N'MANUAL',    1, 1),
	(@Disbursement,N'DISBURSEMENT_LIENS',      N'Outstanding liens resolved',                   N'FINANCIAL',   N'REQUIRED', N'HYBRID',    1, 1),
	(@Disbursement,N'DISBURSEMENT_STATEMENT',  N'Settlement disbursement statement signed',     N'DOCUMENT',    N'REQUIRED', N'HYBRID',    1, 2);

INSERT INTO POLOXI.Legal_MatterStageRequirementDefinition
	(MatterStageRequirementDefinitionId, MatterStageDefinitionId, Code, Name, RequirementType, RequirementLevel, EvaluationMode, DisplayOrder, IsBlocking, IsActive, TenantId)
SELECT NEWID(), r.StageId, r.Code, r.Name, r.ReqType, r.ReqLevel, r.EvalMode, r.DisplayOrder, r.IsBlocking, 1, NULL
FROM @Reqs r
WHERE r.StageId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterStageRequirementDefinition x
				  WHERE x.MatterStageDefinitionId = r.StageId AND x.Code = r.Code AND x.IsDeleted = 0);

-- ── External stage mapping: Clio stage codes → PI_ADVANCED stages ────────────
-- (Interop metadata only — NOT a live integration. Lets a future import land a
--  Clio-sourced matter on the right Judz stage without Judz depending on Clio.)
DECLARE @ClioId UNIQUEIDENTIFIER = (SELECT TOP 1 ExternalSystemId FROM POLOXI.Legal_ExternalSystem WHERE Code = N'CLIO' AND TenantId IS NULL AND IsDeleted = 0);
IF @ClioId IS NULL
BEGIN
	SET @ClioId = NEWID();
	INSERT INTO POLOXI.Legal_ExternalSystem (ExternalSystemId, Code, Name, IsActive, TenantId) VALUES (@ClioId, N'CLIO', N'Clio', 1, NULL);
END

DECLARE @Map TABLE (ExtCode NVARCHAR(100), ExtName NVARCHAR(200), StageId UNIQUEIDENTIFIER, Prio INT);
INSERT INTO @Map (ExtCode, ExtName, StageId, Prio) VALUES
	(N'Intake',         N'Intake',         @Intake,       0),
	(N'Qualification',  N'Qualification',  @Qual,         0),
	(N'Treatment',      N'Treatment',      @Treatment,    0),
	(N'Records',        N'Records',        @Records,      0),
	(N'Demand',         N'Demand',         @Demand,       0),
	(N'Negotiation',    N'Negotiation',    @Negotiation,  0),
	(N'Litigation',     N'Litigation',     @Litigation,   0),
	(N'Discovery',      N'Discovery',      @Discovery,    0),
	(N'Trial',          N'Trial',          @Trial,        0),
	(N'Disbursement',   N'Disbursement',   @Disbursement, 0),
	(N'Closed',         N'Closed',         @Closed,       0);

INSERT INTO POLOXI.Legal_ExternalStageMapping
	(ExternalStageMappingId, ExternalSystemId, MatterLifecycleVersionId, ExternalStageCode, ExternalStageName, MatterStageDefinitionId, MappingPriority, IsAuthoritative, IsActive, TenantId)
SELECT NEWID(), @ClioId, @VerId, m.ExtCode, m.ExtName, m.StageId, m.Prio, 1, 1, NULL
FROM @Map m
WHERE m.StageId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_ExternalStageMapping x
				  WHERE x.ExternalSystemId = @ClioId AND x.MatterLifecycleVersionId = @VerId
					AND x.ExternalStageCode = m.ExtCode AND x.IsDeleted = 0);

-- ════════════════════════════════════════════════════════════════════════════
-- SEED THE SAPINI MATTER (data only) RUNNING ON THE JUDZ LIFECYCLE
-- ════════════════════════════════════════════════════════════════════════════
DECLARE @Tenant UNIQUEIDENTIFIER = N'DA988708-62B2-4C14-956E-0BAA43C9E902';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();
DECLARE @DOI    DATE             = N'2023-04-23';   -- date of incident from the Clio case data

-- Deterministic ids (A3000003-… namespace, non-colliding).
DECLARE @SapMatter  UNIQUEIDENTIFIER = N'A3000003-0000-0000-0000-000000000001';
DECLARE @SapProfile UNIQUEIDENTIFIER = N'A3000003-0000-0000-0000-000000000002';
DECLARE @SapLife    UNIQUEIDENTIFIER = N'A3000003-0000-0000-0000-000000000010';

-- 1) The matter (Clio custom-field values captured as seed data in Description).
IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @SapMatter)
BEGIN
	INSERT POLOXI.Legal_DecisionMatter
		(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture,
		 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
		 PracticeAreaCode, ClaimTypeCode, Description, StatusCode, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@SapMatter,
		 N'Sapini, Justin - MVA (Cedar St & Garden St, New Rochelle)',
		 N'Personal Injury',
		 N'New York - Supreme Court, Westchester County',
		 N'In active litigation; no MMI, policy-limited exposure',
		 N'Plaintiff Justin Sapini',
		 N'Defendant Metro-North Commuter Railroad (self-insured)',
		 N'Negligence / motor-vehicle accident (sideswipe by Metro-North utility vehicle)',
		 N'Recovery up to available coverage net of liens',
		 N'PERSONAL_INJURY',
		 N'Motor Vehicle Accident',
		 N'── Clio Intake Summary (seed data) ──' + CHAR(13) + CHAR(10) +
		 N'Source: Clio Manage matter 00001-Sapini (Clio id 1811205428). Clio status: Open.' + CHAR(13) + CHAR(10) +
		 N'Date of Incident: 2023-04-23.' + CHAR(13) + CHAR(10) +
		 N'Accident Location: Cedar Street at its intersection with Garden Street, New Rochelle, Westchester County, NY.' + CHAR(13) + CHAR(10) +
		 N'Case Summary: Sideswiped by a Metro-North utility vehicle on Cedar Street. Injuries to both shoulders, both knees, and a head injury.' + CHAR(13) + CHAR(10) +
		 N'Insurance: Metro-North is SELF-INSURED (claims administered by Claims Service Bureau; claim SIR068120). Client no-fault: Progressive Insurance.' + CHAR(13) + CHAR(10) +
		 N'Policy Limits: Defendant liability $100,000/$300,000; Client UM/UIM $25,000/$50,000; No-fault $50,000. Policy limits confirmed: Yes.' + CHAR(13) + CHAR(10) +
		 N'Estimated Case Value: $375,000. Medical specials to date: $118,400. Wage loss claimed: $214,000 (2022 1099; no economic expert retained).' + CHAR(13) + CHAR(10) +
		 N'Liens / collateral: NY Medicaid lien $22,180.00 asserted; Progressive no-fault $50,000 basic economic loss exhausted (Ins. Law 5104(a)); SSD claim pending; defendants pleaded CPLR 4545 collateral source.' + CHAR(13) + CHAR(10) +
		 N'Liability: contested on two independent levels, neither fully investigated.' + CHAR(13) + CHAR(10) +
		 N'Treatment Status: active and ongoing, 3+ years post-loss; never discharged, no MMI declared. HIPAA authorization received: Yes.' + CHAR(13) + CHAR(10) +
		 N'Value rationale: economics alone ~$332,400 ($118,400 specials + $214,000 wage loss); second shoulder surgery still recommended, unscheduled. Exposure exceeds coverage; capped at the $100,000 defendant limit less the Medicaid lien.',
		 N'OPEN', @Tenant, DATEADD(DAY, -40, @Now), @User);
END

-- 2) One-to-one PI Profile extension (if the table exists in this database).
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterProfile', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @SapMatter)
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionPIMatterProfile WHERE DecisionPIMatterProfileId = @SapProfile)
BEGIN
	INSERT POLOXI.Legal_DecisionPIMatterProfile
		(DecisionPIMatterProfileId, DecisionMatterId, IncidentTypeCode, IncidentDate, IncidentTime,
		 IncidentLocation, IncidentCity, IncidentCounty, IncidentState,
		 IncidentSummary, LiabilitySummary, InjurySummary, TreatmentSummary, DamagesSummary,
		 CurrentStageCode, LitigationStatusCode, DemandStatusCode, SettlementStatusCode,
		 TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@SapProfile, @SapMatter, N'Motor Vehicle Accident', @DOI, NULL,
		 N'Cedar Street at Garden Street, New Rochelle', N'New Rochelle', N'Westchester', N'New York',
		 N'Plaintiff was sideswiped by a Metro-North utility vehicle at the Cedar/Garden intersection, sustaining bilateral shoulder, bilateral knee, and head injuries.',
		 N'Liability is contested on two independent grounds, neither fully investigated; comparative fault not yet resolved on the current record.',
		 N'Bilateral shoulder injuries (second shoulder surgery recommended, unscheduled), bilateral knee injuries, and a head injury.',
		 N'Active and ongoing treatment over three years post-loss; client never discharged and no MMI declared.',
		 N'Specials $118,400 to date; wage loss $214,000 claimed; Medicaid lien $22,180; no-fault $50,000 exhausted; exposure exceeds the $100,000 defendant policy limit.',
		 N'LITIGATION', N'IN_LITIGATION', N'DEMAND_SENT', N'NOT_SETTLED',
		 @Tenant, DATEADD(DAY, -40, @Now), @User);
END

-- 3) Judz lifecycle instance — pinned to PI_ADVANCED v1, current stage DISCOVERY.
--    (Case data shows pleadings, discovery responses, subpoena, expert exchange,
--     and a completed orthopedic IME → the matter is in active DISCOVERY.)
IF EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @SapMatter)
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterLifecycle WHERE MatterLifecycleId = @SapLife)
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterLifecycle WHERE DecisionMatterId = @SapMatter AND IsPrimary = 1 AND IsDeleted = 0)
BEGIN
	INSERT POLOXI.Legal_MatterLifecycle
		(MatterLifecycleId, DecisionMatterId, MatterLifecycleVersionId, CurrentStageDefinitionId,
		 StatusCode, AuthorityMode, StartedUtc, CurrentStageEnteredUtc, IsPrimary, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@SapLife, @SapMatter, @VerId, @Discovery,
		 N'ACTIVE', N'JUDZ_AUTHORITATIVE', DATEADD(DAY, -40, @Now), DATEADD(DAY, -6, @Now), 1, @Tenant, @Now, @User);

	-- 3a) Immutable stage history — the operational journey to the current stage.
	--     Treatment remains ongoing; the REENTRY edge is reflected as an open note,
	--     but the primary advance path is recorded chronologically. History is never destroyed.
	;WITH j (seq, StageId, EnteredDaysAgo, ExitedDaysAgo, EntryReason, ExitReason) AS (
		SELECT 1, @Intake,      40, 38, N'MATTER_OPENED',   N'ADVANCED'
		UNION ALL SELECT 2, @Qual,       38, 35, N'ADVANCED',       N'ADVANCED'
		UNION ALL SELECT 3, @Treatment,  35, 28, N'ADVANCED',       N'ADVANCED'
		UNION ALL SELECT 4, @Records,    28, 22, N'ADVANCED',       N'ADVANCED'
		UNION ALL SELECT 5, @Damages,    22, 18, N'ADVANCED',       N'ADVANCED'
		UNION ALL SELECT 6, @Demand,     18, 14, N'ADVANCED',       N'ADVANCED'
		UNION ALL SELECT 7, @Negotiation,14, 10, N'ADVANCED',       N'IMPASSE'
		UNION ALL SELECT 8, @Litigation, 10,  6, N'SUIT_FILED',     N'ADVANCED'
		UNION ALL SELECT 9, @Discovery,   6, NULL, N'ADVANCED',     NULL
	)
	INSERT POLOXI.Legal_MatterStageHistory
		(MatterStageHistoryId, MatterLifecycleId, MatterStageDefinitionId, EnteredUtc, ExitedUtc,
		 EntryReasonCode, ExitReasonCode, ChangedByType, ChangedByUserId, IsExternalAuthoritative, TenantId, CreatedDateUtc)
	SELECT NEWID(), @SapLife, j.StageId,
		   DATEADD(DAY, -j.EnteredDaysAgo, @Now),
		   CASE WHEN j.ExitedDaysAgo IS NULL THEN NULL ELSE DATEADD(DAY, -j.ExitedDaysAgo, @Now) END,
		   j.EntryReason, j.ExitReason, N'SYSTEM', @User, 0, @Tenant, @Now
	FROM j;

	-- 3b) Current-stage (DISCOVERY) requirement instances.
	INSERT POLOXI.Legal_MatterStageRequirementInstance
		(MatterStageRequirementInstanceId, MatterLifecycleId, MatterStageRequirementDefinitionId,
		 StatusCode, SatisfiedUtc, EvidenceSummary, TenantId, CreatedDateUtc, CreatedByUserId)
	SELECT NEWID(), @SapLife, d.MatterStageRequirementDefinitionId,
		   CASE d.Code WHEN N'DISCOVERY_WRITTEN' THEN N'SATISFIED' ELSE N'IN_PROGRESS' END,
		   CASE d.Code WHEN N'DISCOVERY_WRITTEN' THEN DATEADD(DAY, -4, @Now) ELSE NULL END,
		   CASE d.Code WHEN N'DISCOVERY_WRITTEN' THEN N'Responses to discovery demands served; defendants'' response on file.' ELSE NULL END,
		   @Tenant, @Now, @User
	FROM POLOXI.Legal_MatterStageRequirementDefinition d
	WHERE d.MatterStageDefinitionId = @Discovery AND d.IsDeleted = 0
	  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterStageRequirementInstance i
					  WHERE i.MatterLifecycleId = @SapLife AND i.MatterStageRequirementDefinitionId = d.MatterStageRequirementDefinitionId AND i.IsDeleted = 0);

	-- 3c) Append-only operational events (powers the "Since Your Last Review" feed).
	INSERT POLOXI.Legal_MatterStageEvent
		(MatterStageEventId, MatterLifecycleId, MatterStageDefinitionId, EventType, OccurredUtc, Title, Description, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(NEWID(), @SapLife, @Litigation,  N'DOCUMENT_RECEIVED',    DATEADD(DAY, -9, @Now), N'Summons & Complaint filed',           N'02 Pleadings — summons and complaint on file.', @Tenant, @Now, @User),
		(NEWID(), @SapLife, @Litigation,  N'DOCUMENT_RECEIVED',    DATEADD(DAY, -8, @Now), N'Verified Answer with demands',        N'Defendant''s verified answer and demands received.', @Tenant, @Now, @User),
		(NEWID(), @SapLife, @Discovery,   N'STAGE_ENTERED',        DATEADD(DAY, -6, @Now), N'Entered Discovery',                   N'Matter advanced into the Discovery stage.', @Tenant, @Now, @User),
		(NEWID(), @SapLife, @Discovery,   N'DOCUMENT_RECEIVED',    DATEADD(DAY, -4, @Now), N'Discovery responses served',          N'Responses to discovery demands and defendants'' response exchanged.', @Tenant, @Now, @User),
		(NEWID(), @SapLife, @Discovery,   N'MILESTONE_COMPLETED',  DATEADD(DAY, -3, @Now), N'Subpoena issued (Kyle Pullano)',      N'Non-party subpoena issued in discovery.', @Tenant, @Now, @User),
		(NEWID(), @SapLife, @Treatment,   N'DECISION_CHANGED',     DATEADD(DAY, -2, @Now), N'Treatment still ongoing — no MMI',    N'Client remains in active treatment 3+ years post-loss; second shoulder surgery recommended and unscheduled. Re-entry to Treatment available.', @Tenant, @Now, @User);
END

COMMIT TRANSACTION;
GO
