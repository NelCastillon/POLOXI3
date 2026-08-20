-- 0126_NonRenewalWorkflow.sql
-- Enterprise Non-Renewal workflow: carrier notice intake, state-mandated notice deadline tracking,
-- and insured notification proof. Creates lookup tables (statuses, reasons, state notice requirements),
-- workflow tables (NonRenewal header, NonRenewalActivity), and seeds tenant-scoped configuration data.
-- Idempotent.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Policy') EXEC(N'CREATE SCHEMA Policy');

-- ────────────────────────────────────────────────────────────────────────────
-- Lookup: Non-Renewal Status (per tenant, DB-backed dropdown source)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.NonRenewalStatus', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.NonRenewalStatus
	(
		NonRenewalStatusId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_NonRenewalStatus PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		StatusName NVARCHAR(100) NOT NULL,
		Description NVARCHAR(400) NULL,
		ColorHex NVARCHAR(10) NULL,
		IsTerminal BIT NOT NULL CONSTRAINT DF_NonRenewalStatus_IsTerminal DEFAULT 0,
		IsDefault BIT NOT NULL CONSTRAINT DF_NonRenewalStatus_IsDefault DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_NonRenewalStatus_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_NonRenewalStatus_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_NonRenewalStatus_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_NonRenewalStatus_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_NonRenewalStatus_Tenant_Code UNIQUE (TenantId, StatusCode)
	);
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Lookup: Non-Renewal Reasons (per tenant)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.NonRenewalReason', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.NonRenewalReason
	(
		NonRenewalReasonId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_NonRenewalReason PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ReasonCode NVARCHAR(50) NOT NULL,
		ReasonName NVARCHAR(150) NOT NULL,
		Description NVARCHAR(400) NULL,
		CategoryCode NVARCHAR(40) NOT NULL CONSTRAINT DF_NonRenewalReason_Category DEFAULT N'Carrier',
		IsRemarketRecommended BIT NOT NULL CONSTRAINT DF_NonRenewalReason_Remarket DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_NonRenewalReason_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_NonRenewalReason_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_NonRenewalReason_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_NonRenewalReason_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_NonRenewalReason_Tenant_Code UNIQUE (TenantId, ReasonCode)
	);
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Lookup: State-mandated notice requirements (per tenant, per state)
-- Minimum advance-notice days a carrier/agency must give before expiration.
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.NonRenewalStateRequirement', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.NonRenewalStateRequirement
	(
		NonRenewalStateRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_NonRenewalStateReq PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		StateCode NVARCHAR(2) NOT NULL,
		StateName NVARCHAR(60) NOT NULL,
		LineCategoryCode NVARCHAR(30) NOT NULL CONSTRAINT DF_NonRenewalStateReq_LineCat DEFAULT N'All',
		MinimumNoticeDays INT NOT NULL,
		InsuredNoticeDays INT NOT NULL CONSTRAINT DF_NonRenewalStateReq_InsuredDays DEFAULT 0,
		Notes NVARCHAR(400) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_NonRenewalStateReq_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_NonRenewalStateReq_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_NonRenewalStateReq_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_NonRenewalStateReq_Tenant_State_Line UNIQUE (TenantId, StateCode, LineCategoryCode)
	);
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Workflow: Non-Renewal header
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.NonRenewal', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.NonRenewal
	(
		NonRenewalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_NonRenewal PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NULL,
		AccountId UNIQUEIDENTIFIER NULL,
		NonRenewalNumber NVARCHAR(50) NOT NULL,
		PolicyNumber NVARCHAR(120) NOT NULL,
		AccountName NVARCHAR(200) NOT NULL,
		CarrierName NVARCHAR(200) NULL,
		LineOfBusiness NVARCHAR(120) NULL,
		StateCode NVARCHAR(2) NULL,
		PolicyExpirationDate DATE NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_NonRenewal_Status DEFAULT N'NoticeReceived',
		ReasonCode NVARCHAR(50) NULL,
		InitiatedByCode NVARCHAR(30) NOT NULL CONSTRAINT DF_NonRenewal_InitiatedBy DEFAULT N'Carrier',
		-- Carrier notice intake
		CarrierNoticeDate DATE NULL,
		CarrierNoticeMethodCode NVARCHAR(40) NULL,
		CarrierNoticeReference NVARCHAR(120) NULL,
		CarrierNoticeSummary NVARCHAR(1000) NULL,
		-- State-mandated deadline tracking
		RequiredNoticeDays INT NULL,
		NoticeDeadlineDate DATE NULL,
		IsNoticeCompliant BIT NULL,
		-- Insured notification proof
		InsuredNotifiedDate DATE NULL,
		InsuredNotificationMethodCode NVARCHAR(40) NULL,
		InsuredNotificationProofReference NVARCHAR(200) NULL,
		InsuredNotificationSentByName NVARCHAR(200) NULL,
		-- Remarketing / resolution
		RemarketRecommended BIT NOT NULL CONSTRAINT DF_NonRenewal_Remarket DEFAULT 0,
		RemarketSubmissionId UNIQUEIDENTIFIER NULL,
		ResolutionSummary NVARCHAR(1000) NULL,
		AssignedToUserId UNIQUEIDENTIFIER NULL,
		AssignedToName NVARCHAR(200) NULL,
		CompletedDateUtc DATETIME2 NULL,
		Notes NVARCHAR(2000) NULL,
		IsUrgent BIT NOT NULL CONSTRAINT DF_NonRenewal_IsUrgent DEFAULT 0,
		IsArchived BIT NOT NULL CONSTRAINT DF_NonRenewal_IsArchived DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_NonRenewal_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_NonRenewal_IsDeleted DEFAULT 0
	);
	CREATE INDEX IX_Policy_NonRenewal_Tenant ON Policy.NonRenewal (TenantId, IsDeleted, IsArchived);
	CREATE INDEX IX_Policy_NonRenewal_Policy ON Policy.NonRenewal (PolicyId);
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Workflow: Non-Renewal activity / audit trail
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.NonRenewalActivity', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.NonRenewalActivity
	(
		ActivityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_NonRenewalActivity PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		NonRenewalId UNIQUEIDENTIFIER NOT NULL,
		ActivityType NVARCHAR(50) NOT NULL,
		Subject NVARCHAR(200) NOT NULL,
		Notes NVARCHAR(2000) NULL,
		CreatedByName NVARCHAR(200) NOT NULL,
		ActivityDateUtc DATETIME2 NOT NULL CONSTRAINT DF_NonRenewalActivity_Date DEFAULT SYSUTCDATETIME(),
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_NonRenewalActivity_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_NonRenewalActivity_IsDeleted DEFAULT 0
	);
	CREATE INDEX IX_Policy_NonRenewalActivity_Parent ON Policy.NonRenewalActivity (NonRenewalId, IsDeleted);
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Seed: statuses per tenant
-- ────────────────────────────────────────────────────────────────────────────
;WITH StatusSeed AS (
	SELECT * FROM (VALUES
		(N'NoticeReceived',     N'Notice Received',      N'Carrier non-renewal notice received and logged.',                        N'#0d6efd', 0, 1, 10),
		(N'UnderReview',        N'Under Review',         N'Reviewing notice validity, reason, and compliance requirements.',        N'#6f42c1', 0, 0, 20),
		(N'InsuredNotification',N'Insured Notification', N'Preparing or sending state-compliant notification to the insured.',      N'#fd7e14', 0, 0, 30),
		(N'InsuredNotified',    N'Insured Notified',     N'Insured notified; proof of notification recorded.',                      N'#20c997', 0, 0, 40),
		(N'Remarketing',        N'Remarketing',          N'Actively remarketing the risk to replacement markets.',                  N'#0dcaf0', 0, 0, 50),
		(N'Replaced',           N'Replaced',             N'Coverage replaced with another carrier before expiration.',              N'#198754', 1, 0, 60),
		(N'Rescinded',          N'Rescinded',            N'Carrier rescinded the non-renewal; policy will renew.',                  N'#198754', 1, 0, 70),
		(N'NonRenewed',         N'Non-Renewed',          N'Policy lapsed at expiration without replacement.',                       N'#dc3545', 1, 0, 80),
		(N'Closed',             N'Closed',               N'Workflow closed.',                                                       N'#6c757d', 1, 0, 90)
	) AS v(StatusCode, StatusName, Description, ColorHex, IsTerminal, IsDefault, SortOrder)
)
INSERT INTO Policy.NonRenewalStatus (TenantId, StatusCode, StatusName, Description, ColorHex, IsTerminal, IsDefault, SortOrder)
SELECT t.TenantId, s.StatusCode, s.StatusName, s.Description, s.ColorHex, s.IsTerminal, s.IsDefault, s.SortOrder
FROM Core.Tenant t
CROSS JOIN StatusSeed s
WHERE t.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Policy.NonRenewalStatus x WHERE x.TenantId = t.TenantId AND x.StatusCode = s.StatusCode);

-- ────────────────────────────────────────────────────────────────────────────
-- Seed: non-renewal reasons per tenant
-- ────────────────────────────────────────────────────────────────────────────
;WITH ReasonSeed AS (
	SELECT * FROM (VALUES
		(N'LossHistory',        N'Adverse Loss History',           N'Frequency or severity of claims exceeds carrier appetite.',       N'Carrier',   1, 10),
		(N'UnderwritingAppetite',N'Change in Underwriting Appetite',N'Carrier exiting the class, program, or line of business.',        N'Carrier',   1, 20),
		(N'MarketWithdrawal',   N'Carrier Market Withdrawal',      N'Carrier withdrawing from the state or territory.',                N'Carrier',   1, 30),
		(N'RiskCondition',      N'Risk Condition / Inspection',    N'Unresolved property or operational conditions from inspection.',  N'Carrier',   1, 40),
		(N'NonCompliance',      N'Underwriting Non-Compliance',    N'Insured failed to satisfy underwriting requirements.',            N'Carrier',   1, 50),
		(N'CatExposure',        N'Catastrophe Exposure',           N'Carrier reducing catastrophe-exposed concentration.',             N'Carrier',   1, 60),
		(N'InsuredRequest',     N'Insured Request',                N'Insured elected not to renew coverage.',                           N'Insured',   0, 70),
		(N'AgencyDecision',     N'Agency Book Decision',           N'Agency electing to move or non-renew the account.',                N'Agency',    1, 80),
		(N'Other',              N'Other',                          N'Other non-renewal reason; document in notes.',                     N'Other',     1, 90)
	) AS v(ReasonCode, ReasonName, Description, CategoryCode, IsRemarketRecommended, SortOrder)
)
INSERT INTO Policy.NonRenewalReason (TenantId, ReasonCode, ReasonName, Description, CategoryCode, IsRemarketRecommended, SortOrder)
SELECT t.TenantId, s.ReasonCode, s.ReasonName, s.Description, s.CategoryCode, s.IsRemarketRecommended, s.SortOrder
FROM Core.Tenant t
CROSS JOIN ReasonSeed s
WHERE t.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Policy.NonRenewalReason x WHERE x.TenantId = t.TenantId AND x.ReasonCode = s.ReasonCode);

-- ────────────────────────────────────────────────────────────────────────────
-- Seed: state notice requirements per tenant (minimum advance notice days).
-- Common baseline values; agencies can adjust per statute updates.
-- ────────────────────────────────────────────────────────────────────────────
;WITH StateSeed AS (
	SELECT * FROM (VALUES
		(N'AL', N'Alabama', 30, 10), (N'AK', N'Alaska', 45, 10), (N'AZ', N'Arizona', 45, 10), (N'AR', N'Arkansas', 60, 10),
		(N'CA', N'California', 75, 10), (N'CO', N'Colorado', 45, 10), (N'CT', N'Connecticut', 60, 10), (N'DE', N'Delaware', 60, 10),
		(N'DC', N'District of Columbia', 30, 10), (N'FL', N'Florida', 120, 10), (N'GA', N'Georgia', 45, 10), (N'HI', N'Hawaii', 30, 10),
		(N'ID', N'Idaho', 45, 10), (N'IL', N'Illinois', 60, 10), (N'IN', N'Indiana', 45, 10), (N'IA', N'Iowa', 45, 10),
		(N'KS', N'Kansas', 60, 10), (N'KY', N'Kentucky', 75, 10), (N'LA', N'Louisiana', 60, 10), (N'ME', N'Maine', 30, 10),
		(N'MD', N'Maryland', 45, 10), (N'MA', N'Massachusetts', 45, 10), (N'MI', N'Michigan', 30, 10), (N'MN', N'Minnesota', 60, 10),
		(N'MS', N'Mississippi', 30, 10), (N'MO', N'Missouri', 60, 10), (N'MT', N'Montana', 45, 10), (N'NE', N'Nebraska', 60, 10),
		(N'NV', N'Nevada', 60, 10), (N'NH', N'New Hampshire', 60, 10), (N'NJ', N'New Jersey', 60, 10), (N'NM', N'New Mexico', 30, 10),
		(N'NY', N'New York', 60, 10), (N'NC', N'North Carolina', 60, 10), (N'ND', N'North Dakota', 60, 10), (N'OH', N'Ohio', 30, 10),
		(N'OK', N'Oklahoma', 45, 10), (N'OR', N'Oregon', 45, 10), (N'PA', N'Pennsylvania', 60, 10), (N'RI', N'Rhode Island', 60, 10),
		(N'SC', N'South Carolina', 60, 10), (N'SD', N'South Dakota', 60, 10), (N'TN', N'Tennessee', 60, 10), (N'TX', N'Texas', 60, 10),
		(N'UT', N'Utah', 30, 10), (N'VT', N'Vermont', 45, 10), (N'VA', N'Virginia', 45, 10), (N'WA', N'Washington', 45, 10),
		(N'WV', N'West Virginia', 45, 10), (N'WI', N'Wisconsin', 60, 10), (N'WY', N'Wyoming', 45, 10)
	) AS v(StateCode, StateName, MinimumNoticeDays, InsuredNoticeDays)
)
INSERT INTO Policy.NonRenewalStateRequirement (TenantId, StateCode, StateName, LineCategoryCode, MinimumNoticeDays, InsuredNoticeDays, Notes)
SELECT t.TenantId, s.StateCode, s.StateName, N'All', s.MinimumNoticeDays, s.InsuredNoticeDays,
	   N'Baseline statutory minimum advance notice before expiration; verify current statute for line-specific rules.'
FROM Core.Tenant t
CROSS JOIN StateSeed s
WHERE t.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Policy.NonRenewalStateRequirement x WHERE x.TenantId = t.TenantId AND x.StateCode = s.StateCode AND x.LineCategoryCode = N'All');
