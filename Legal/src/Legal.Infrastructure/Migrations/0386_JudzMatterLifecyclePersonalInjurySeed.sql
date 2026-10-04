-- ============================================================================
-- 0386: Seed the first Judz Matter Lifecycle — PI_STANDARD v1 (Personal Injury).
--
-- The 8 Personal Injury stages become the FIRST versioned Domain Lifecycle
-- Definition on the engine core from migration 0385. This is DATA, not schema:
-- other domains publish completely different lifecycles against the same tables.
--
-- Directed graph (NOT StageOrder=1,2,3):
--   INTAKE → TREATMENT → DEMAND → NEGOTIATION → LITIGATION → TRIAL → DISBURSEMENT → CLOSED
-- plus branches:
--   NEGOTIATION → DISBURSEMENT (early settlement)
--   LITIGATION  → DISBURSEMENT (settle during litigation)
--   TRIAL       → DISBURSEMENT
-- plus legitimate re-entry (history is never destroyed):
--   LITIGATION  → TREATMENT   (matter returns to an earlier operational state)
--
-- Clio external stage codes are mapped so Clio "Litigation" → PI_STANDARD/LITIGATION
-- without making Judz dependent on Clio. Clio-sourced matters should run
-- EXTERNAL_AUTHORITATIVE so sync does not fight the attorney (applied at runtime).
--
-- Global defaults (TenantId NULL); tenant rows may override later.
-- Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

-- ── Definition ──────────────────────────────────────────────────────────────
DECLARE @DefId UNIQUEIDENTIFIER =
	(SELECT TOP 1 MatterLifecycleDefinitionId FROM POLOXI.Legal_MatterLifecycleDefinition
	 WHERE Code = N'PI_STANDARD' AND TenantId IS NULL AND IsDeleted = 0);
IF @DefId IS NULL
BEGIN
	SET @DefId = NEWID();
	INSERT INTO POLOXI.Legal_MatterLifecycleDefinition
		(MatterLifecycleDefinitionId, DecisionDomainPackId, Code, Name, Description, MatterTypeCode, IsDefault, IsActive, SortOrder, TenantId)
	VALUES
		(@DefId, @PiPackId, N'PI_STANDARD', N'Personal Injury — Standard Lifecycle',
		 N'Standard operational lifecycle for personal-injury matters from intake through disbursement.',
		 N'PERSONAL_INJURY', 1, 1, 0, NULL);
END

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
-- Helper insert pattern: only insert a stage code if missing for this version.
DECLARE @Stages TABLE (Code NVARCHAR(100), Name NVARCHAR(200), DisplayOrder INT, IsInitial BIT, IsTerminal BIT, AllowReentry BIT, Category NVARCHAR(100), SlaDays INT NULL);
INSERT INTO @Stages (Code, Name, DisplayOrder, IsInitial, IsTerminal, AllowReentry, Category, SlaDays) VALUES
	(N'INTAKE',       N'Intake',       1, 1, 0, 0, N'ONBOARDING', 14),
	(N'TREATMENT',    N'Treatment',    2, 0, 0, 1, N'MEDICAL',    NULL),
	(N'DEMAND',       N'Demand',       3, 0, 0, 0, N'PRE_SUIT',   30),
	(N'NEGOTIATION',  N'Negotiation',  4, 0, 0, 0, N'PRE_SUIT',   45),
	(N'LITIGATION',   N'Litigation',   5, 0, 0, 0, N'LITIGATION', NULL),
	(N'TRIAL',        N'Trial',        6, 0, 0, 0, N'LITIGATION', NULL),
	(N'DISBURSEMENT', N'Disbursement', 7, 0, 0, 0, N'RESOLUTION', 30),
	(N'CLOSED',       N'Closed',       8, 0, 1, 0, N'CLOSED',     NULL);

INSERT INTO POLOXI.Legal_MatterStageDefinition
	(MatterStageDefinitionId, MatterLifecycleVersionId, Code, Name, StageCategory, DisplayOrder, IsInitial, IsTerminal, AllowReentry, DefaultSlaDays, IsActive, TenantId)
SELECT NEWID(), @VerId, s.Code, s.Name, s.Category, s.DisplayOrder, s.IsInitial, s.IsTerminal, s.AllowReentry, s.SlaDays, 1, NULL
FROM @Stages s
WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterStageDefinition d
				  WHERE d.MatterLifecycleVersionId = @VerId AND d.Code = s.Code AND d.IsDeleted = 0);

-- Resolve stage ids for transition wiring.
DECLARE @Intake UNIQUEIDENTIFIER, @Treatment UNIQUEIDENTIFIER, @Demand UNIQUEIDENTIFIER, @Negotiation UNIQUEIDENTIFIER,
		@Litigation UNIQUEIDENTIFIER, @Trial UNIQUEIDENTIFIER, @Disbursement UNIQUEIDENTIFIER, @Closed UNIQUEIDENTIFIER;
SELECT @Intake       = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'INTAKE'       AND IsDeleted=0;
SELECT @Treatment    = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'TREATMENT'    AND IsDeleted=0;
SELECT @Demand       = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'DEMAND'       AND IsDeleted=0;
SELECT @Negotiation  = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'NEGOTIATION'  AND IsDeleted=0;
SELECT @Litigation   = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'LITIGATION'   AND IsDeleted=0;
SELECT @Trial        = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'TRIAL'        AND IsDeleted=0;
SELECT @Disbursement = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'DISBURSEMENT' AND IsDeleted=0;
SELECT @Closed       = MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition WHERE MatterLifecycleVersionId=@VerId AND Code=N'CLOSED'       AND IsDeleted=0;

-- ── Directed transition graph ───────────────────────────────────────────────
DECLARE @Trans TABLE (Code NVARCHAR(100), Name NVARCHAR(200), FromId UNIQUEIDENTIFIER, ToId UNIQUEIDENTIFIER, Type NVARCHAR(50), Priority INT);
INSERT INTO @Trans (Code, Name, FromId, ToId, Type, Priority) VALUES
	(N'INTAKE_TO_TREATMENT',       N'Begin Treatment',           @Intake,       @Treatment,    N'ADVANCE', 10),
	(N'TREATMENT_TO_DEMAND',       N'Prepare Demand',            @Treatment,    @Demand,       N'ADVANCE', 10),
	(N'DEMAND_TO_NEGOTIATION',     N'Open Negotiation',         @Demand,       @Negotiation,  N'ADVANCE', 10),
	(N'NEGOTIATION_TO_LITIGATION', N'Escalate to Litigation',   @Negotiation,  @Litigation,   N'ADVANCE', 10),
	(N'NEGOTIATION_TO_DISBURSEMENT',N'Settle (Pre-Suit)',       @Negotiation,  @Disbursement, N'BRANCH',  20),
	(N'LITIGATION_TO_TRIAL',       N'Proceed to Trial',         @Litigation,   @Trial,        N'ADVANCE', 10),
	(N'LITIGATION_TO_DISBURSEMENT',N'Settle (Litigation)',      @Litigation,   @Disbursement, N'BRANCH',  20),
	(N'LITIGATION_TO_TREATMENT',   N'Return to Treatment',      @Litigation,   @Treatment,    N'REENTRY', 30),
	(N'TRIAL_TO_DISBURSEMENT',     N'Resolve After Trial',      @Trial,        @Disbursement, N'ADVANCE', 10),
	(N'DISBURSEMENT_TO_CLOSED',    N'Close Matter',             @Disbursement, @Closed,       N'CLOSE',   10);

INSERT INTO POLOXI.Legal_MatterStageTransitionDefinition
	(MatterStageTransitionDefinitionId, MatterLifecycleVersionId, FromStageDefinitionId, ToStageDefinitionId, Code, Name, TransitionType, Priority, IsActive, TenantId)
SELECT NEWID(), @VerId, t.FromId, t.ToId, t.Code, t.Name, t.Type, t.Priority, 1, NULL
FROM @Trans t
WHERE t.FromId IS NOT NULL AND t.ToId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterStageTransitionDefinition x
				  WHERE x.MatterLifecycleVersionId = @VerId AND x.Code = t.Code AND x.IsDeleted = 0);

-- ── Baseline stage requirements (intelligent lifecycle, not a progress bar) ──
DECLARE @Reqs TABLE (StageId UNIQUEIDENTIFIER, Code NVARCHAR(100), Name NVARCHAR(250), ReqType NVARCHAR(50), ReqLevel NVARCHAR(30), IsBlocking BIT, DisplayOrder INT);
INSERT INTO @Reqs (StageId, Code, Name, ReqType, ReqLevel, IsBlocking, DisplayOrder) VALUES
	(@Intake,      N'INTAKE_RETAINER',       N'Signed retainer agreement on file',       N'DOCUMENT', N'REQUIRED', 1, 1),
	(@Intake,      N'INTAKE_CLIENT_CONTACT', N'Client contact details captured',         N'FIELD',    N'REQUIRED', 1, 2),
	(@Treatment,   N'TREATMENT_PROVIDERS',   N'At least one treating provider recorded', N'PARTICIPANT', N'EXPECTED', 0, 1),
	(@Treatment,   N'TREATMENT_RECORDS',     N'Medical records requested',               N'DOCUMENT', N'EXPECTED', 0, 2),
	(@Demand,      N'DEMAND_SPECIALS',       N'Medical specials totaled',                N'FINANCIAL', N'REQUIRED', 1, 1),
	(@Demand,      N'DEMAND_PACKAGE',        N'Demand package assembled',                N'DOCUMENT', N'REQUIRED', 1, 2),
	(@Negotiation, N'NEGOTIATION_OFFER',     N'At least one carrier offer logged',       N'EVENT',    N'EXPECTED', 0, 1),
	(@Litigation,  N'LITIGATION_COMPLAINT',  N'Complaint filed',                         N'DOCUMENT', N'REQUIRED', 1, 1),
	(@Litigation,  N'LITIGATION_DISCOVERY',  N'Discovery artifacts identified',          N'DOCUMENT', N'EXPECTED', 0, 2),
	(@Trial,       N'TRIAL_READINESS',       N'Pretrial readiness confirmed',            N'MILESTONE', N'REQUIRED', 1, 1),
	(@Disbursement,N'DISBURSEMENT_LIENS',    N'Outstanding liens resolved',              N'FINANCIAL', N'REQUIRED', 1, 1),
	(@Disbursement,N'DISBURSEMENT_STATEMENT',N'Settlement disbursement statement signed',N'DOCUMENT', N'REQUIRED', 1, 2);

INSERT INTO POLOXI.Legal_MatterStageRequirementDefinition
	(MatterStageRequirementDefinitionId, MatterStageDefinitionId, Code, Name, RequirementType, RequirementLevel, EvaluationMode, DisplayOrder, IsBlocking, IsActive, TenantId)
SELECT NEWID(), r.StageId, r.Code, r.Name, r.ReqType, r.ReqLevel, N'MANUAL', r.DisplayOrder, r.IsBlocking, 1, NULL
FROM @Reqs r
WHERE r.StageId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterStageRequirementDefinition x
				  WHERE x.MatterStageDefinitionId = r.StageId AND x.Code = r.Code AND x.IsDeleted = 0);

-- ── External systems (CMS-independence) ─────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_ExternalSystem WHERE Code = N'CLIO' AND TenantId IS NULL AND IsDeleted = 0)
	INSERT INTO POLOXI.Legal_ExternalSystem (ExternalSystemId, Code, Name, IsActive, TenantId) VALUES (NEWID(), N'CLIO', N'Clio', 1, NULL);
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_ExternalSystem WHERE Code = N'JUDZ' AND TenantId IS NULL AND IsDeleted = 0)
	INSERT INTO POLOXI.Legal_ExternalSystem (ExternalSystemId, Code, Name, IsActive, TenantId) VALUES (NEWID(), N'JUDZ', N'Judz (native)', 1, NULL);

DECLARE @ClioId UNIQUEIDENTIFIER = (SELECT TOP 1 ExternalSystemId FROM POLOXI.Legal_ExternalSystem WHERE Code = N'CLIO' AND TenantId IS NULL AND IsDeleted = 0);

-- ── Clio external stage mappings → Judz PI_STANDARD stages ──────────────────
DECLARE @Map TABLE (ExtCode NVARCHAR(100), ExtName NVARCHAR(200), StageId UNIQUEIDENTIFIER);
INSERT INTO @Map (ExtCode, ExtName, StageId) VALUES
	(N'Intake',       N'Intake',       @Intake),
	(N'Treatment',    N'Treatment',    @Treatment),
	(N'Demand',       N'Demand',       @Demand),
	(N'Negotiation',  N'Negotiation',  @Negotiation),
	(N'Litigation',   N'Litigation',   @Litigation),
	(N'Trial',        N'Trial',        @Trial),
	(N'Disbursement', N'Disbursement', @Disbursement),
	(N'Closed',       N'Closed',       @Closed);

INSERT INTO POLOXI.Legal_ExternalStageMapping
	(ExternalStageMappingId, ExternalSystemId, MatterLifecycleVersionId, ExternalStageCode, ExternalStageName, MatterStageDefinitionId, MappingPriority, IsAuthoritative, IsActive, TenantId)
SELECT NEWID(), @ClioId, @VerId, m.ExtCode, m.ExtName, m.StageId, 0, 1, 1, NULL
FROM @Map m
WHERE @ClioId IS NOT NULL AND m.StageId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_ExternalStageMapping x
				  WHERE x.ExternalSystemId = @ClioId AND x.MatterLifecycleVersionId = @VerId
					AND x.ExternalStageCode = m.ExtCode AND x.IsDeleted = 0);

COMMIT TRANSACTION;
GO
