SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Intelligence — Domain Pack architecture (/legal/personalinjury_decision)
--
--                     POLOXI CORE
--                         │
--               LEGAL DECISION INTELLIGENCE
--                         │
--                  LEGAL DOMAIN PACK
--                         │
--          ┌──────────────┼───────────────┐
--   Personal Injury    Employment      Contract
--
-- A Domain Pack supplies DOMAIN SEMANTICS only: terminology, decision-hierarchy dimensions,
-- evidence classifications, verification profiles, and the matter-type taxonomy. It does NOT own
-- ambiguity, hierarchy governance, Candidate × Branch competition, frontier, information value,
-- adaptive narrowing, dependency propagation, recompetition, flip points, convergence, or readiness —
-- those remain in POLOXI Core. Domain concepts are advisory (never hard-coded conclusions).
--
-- Global (TenantId NULL) rows are seeded defaults; tenant rows override, mirroring 0173 AbvDomainPack.
-- All objects live in the POLOXI schema, are prefixed Legal_Decision*, and carry base/audit fields.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Legal_DecisionDomainPack: a practice-area domain pack (PERSONAL_INJURY, EMPLOYMENT, …). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPack', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPack
(
	DecisionDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPack PRIMARY KEY DEFAULT NEWID(),
	PackCode             NVARCHAR(60) NOT NULL,
	PracticeAreaCode     NVARCHAR(60) NOT NULL,
	Name                 NVARCHAR(200) NOT NULL,
	Description          NVARCHAR(1000) NULL,
	IsDefault            BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPack_IsDefault DEFAULT 0,
	IsActive             BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPack_IsActive DEFAULT 1,
	SortOrder            INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPack_SortOrder DEFAULT 0,
	TenantId             UNIQUEIDENTIFIER NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPack_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPack_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPack') AND name = N'IX_Legal_DecisionDomainPack_Lookup')
	CREATE INDEX IX_Legal_DecisionDomainPack_Lookup
		ON POLOXI.Legal_DecisionDomainPack (PracticeAreaCode, IsActive, IsDefault, SortOrder) INCLUDE (PackCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionDomainPackDimension: decision-hierarchy dimensions (Liability, Causation, …). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackDimension', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackDimension
(
	DecisionDomainPackDimensionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackDimension PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackDimension_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	DimensionCode                 NVARCHAR(60) NOT NULL,
	Name                          NVARCHAR(200) NOT NULL,
	Description                   NVARCHAR(1000) NULL,
	SortOrder                     INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDimension_SortOrder DEFAULT 0,
	IsActive                      BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDimension_IsActive DEFAULT 1,
	TenantId                      UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDimension_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId               UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc               DATETIME2 NULL,
	ModifiedByUserId              UNIQUEIDENTIFIER NULL,
	IsDeleted                     BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDimension_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackDimension') AND name = N'IX_Legal_DecisionDomainPackDimension_Pack')
	CREATE INDEX IX_Legal_DecisionDomainPackDimension_Pack
		ON POLOXI.Legal_DecisionDomainPackDimension (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (DimensionCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionDomainPackEvidenceType: source/evidence classifications (Medical Records, …). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceType', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackEvidenceType
(
	DecisionDomainPackEvidenceTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackEvidenceType PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackEvidenceType_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	EvidenceTypeCode                 NVARCHAR(60) NOT NULL,
	Name                             NVARCHAR(200) NOT NULL,
	DimensionCode                    NVARCHAR(60) NULL,   -- optional link to the dimension it most informs
	Description                      NVARCHAR(1000) NULL,
	SortOrder                        INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackEvidenceType_SortOrder DEFAULT 0,
	IsActive                         BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackEvidenceType_IsActive DEFAULT 1,
	TenantId                         UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                   DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackEvidenceType_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                  UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                  DATETIME2 NULL,
	ModifiedByUserId                 UNIQUEIDENTIFIER NULL,
	IsDeleted                        BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackEvidenceType_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceType') AND name = N'IX_Legal_DecisionDomainPackEvidenceType_Pack')
	CREATE INDEX IX_Legal_DecisionDomainPackEvidenceType_Pack
		ON POLOXI.Legal_DecisionDomainPackEvidenceType (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (EvidenceTypeCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionDomainPackVerificationProfile: source-routing / verification profiles. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackVerificationProfile', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackVerificationProfile
(
	DecisionDomainPackVerificationProfileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackVerificationProfile PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackVerificationProfile_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	ProfileCode                             NVARCHAR(60) NOT NULL,
	Name                                    NVARCHAR(200) NOT NULL,
	EvidenceTypeCode                        NVARCHAR(60) NULL,   -- optional evidence type this profile governs
	Description                             NVARCHAR(1000) NULL,
	SortOrder                               INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackVerificationProfile_SortOrder DEFAULT 0,
	IsActive                                BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackVerificationProfile_IsActive DEFAULT 1,
	TenantId                                UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                          DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackVerificationProfile_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                         UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                         DATETIME2 NULL,
	ModifiedByUserId                        UNIQUEIDENTIFIER NULL,
	IsDeleted                               BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackVerificationProfile_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackVerificationProfile') AND name = N'IX_Legal_DecisionDomainPackVerificationProfile_Pack')
	CREATE INDEX IX_Legal_DecisionDomainPackVerificationProfile_Pack
		ON POLOXI.Legal_DecisionDomainPackVerificationProfile (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (ProfileCode) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionDomainPackMatterType: matter-type taxonomy owned by the pack. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackMatterType', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackMatterType
(
	DecisionDomainPackMatterTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackMatterType PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackMatterType_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	MatterTypeCode                 NVARCHAR(120) NOT NULL,
	Name                           NVARCHAR(200) NOT NULL,
	Description                    NVARCHAR(1000) NULL,
	SortOrder                      INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackMatterType_SortOrder DEFAULT 0,
	IsActive                       BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackMatterType_IsActive DEFAULT 1,
	TenantId                       UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                 DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackMatterType_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                DATETIME2 NULL,
	ModifiedByUserId               UNIQUEIDENTIFIER NULL,
	IsDeleted                      BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackMatterType_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackMatterType') AND name = N'IX_Legal_DecisionDomainPackMatterType_Pack')
	CREATE INDEX IX_Legal_DecisionDomainPackMatterType_Pack
		ON POLOXI.Legal_DecisionDomainPackMatterType (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (MatterTypeCode) WHERE IsDeleted = 0;

GO

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Seed the Personal Injury Domain Pack (global default, TenantId NULL). Idempotent by PackCode.
-- ────────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NULL
BEGIN
	SET @PiPackId = NEWID();
	INSERT INTO POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId, PackCode, PracticeAreaCode, Name, Description, IsDefault, IsActive, SortOrder, TenantId)
	VALUES (@PiPackId, N'PERSONAL_INJURY', N'PERSONAL_INJURY', N'Personal Injury',
		N'Domain semantics for Personal Injury matters: liability, causation, injury, damages, evidence, defenses, insurance, procedure, and settlement. Advisory only — POLOXI Core owns all decision reasoning.',
		1, 1, 10, NULL);
END

-- ── Decision-hierarchy dimensions ──
MERGE POLOXI.Legal_DecisionDomainPackDimension AS target
USING (VALUES
	(N'LIABILITY',   N'Liability',    N'Duty, breach, and comparative responsibility.',                     10),
	(N'INJURY',      N'Injury',       N'Diagnosis, symptoms, permanency, aggravation/preexisting conditions.',20),
	(N'CAUSATION',   N'Causation',    N'Incident → injury relationship and intervening causes.',            30),
	(N'DAMAGES',     N'Damages',      N'Medical expenses, lost earnings, future and noneconomic damages.',    40),
	(N'EVIDENCE',    N'Evidence',     N'Medical records, imaging, bills, photos/video, witnesses, experts.',  50),
	(N'DEFENSES',    N'Defenses',     N'Comparative fault, causation dispute, mitigation, preexisting condition.',60),
	(N'INSURANCE',   N'Insurance',    N'Coverage/limits where relevant to the matter.',                      70),
	(N'PROCEDURE',   N'Procedure',    N'Limitations, discovery, motions, trial posture.',                    80),
	(N'SETTLEMENT',  N'Settlement',   N'Competing valuation/outcome scenarios.',                             90)
) AS source (DimensionCode, Name, Description, SortOrder)
ON target.DecisionDomainPackId = @PiPackId AND target.DimensionCode = source.DimensionCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, DimensionCode, Name, Description, SortOrder, TenantId)
	VALUES (@PiPackId, source.DimensionCode, source.Name, source.Description, source.SortOrder, NULL);

-- ── Evidence types ──
MERGE POLOXI.Legal_DecisionDomainPackEvidenceType AS target
USING (VALUES
	(N'MEDICAL_RECORDS',   N'Medical Records',        N'INJURY',     N'Treating provider records and history.',            10),
	(N'IMAGING',           N'Imaging',                N'INJURY',     N'MRI, CT, X-ray and diagnostic imaging.',            20),
	(N'MEDICAL_BILLS',     N'Medical Bills',          N'DAMAGES',    N'Billing records for past medical expenses.',        30),
	(N'PHOTOS_VIDEO',      N'Photographs / Video',    N'LIABILITY',  N'Scene, vehicle, and injury photos or video.',       40),
	(N'WITNESS_STATEMENTS',N'Witness Statements',     N'LIABILITY',  N'Lay witness accounts of the incident.',             50),
	(N'EXPERT_REPORTS',    N'Expert Reports',         N'CAUSATION',  N'Medical, accident-reconstruction, economic experts.',60),
	(N'ACCIDENT_REPORT',   N'Accident / Police Report',N'LIABILITY', N'Official incident report.',                         70),
	(N'DEPOSITION',        N'Deposition Testimony',   N'LIABILITY',  N'Sworn deposition transcripts.',                     80),
	(N'WAGE_RECORDS',      N'Employment / Wage Records',N'DAMAGES',   N'Earnings and lost-wage documentation.',             90),
	(N'DEMAND_OFFER',      N'Demand / Offer Correspondence',N'SETTLEMENT',N'Demand letters and settlement offers.',        100)
) AS source (EvidenceTypeCode, Name, DimensionCode, Description, SortOrder)
ON target.DecisionDomainPackId = @PiPackId AND target.EvidenceTypeCode = source.EvidenceTypeCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, EvidenceTypeCode, Name, DimensionCode, Description, SortOrder, TenantId)
	VALUES (@PiPackId, source.EvidenceTypeCode, source.Name, source.DimensionCode, source.Description, source.SortOrder, NULL);

-- ── Verification profiles ──
MERGE POLOXI.Legal_DecisionDomainPackVerificationProfile AS target
USING (VALUES
	(N'MEDICAL_CAUSATION', N'Medical Causation Verification', N'MEDICAL_RECORDS', N'Verify injury-to-incident causation against treating records and imaging.', 10),
	(N'DAMAGES_QUANTUM',   N'Damages Quantum Verification',   N'MEDICAL_BILLS',   N'Verify economic damages against bills and wage records.',                  20),
	(N'LIABILITY_FACTS',   N'Liability Fact Verification',    N'ACCIDENT_REPORT', N'Verify incident facts against report, photos, and witnesses.',            30),
	(N'EXPERT_FOUNDATION', N'Expert Foundation Verification', N'EXPERT_REPORTS',  N'Verify expert opinions are supported and admissible.',                    40)
) AS source (ProfileCode, Name, EvidenceTypeCode, Description, SortOrder)
ON target.DecisionDomainPackId = @PiPackId AND target.ProfileCode = source.ProfileCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, ProfileCode, Name, EvidenceTypeCode, Description, SortOrder, TenantId)
	VALUES (@PiPackId, source.ProfileCode, source.Name, source.EvidenceTypeCode, source.Description, source.SortOrder, NULL);

-- ── Matter-type taxonomy ──
MERGE POLOXI.Legal_DecisionDomainPackMatterType AS target
USING (VALUES
	(N'Motor Vehicle Accident', N'Motor Vehicle Accident', N'Auto, truck, motorcycle collisions.',        10),
	(N'Premises Liability',      N'Premises Liability',      N'Injuries on another party''s property.',      20),
	(N'Product Liability',       N'Product Liability',       N'Defective product injuries.',                30),
	(N'Medical Malpractice',     N'Medical Malpractice',     N'Provider negligence injuries.',              40),
	(N'Wrongful Death',          N'Wrongful Death',          N'Fatal-injury claims.',                       50),
	(N'Dog Bite / Animal Injury',N'Dog Bite / Animal Injury',N'Animal-caused injuries.',                    60),
	(N'Slip and Fall',           N'Slip and Fall',           N'Fall-related premises injuries.',            70),
	(N'Other Negligence',        N'Other Negligence',        N'General negligence not otherwise classified.',80)
) AS source (MatterTypeCode, Name, Description, SortOrder)
ON target.DecisionDomainPackId = @PiPackId AND target.MatterTypeCode = source.MatterTypeCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, MatterTypeCode, Name, Description, SortOrder, TenantId)
	VALUES (@PiPackId, source.MatterTypeCode, source.Name, source.Description, source.SortOrder, NULL);

GO

COMMIT TRANSACTION;
