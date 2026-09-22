SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Intelligence — Personal Injury DB-backed option / config data.
-- Every selectable PI value is database-backed: simple dropdown lists reuse the existing
-- POLOXI.Legal_DecisionMatterOption (FieldCode/Value/DisplayName/SortOrder, 0211 pattern); PI
-- decision types and the matter-stage → default-decision-type map get dedicated config tables so
-- the /legal/personalinjury_decision cockpit loads them from the DB (no hardcoded C#/Razor lists).
-- Draft/provenance field source-type + verification-state enumerations are seeded here too so the
-- Generate-New-Matter review UI binds to DB-backed values. All objects live in the POLOXI schema.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Simple PI option lists (reuse Legal_DecisionMatterOption; idempotent MERGE). ──
MERGE POLOXI.Legal_DecisionMatterOption AS target
USING (VALUES
	-- ── Incident types ──
	(N'PI_INCIDENT_TYPE', N'Rear-end Collision',        10),
	(N'PI_INCIDENT_TYPE', N'Head-on Collision',         20),
	(N'PI_INCIDENT_TYPE', N'Side-impact Collision',     30),
	(N'PI_INCIDENT_TYPE', N'Rollover',                  40),
	(N'PI_INCIDENT_TYPE', N'Pedestrian Struck',         50),
	(N'PI_INCIDENT_TYPE', N'Bicycle Struck',            60),
	(N'PI_INCIDENT_TYPE', N'Slip and Fall',             70),
	(N'PI_INCIDENT_TYPE', N'Trip and Fall',             80),
	(N'PI_INCIDENT_TYPE', N'Falling Object',            90),
	(N'PI_INCIDENT_TYPE', N'Dog Bite / Animal Attack', 100),
	(N'PI_INCIDENT_TYPE', N'Defective Product',        110),
	(N'PI_INCIDENT_TYPE', N'Medical Procedure',        120),
	(N'PI_INCIDENT_TYPE', N'Other',                    130),

	-- ── Injury body areas ──
	(N'PI_BODY_AREA', N'Head',          10),
	(N'PI_BODY_AREA', N'Neck / Cervical',20),
	(N'PI_BODY_AREA', N'Back / Lumbar',  30),
	(N'PI_BODY_AREA', N'Shoulder',       40),
	(N'PI_BODY_AREA', N'Arm / Elbow',    50),
	(N'PI_BODY_AREA', N'Wrist / Hand',   60),
	(N'PI_BODY_AREA', N'Hip / Pelvis',   70),
	(N'PI_BODY_AREA', N'Knee',           80),
	(N'PI_BODY_AREA', N'Ankle / Foot',   90),
	(N'PI_BODY_AREA', N'Chest / Ribs',  100),
	(N'PI_BODY_AREA', N'Internal',      110),
	(N'PI_BODY_AREA', N'Psychological', 120),
	(N'PI_BODY_AREA', N'Multiple',      130),

	-- ── Injury severity ──
	(N'PI_INJURY_SEVERITY', N'Minor',        10),
	(N'PI_INJURY_SEVERITY', N'Moderate',     20),
	(N'PI_INJURY_SEVERITY', N'Severe',       30),
	(N'PI_INJURY_SEVERITY', N'Catastrophic', 40),

	-- ── Insurance coverage types ──
	(N'PI_COVERAGE_TYPE', N'Bodily Injury Liability', 10),
	(N'PI_COVERAGE_TYPE', N'Uninsured Motorist',      20),
	(N'PI_COVERAGE_TYPE', N'Underinsured Motorist',   30),
	(N'PI_COVERAGE_TYPE', N'MedPay',                  40),
	(N'PI_COVERAGE_TYPE', N'PIP',                     50),
	(N'PI_COVERAGE_TYPE', N'Umbrella / Excess',       60),
	(N'PI_COVERAGE_TYPE', N'Commercial General Liability', 70),
	(N'PI_COVERAGE_TYPE', N'Homeowners',              80),

	-- ── Coverage status ──
	(N'PI_COVERAGE_STATUS', N'Unverified',       10),
	(N'PI_COVERAGE_STATUS', N'Requested',        20),
	(N'PI_COVERAGE_STATUS', N'Confirmed',        30),
	(N'PI_COVERAGE_STATUS', N'Disputed',         40),
	(N'PI_COVERAGE_STATUS', N'Denied',           50),

	-- ── Treatment status ──
	(N'PI_TREATMENT_STATUS', N'Active',        10),
	(N'PI_TREATMENT_STATUS', N'Completed',     20),
	(N'PI_TREATMENT_STATUS', N'On Hold',       30),
	(N'PI_TREATMENT_STATUS', N'Referred Out',  40),
	(N'PI_TREATMENT_STATUS', N'MMI Reached',   50),

	-- ── Damage types ──
	(N'PI_DAMAGE_TYPE', N'Past Medical',         10),
	(N'PI_DAMAGE_TYPE', N'Future Medical',       20),
	(N'PI_DAMAGE_TYPE', N'Lost Wages',           30),
	(N'PI_DAMAGE_TYPE', N'Lost Earning Capacity',40),
	(N'PI_DAMAGE_TYPE', N'Property Damage',      50),
	(N'PI_DAMAGE_TYPE', N'Out-of-pocket',        60),
	(N'PI_DAMAGE_TYPE', N'Pain and Suffering',   70),
	(N'PI_DAMAGE_TYPE', N'Loss of Consortium',   80),
	(N'PI_DAMAGE_TYPE', N'Disfigurement',        90),
	(N'PI_DAMAGE_TYPE', N'Other',               100),

	-- ── Lien types ──
	(N'PI_LIEN_TYPE', N'Health Insurance',   10),
	(N'PI_LIEN_TYPE', N'Medicare',           20),
	(N'PI_LIEN_TYPE', N'Medicaid',           30),
	(N'PI_LIEN_TYPE', N'ERISA Plan',         40),
	(N'PI_LIEN_TYPE', N'Provider / LOP',     50),
	(N'PI_LIEN_TYPE', N'Workers Comp',       60),
	(N'PI_LIEN_TYPE', N'Attorney',           70),
	(N'PI_LIEN_TYPE', N'Other',              80),

	-- ── Lien status ──
	(N'PI_LIEN_STATUS', N'Asserted',    10),
	(N'PI_LIEN_STATUS', N'Verified',    20),
	(N'PI_LIEN_STATUS', N'Negotiating', 30),
	(N'PI_LIEN_STATUS', N'Resolved',    40),
	(N'PI_LIEN_STATUS', N'Waived',      50),

	-- ── Demand status ──
	(N'PI_DEMAND_STATUS', N'Not Sent',    10),
	(N'PI_DEMAND_STATUS', N'In Progress', 20),
	(N'PI_DEMAND_STATUS', N'Sent',        30),
	(N'PI_DEMAND_STATUS', N'Responded',   40),
	(N'PI_DEMAND_STATUS', N'Expired',     50),

	-- ── Settlement status ──
	(N'PI_SETTLEMENT_STATUS', N'Not Started',  10),
	(N'PI_SETTLEMENT_STATUS', N'Negotiating',  20),
	(N'PI_SETTLEMENT_STATUS', N'Agreed',       30),
	(N'PI_SETTLEMENT_STATUS', N'Disbursed',    40),
	(N'PI_SETTLEMENT_STATUS', N'Litigating',   50),

	-- ── Litigation status ──
	(N'PI_LITIGATION_STATUS', N'Pre-Litigation', 10),
	(N'PI_LITIGATION_STATUS', N'Filed',          20),
	(N'PI_LITIGATION_STATUS', N'Discovery',      30),
	(N'PI_LITIGATION_STATUS', N'Mediation',      40),
	(N'PI_LITIGATION_STATUS', N'Trial Set',      50),
	(N'PI_LITIGATION_STATUS', N'Resolved',       60),

	-- ── Matter stage ──
	(N'PI_MATTER_STAGE', N'Intake',        10),
	(N'PI_MATTER_STAGE', N'Investigation', 20),
	(N'PI_MATTER_STAGE', N'Treatment',     30),
	(N'PI_MATTER_STAGE', N'Demand',        40),
	(N'PI_MATTER_STAGE', N'Negotiation',   50),
	(N'PI_MATTER_STAGE', N'Litigation',    60),
	(N'PI_MATTER_STAGE', N'Mediation',     70),
	(N'PI_MATTER_STAGE', N'Settlement',    80),
	(N'PI_MATTER_STAGE', N'Closed',        90),

	-- ── Deadline rule sources ──
	(N'PI_DEADLINE_RULE_SOURCE', N'Statute of Limitations',  10),
	(N'PI_DEADLINE_RULE_SOURCE', N'Government Claim Rule',    20),
	(N'PI_DEADLINE_RULE_SOURCE', N'Court Scheduling Order',   30),
	(N'PI_DEADLINE_RULE_SOURCE', N'Discovery Rule',           40),
	(N'PI_DEADLINE_RULE_SOURCE', N'Contractual Deadline',     50),

	-- ── Deadline verification states ──
	(N'PI_DEADLINE_VERIFICATION', N'Potential',           10),
	(N'PI_DEADLINE_VERIFICATION', N'Calculated',          20),
	(N'PI_DEADLINE_VERIFICATION', N'Verified',            30),
	(N'PI_DEADLINE_VERIFICATION', N'Attorney Confirmed',  40),

	-- ── Vehicle roles ──
	(N'PI_VEHICLE_ROLE', N'Client Vehicle',    10),
	(N'PI_VEHICLE_ROLE', N'Defendant Vehicle', 20),
	(N'PI_VEHICLE_ROLE', N'Third-party Vehicle',30),

	-- ── Draft field source types (provenance) ──
	(N'PI_DRAFT_SOURCE_TYPE', N'User Supplied',       10),
	(N'PI_DRAFT_SOURCE_TYPE', N'Document Extracted',  20),
	(N'PI_DRAFT_SOURCE_TYPE', N'Inferred',            30),
	(N'PI_DRAFT_SOURCE_TYPE', N'Missing',             40),

	-- ── Draft field verification states (provenance) ──
	(N'PI_DRAFT_VERIFICATION_STATE', N'Confirmed', 10),
	(N'PI_DRAFT_VERIFICATION_STATE', N'Review',    20),
	(N'PI_DRAFT_VERIFICATION_STATE', N'Missing',   30),
	(N'PI_DRAFT_VERIFICATION_STATE', N'Conflict',  40),
	(N'PI_DRAFT_VERIFICATION_STATE', N'Invalid',   50)
) AS source (FieldCode, Value, SortOrder)
ON target.FieldCode = source.FieldCode AND target.Value = source.Value
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FieldCode, Value, DisplayName, SortOrder)
	VALUES (source.FieldCode, source.Value, source.Value, source.SortOrder);

GO

-- ── Legal_DecisionPIDecisionType: DB-backed PI decision-intelligence question types. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIDecisionType', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIDecisionType
(
	DecisionPIDecisionTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIDecisionType PRIMARY KEY DEFAULT NEWID(),
	DecisionTypeCode         NVARCHAR(60) NOT NULL,
	Name                     NVARCHAR(200) NOT NULL,
	Description              NVARCHAR(1000) NULL,
	SortOrder                INT NOT NULL CONSTRAINT DF_Legal_DecisionPIDecisionType_SortOrder DEFAULT 0,
	IsActive                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIDecisionType_IsActive DEFAULT 1,
	TenantId                 UNIQUEIDENTIFIER NULL,
	CreatedDateUtc           DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIDecisionType_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId          UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc          DATETIME2 NULL,
	ModifiedByUserId         UNIQUEIDENTIFIER NULL,
	IsDeleted                BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIDecisionType_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_DecisionPIDecisionType_Code UNIQUE (DecisionTypeCode)
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIDecisionType') AND name = N'IX_Legal_DecisionPIDecisionType_Lookup')
	CREATE INDEX IX_Legal_DecisionPIDecisionType_Lookup
		ON POLOXI.Legal_DecisionPIDecisionType (IsActive, SortOrder) INCLUDE (DecisionTypeCode) WHERE IsDeleted = 0;

GO

MERGE POLOXI.Legal_DecisionPIDecisionType AS target
USING (VALUES
	(N'CASE_ACCEPTANCE',     N'Case Acceptance',      N'Should the firm accept / retain this matter?',                       10),
	(N'LIABILITY_ASSESSMENT',N'Liability Assessment', N'How strong is liability against the defendant(s)?',                  20),
	(N'COMPARATIVE_FAULT',   N'Comparative Fault',    N'What comparative-fault exposure applies to the client?',            30),
	(N'CAUSATION_ASSESSMENT',N'Causation Assessment', N'Is the injury medically attributable to the incident?',            40),
	(N'DAMAGES_ASSESSMENT',  N'Damages Assessment',   N'What is the supportable damages picture?',                          50),
	(N'COVERAGE_STRATEGY',   N'Coverage Strategy',    N'What coverage / recovery sources are available and collectable?',  60),
	(N'DEMAND_STRATEGY',     N'Demand Strategy',      N'What demand posture and amount is supported?',                      70),
	(N'SETTLEMENT_OFFER',    N'Settlement Offer',     N'Should the client accept, counter, or reject the current offer?',  80),
	(N'LITIGATION_READINESS',N'Litigation Readiness', N'Is the matter ready to file / proceed in litigation?',             90),
	(N'DISCOVERY_PRIORITY',  N'Discovery Priority',   N'What discovery should be prioritized next?',                       100),
	(N'MEDIATION_STRATEGY',  N'Mediation Strategy',   N'What is the optimal mediation posture?',                           110),
	(N'TRIAL_STRATEGY',      N'Trial Strategy',       N'What is the optimal trial posture?',                               120)
) AS source (DecisionTypeCode, Name, Description, SortOrder)
ON target.DecisionTypeCode = source.DecisionTypeCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionTypeCode, Name, Description, SortOrder)
	VALUES (source.DecisionTypeCode, source.Name, source.Description, source.SortOrder);

GO

-- ── Legal_DecisionPIStageDecisionMap: DB-backed matter-stage → default decision type. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIStageDecisionMap', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIStageDecisionMap
(
	DecisionPIStageDecisionMapId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIStageDecisionMap PRIMARY KEY DEFAULT NEWID(),
	StageCode                    NVARCHAR(60) NOT NULL,
	DefaultDecisionTypeCode      NVARCHAR(60) NOT NULL,
	SortOrder                    INT NOT NULL CONSTRAINT DF_Legal_DecisionPIStageDecisionMap_SortOrder DEFAULT 0,
	IsActive                     BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIStageDecisionMap_IsActive DEFAULT 1,
	TenantId                     UNIQUEIDENTIFIER NULL,
	CreatedDateUtc               DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIStageDecisionMap_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId              UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc              DATETIME2 NULL,
	ModifiedByUserId             UNIQUEIDENTIFIER NULL,
	IsDeleted                    BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIStageDecisionMap_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_DecisionPIStageDecisionMap_Stage UNIQUE (StageCode)
);

GO

MERGE POLOXI.Legal_DecisionPIStageDecisionMap AS target
USING (VALUES
	(N'Intake',        N'CASE_ACCEPTANCE',      10),
	(N'Investigation', N'LIABILITY_ASSESSMENT', 20),
	(N'Treatment',     N'CAUSATION_ASSESSMENT', 30),
	(N'Demand',        N'DEMAND_STRATEGY',      40),
	(N'Negotiation',   N'SETTLEMENT_OFFER',     50),
	(N'Litigation',    N'LITIGATION_READINESS', 60),
	(N'Mediation',     N'MEDIATION_STRATEGY',   70),
	(N'Settlement',    N'SETTLEMENT_OFFER',     80)
) AS source (StageCode, DefaultDecisionTypeCode, SortOrder)
ON target.StageCode = source.StageCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (StageCode, DefaultDecisionTypeCode, SortOrder)
	VALUES (source.StageCode, source.DefaultDecisionTypeCode, source.SortOrder);

GO

COMMIT TRANSACTION;
