SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Intelligence — Personal Injury Matter Profile + child aggregates.
-- Personal Injury is a first-class practice-area domain OVER the shared POLOXI.Legal_DecisionMatter
-- aggregate root (0283 added PracticeAreaCode/ClaimTypeCode). This migration adds the structured PI
-- matter data model:
--   • Legal_DecisionPIMatterProfile  — one-to-one extension of the Matter (incident/summary/stage).
--   • child aggregates (one-to-many): insurance policies, injuries, treatments, medical bills,
--     damages, liens, demands, negotiations, settlements, deadlines, incident vehicles, witnesses.
-- POLOXI Core objects (candidates/branches/graph/verification) are NOT duplicated — PI only adds
-- domain data. All objects live in POLOXI schema, are prefixed Legal_DecisionPI*, and carry the
-- standard base/audit fields (TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc,
-- ModifiedByUserId, IsDeleted). Table → API → UI: schema first.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Legal_DecisionPIMatterProfile: one-to-one PI extension of the shared Matter aggregate. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterProfile', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIMatterProfile
(
	DecisionPIMatterProfileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIMatterProfile PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId          UNIQUEIDENTIFIER NOT NULL,
	IncidentTypeCode          NVARCHAR(60) NULL,
	IncidentDate              DATE NULL,
	IncidentTime              TIME(0) NULL,
	IncidentLocation          NVARCHAR(400) NULL,
	IncidentCity              NVARCHAR(120) NULL,
	IncidentCounty            NVARCHAR(120) NULL,
	IncidentState             NVARCHAR(60) NULL,
	IncidentSummary           NVARCHAR(MAX) NULL,
	LiabilitySummary          NVARCHAR(MAX) NULL,
	InjurySummary             NVARCHAR(MAX) NULL,
	TreatmentSummary          NVARCHAR(MAX) NULL,
	DamagesSummary            NVARCHAR(MAX) NULL,
	CurrentStageCode          NVARCHAR(60) NULL,
	LitigationStatusCode      NVARCHAR(60) NULL,
	DemandStatusCode          NVARCHAR(60) NULL,
	SettlementStatusCode      NVARCHAR(60) NULL,
	TenantId                  UNIQUEIDENTIFIER NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIMatterProfile_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIMatterProfile_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterProfile') AND name = N'UX_Legal_DecisionPIMatterProfile_Matter')
	CREATE UNIQUE INDEX UX_Legal_DecisionPIMatterProfile_Matter
		ON POLOXI.Legal_DecisionPIMatterProfile (DecisionMatterId) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIInsurancePolicy: liability / UM-UIM / MedPay coverage on the matter. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIInsurancePolicy', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIInsurancePolicy
(
	DecisionPIInsurancePolicyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIInsurancePolicy PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId            UNIQUEIDENTIFIER NOT NULL,
	Carrier                     NVARCHAR(200) NULL,
	Insured                     NVARCHAR(200) NULL,
	Adjuster                    NVARCHAR(200) NULL,
	ClaimNumber                 NVARCHAR(120) NULL,
	PolicyNumber                NVARCHAR(120) NULL,
	CoverageTypeCode            NVARCHAR(60) NULL,
	BodilyInjuryLimitPerPerson  DECIMAL(18,2) NULL,
	BodilyInjuryLimitPerOccur   DECIMAL(18,2) NULL,
	CoverageStatusCode          NVARCHAR(60) NULL,
	LimitsSource                NVARCHAR(400) NULL,
	LimitsVerified              BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIInsurancePolicy_LimitsVerified DEFAULT 0,
	Notes                       NVARCHAR(MAX) NULL,
	TenantId                    UNIQUEIDENTIFIER NULL,
	CreatedDateUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIInsurancePolicy_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId             UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc             DATETIME2 NULL,
	ModifiedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                   BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIInsurancePolicy_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIInsurancePolicy') AND name = N'IX_Legal_DecisionPIInsurancePolicy_Matter')
	CREATE INDEX IX_Legal_DecisionPIInsurancePolicy_Matter
		ON POLOXI.Legal_DecisionPIInsurancePolicy (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIInjury: injured body area / diagnosis / severity. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIInjury', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIInjury
(
	DecisionPIInjuryId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIInjury PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId      UNIQUEIDENTIFIER NOT NULL,
	BodyAreaCode          NVARCHAR(60) NULL,
	InitialSymptoms       NVARCHAR(1000) NULL,
	Diagnosis             NVARCHAR(1000) NULL,
	IsPreexisting         BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIInjury_IsPreexisting DEFAULT 0,
	ClaimedPermanency     BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIInjury_ClaimedPermanency DEFAULT 0,
	SurgeryRecommended    BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIInjury_SurgeryRecommended DEFAULT 0,
	SurgeryPerformed      BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIInjury_SurgeryPerformed DEFAULT 0,
	SeverityCode          NVARCHAR(60) NULL,
	Notes                 NVARCHAR(MAX) NULL,
	TenantId              UNIQUEIDENTIFIER NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIInjury_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIInjury_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIInjury') AND name = N'IX_Legal_DecisionPIInjury_Matter')
	CREATE INDEX IX_Legal_DecisionPIInjury_Matter
		ON POLOXI.Legal_DecisionPIInjury (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPITreatment: provider treatment history. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPITreatment', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPITreatment
(
	DecisionPITreatmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPITreatment PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId      UNIQUEIDENTIFIER NOT NULL,
	Provider              NVARCHAR(200) NULL,
	Specialty             NVARCHAR(120) NULL,
	FirstTreatmentDate    DATE NULL,
	LastTreatmentDate     DATE NULL,
	StatusCode            NVARCHAR(60) NULL,
	VisitCount            INT NULL,
	RecordRequestStatus   NVARCHAR(60) NULL,
	BillRequestStatus     NVARCHAR(60) NULL,
	TreatmentGapDays      INT NULL,
	Notes                 NVARCHAR(MAX) NULL,
	TenantId              UNIQUEIDENTIFIER NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPITreatment_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_DecisionPITreatment_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPITreatment') AND name = N'IX_Legal_DecisionPITreatment_Matter')
	CREATE INDEX IX_Legal_DecisionPITreatment_Matter
		ON POLOXI.Legal_DecisionPITreatment (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIMedicalBill: billed / paid / outstanding medical financials. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIMedicalBill', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIMedicalBill
(
	DecisionPIMedicalBillId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIMedicalBill PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId        UNIQUEIDENTIFIER NOT NULL,
	Provider                NVARCHAR(200) NULL,
	AmountBilled            DECIMAL(18,2) NULL,
	Adjustments             DECIMAL(18,2) NULL,
	AmountPaid              DECIMAL(18,2) NULL,
	OutstandingBalance      DECIMAL(18,2) NULL,
	Payer                   NVARCHAR(200) NULL,
	LienStatusCode          NVARCHAR(60) NULL,
	Notes                   NVARCHAR(MAX) NULL,
	TenantId                UNIQUEIDENTIFIER NULL,
	CreatedDateUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIMedicalBill_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId         UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc         DATETIME2 NULL,
	ModifiedByUserId        UNIQUEIDENTIFIER NULL,
	IsDeleted               BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIMedicalBill_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIMedicalBill') AND name = N'IX_Legal_DecisionPIMedicalBill_Matter')
	CREATE INDEX IX_Legal_DecisionPIMedicalBill_Matter
		ON POLOXI.Legal_DecisionPIMedicalBill (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIDamage: economic / non-economic damage line items. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIDamage', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIDamage
(
	DecisionPIDamageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIDamage PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId   UNIQUEIDENTIFIER NOT NULL,
	DamageTypeCode     NVARCHAR(60) NULL,
	Description        NVARCHAR(1000) NULL,
	ClaimedAmount      DECIMAL(18,2) NULL,
	IsEconomic         BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIDamage_IsEconomic DEFAULT 1,
	Notes              NVARCHAR(MAX) NULL,
	TenantId           UNIQUEIDENTIFIER NULL,
	CreatedDateUtc     DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIDamage_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId    UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc    DATETIME2 NULL,
	ModifiedByUserId   UNIQUEIDENTIFIER NULL,
	IsDeleted          BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIDamage_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIDamage') AND name = N'IX_Legal_DecisionPIDamage_Matter')
	CREATE INDEX IX_Legal_DecisionPIDamage_Matter
		ON POLOXI.Legal_DecisionPIDamage (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPILien: lien / subrogation tracking. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPILien', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPILien
(
	DecisionPILienId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPILien PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId   UNIQUEIDENTIFIER NOT NULL,
	Lienholder         NVARCHAR(200) NULL,
	LienTypeCode       NVARCHAR(60) NULL,
	AssertedAmount     DECIMAL(18,2) NULL,
	VerifiedAmount     DECIMAL(18,2) NULL,
	NegotiatedAmount   DECIMAL(18,2) NULL,
	FinalPayoffAmount  DECIMAL(18,2) NULL,
	StatusCode         NVARCHAR(60) NULL,
	Notes              NVARCHAR(MAX) NULL,
	TenantId           UNIQUEIDENTIFIER NULL,
	CreatedDateUtc     DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPILien_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId    UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc    DATETIME2 NULL,
	ModifiedByUserId   UNIQUEIDENTIFIER NULL,
	IsDeleted          BIT NOT NULL CONSTRAINT DF_Legal_DecisionPILien_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPILien') AND name = N'IX_Legal_DecisionPILien_Matter')
	CREATE INDEX IX_Legal_DecisionPILien_Matter
		ON POLOXI.Legal_DecisionPILien (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIDemand: demand package tracking. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIDemand', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIDemand
(
	DecisionPIDemandId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIDemand PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId   UNIQUEIDENTIFIER NOT NULL,
	DemandDate         DATE NULL,
	DemandAmount       DECIMAL(18,2) NULL,
	Recipient          NVARCHAR(200) NULL,
	ResponseDeadline   DATE NULL,
	StatusCode         NVARCHAR(60) NULL,
	Notes              NVARCHAR(MAX) NULL,
	TenantId           UNIQUEIDENTIFIER NULL,
	CreatedDateUtc     DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIDemand_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId    UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc    DATETIME2 NULL,
	ModifiedByUserId   UNIQUEIDENTIFIER NULL,
	IsDeleted          BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIDemand_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIDemand') AND name = N'IX_Legal_DecisionPIDemand_Matter')
	CREATE INDEX IX_Legal_DecisionPIDemand_Matter
		ON POLOXI.Legal_DecisionPIDemand (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPINegotiation: offer / counteroffer history. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPINegotiation', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPINegotiation
(
	DecisionPINegotiationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPINegotiation PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId        UNIQUEIDENTIFIER NOT NULL,
	EventDate               DATE NULL,
	Amount                  DECIMAL(18,2) NULL,
	Source                  NVARCHAR(200) NULL,
	IsOffer                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionPINegotiation_IsOffer DEFAULT 1,
	Conditions              NVARCHAR(1000) NULL,
	ExpirationDate          DATE NULL,
	Notes                   NVARCHAR(MAX) NULL,
	TenantId                UNIQUEIDENTIFIER NULL,
	CreatedDateUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPINegotiation_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId         UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc         DATETIME2 NULL,
	ModifiedByUserId        UNIQUEIDENTIFIER NULL,
	IsDeleted               BIT NOT NULL CONSTRAINT DF_Legal_DecisionPINegotiation_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPINegotiation') AND name = N'IX_Legal_DecisionPINegotiation_Matter')
	CREATE INDEX IX_Legal_DecisionPINegotiation_Matter
		ON POLOXI.Legal_DecisionPINegotiation (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPISettlement: settlement / net-recovery record. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPISettlement', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPISettlement
(
	DecisionPISettlementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPISettlement PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId       UNIQUEIDENTIFIER NOT NULL,
	GrossRecovery          DECIMAL(18,2) NULL,
	Fees                   DECIMAL(18,2) NULL,
	Costs                  DECIMAL(18,2) NULL,
	Liens                  DECIMAL(18,2) NULL,
	NetToClient            DECIMAL(18,2) NULL,
	StatusCode             NVARCHAR(60) NULL,
	SettlementDate         DATE NULL,
	Notes                  NVARCHAR(MAX) NULL,
	TenantId               UNIQUEIDENTIFIER NULL,
	CreatedDateUtc         DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPISettlement_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId        UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc        DATETIME2 NULL,
	ModifiedByUserId       UNIQUEIDENTIFIER NULL,
	IsDeleted              BIT NOT NULL CONSTRAINT DF_Legal_DecisionPISettlement_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPISettlement') AND name = N'IX_Legal_DecisionPISettlement_Matter')
	CREATE INDEX IX_Legal_DecisionPISettlement_Matter
		ON POLOXI.Legal_DecisionPISettlement (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIDeadline: SOL / filing / procedural deadlines with verification state. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIDeadline', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIDeadline
(
	DecisionPIDeadlineId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIDeadline PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId     UNIQUEIDENTIFIER NOT NULL,
	DeadlineTypeCode     NVARCHAR(60) NULL,
	CandidateDate        DATE NULL,
	RuleSourceCode       NVARCHAR(60) NULL,
	VerificationState    NVARCHAR(60) NULL,
	Notes                NVARCHAR(MAX) NULL,
	TenantId             UNIQUEIDENTIFIER NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIDeadline_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIDeadline_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIDeadline') AND name = N'IX_Legal_DecisionPIDeadline_Matter')
	CREATE INDEX IX_Legal_DecisionPIDeadline_Matter
		ON POLOXI.Legal_DecisionPIDeadline (DecisionMatterId, CandidateDate) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIIncidentVehicle: vehicles involved (MVA matters). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIIncidentVehicle', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIIncidentVehicle
(
	DecisionPIIncidentVehicleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIIncidentVehicle PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId            UNIQUEIDENTIFIER NOT NULL,
	RoleCode                    NVARCHAR(60) NULL,
	Description                 NVARCHAR(400) NULL,
	Owner                       NVARCHAR(200) NULL,
	Driver                      NVARCHAR(200) NULL,
	ImpactType                  NVARCHAR(120) NULL,
	Citation                    NVARCHAR(200) NULL,
	Notes                       NVARCHAR(MAX) NULL,
	TenantId                    UNIQUEIDENTIFIER NULL,
	CreatedDateUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIIncidentVehicle_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId             UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc             DATETIME2 NULL,
	ModifiedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                   BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIIncidentVehicle_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIIncidentVehicle') AND name = N'IX_Legal_DecisionPIIncidentVehicle_Matter')
	CREATE INDEX IX_Legal_DecisionPIIncidentVehicle_Matter
		ON POLOXI.Legal_DecisionPIIncidentVehicle (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Legal_DecisionPIWitness: witnesses to the incident. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIWitness', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionPIWitness
(
	DecisionPIWitnessId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionPIWitness PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId    UNIQUEIDENTIFIER NOT NULL,
	Name                NVARCHAR(200) NULL,
	ContactInfo         NVARCHAR(400) NULL,
	StatementSummary    NVARCHAR(MAX) NULL,
	SupportsClient      BIT NULL,
	Notes               NVARCHAR(MAX) NULL,
	TenantId            UNIQUEIDENTIFIER NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionPIWitness_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_DecisionPIWitness_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionPIWitness') AND name = N'IX_Legal_DecisionPIWitness_Matter')
	CREATE INDEX IX_Legal_DecisionPIWitness_Matter
		ON POLOXI.Legal_DecisionPIWitness (DecisionMatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
