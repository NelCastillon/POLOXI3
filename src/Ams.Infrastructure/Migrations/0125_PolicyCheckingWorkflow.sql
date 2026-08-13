-- 0125_PolicyCheckingWorkflow.sql
-- Enterprise Policy Checking: verify carrier-issued policy documents against bound quote/policy terms.
-- Creates lookup tables (statuses, check item definitions, discrepancy types), workflow tables
-- (PolicyCheck header, PolicyCheckItem line comparisons, PolicyCheckDiscrepancy, PolicyCheckActivity),
-- seeds per-tenant configuration data, and backfills checks from existing bound policies. Idempotent.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Policy') EXEC(N'CREATE SCHEMA Policy');

-- ────────────────────────────────────────────────────────────────────────────
-- Lookup: Policy Check Status (per tenant, DB-backed dropdown source)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.PolicyCheckStatus', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCheckStatus
	(
		PolicyCheckStatusId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_PolicyCheckStatus PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		StatusName NVARCHAR(100) NOT NULL,
		Description NVARCHAR(400) NULL,
		ColorHex NVARCHAR(10) NULL,
		IsTerminal BIT NOT NULL CONSTRAINT DF_PolicyCheckStatus_IsTerminal DEFAULT 0,
		IsDefault BIT NOT NULL CONSTRAINT DF_PolicyCheckStatus_IsDefault DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_PolicyCheckStatus_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_PolicyCheckStatus_SortOrder DEFAULT 0,
		TenantIdCreatedBy UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCheckStatus_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCheckStatus_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_PolicyCheckStatus_Tenant_Code UNIQUE (TenantId, StatusCode)
	);
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Lookup: Policy Check Item Definitions (the checklist template, per tenant)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.PolicyCheckItemDefinition', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCheckItemDefinition
	(
		PolicyCheckItemDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_PolicyCheckItemDefinition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ItemCode NVARCHAR(60) NOT NULL,
		ItemName NVARCHAR(150) NOT NULL,
		CategoryCode NVARCHAR(50) NOT NULL,
		CategoryName NVARCHAR(100) NOT NULL,
		Description NVARCHAR(400) NULL,
		DefaultSeverityCode NVARCHAR(30) NOT NULL CONSTRAINT DF_PolicyCheckItemDef_Severity DEFAULT N'Major',
		IsRequired BIT NOT NULL CONSTRAINT DF_PolicyCheckItemDef_IsRequired DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_PolicyCheckItemDef_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_PolicyCheckItemDef_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCheckItemDef_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCheckItemDef_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_PolicyCheckItemDef_Tenant_Code UNIQUE (TenantId, ItemCode)
	);
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Lookup: Discrepancy Types (per tenant)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.PolicyCheckDiscrepancyType', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCheckDiscrepancyType
	(
		PolicyCheckDiscrepancyTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_PolicyCheckDiscrepancyType PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		TypeCode NVARCHAR(50) NOT NULL,
		TypeName NVARCHAR(120) NOT NULL,
		Description NVARCHAR(400) NULL,
		DefaultSeverityCode NVARCHAR(30) NOT NULL CONSTRAINT DF_PolicyCheckDiscType_Severity DEFAULT N'Major',
		RequiresCarrierNotification BIT NOT NULL CONSTRAINT DF_PolicyCheckDiscType_Notify DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_PolicyCheckDiscType_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_PolicyCheckDiscType_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCheckDiscType_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCheckDiscType_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_PolicyCheckDiscType_Tenant_Code UNIQUE (TenantId, TypeCode)
	);
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Workflow: Policy Check header
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.PolicyCheck', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCheck
	(
		PolicyCheckId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_PolicyCheck PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NULL,
		AccountId UNIQUEIDENTIFIER NULL,
		QuoteId UNIQUEIDENTIFIER NULL,
		CheckNumber NVARCHAR(50) NOT NULL,
		PolicyNumber NVARCHAR(120) NOT NULL,
		AccountName NVARCHAR(200) NOT NULL,
		CarrierName NVARCHAR(200) NULL,
		LineOfBusiness NVARCHAR(120) NULL,
		PolicyEffectiveDate DATE NULL,
		PolicyExpirationDate DATE NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyCheck_Status DEFAULT N'Pending',
		PriorityCode NVARCHAR(30) NOT NULL CONSTRAINT DF_PolicyCheck_Priority DEFAULT N'Normal',
		CheckTypeCode NVARCHAR(40) NOT NULL CONSTRAINT DF_PolicyCheck_Type DEFAULT N'NewBusiness',
		AssignedToUserId UNIQUEIDENTIFIER NULL,
		AssignedToName NVARCHAR(200) NULL,
		DueDate DATE NULL,
		ReceivedDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		CompletedByName NVARCHAR(200) NULL,
		ItemsTotal INT NOT NULL CONSTRAINT DF_PolicyCheck_ItemsTotal DEFAULT 0,
		ItemsMatched INT NOT NULL CONSTRAINT DF_PolicyCheck_ItemsMatched DEFAULT 0,
		ItemsDiscrepant INT NOT NULL CONSTRAINT DF_PolicyCheck_ItemsDiscrepant DEFAULT 0,
		ResultSummary NVARCHAR(1000) NULL,
		Notes NVARCHAR(2000) NULL,
		IsUrgent BIT NOT NULL CONSTRAINT DF_PolicyCheck_IsUrgent DEFAULT 0,
		IsArchived BIT NOT NULL CONSTRAINT DF_PolicyCheck_IsArchived DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCheck_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCheck_IsDeleted DEFAULT 0
	);
	CREATE INDEX IX_PolicyCheck_Tenant_Status ON Policy.PolicyCheck (TenantId, StatusCode) WHERE IsDeleted = 0;
	CREATE INDEX IX_PolicyCheck_Policy ON Policy.PolicyCheck (PolicyId) WHERE IsDeleted = 0;
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Workflow: Policy Check Items (discrete expected-vs-actual comparisons)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.PolicyCheckItem', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCheckItem
	(
		PolicyCheckItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_PolicyCheckItem PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyCheckId UNIQUEIDENTIFIER NOT NULL,
		PolicyCheckItemDefinitionId UNIQUEIDENTIFIER NULL,
		ItemCode NVARCHAR(60) NOT NULL,
		ItemName NVARCHAR(150) NOT NULL,
		CategoryName NVARCHAR(100) NOT NULL,
		ExpectedValue NVARCHAR(500) NULL,
		ActualValue NVARCHAR(500) NULL,
		MatchStatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_PolicyCheckItem_Match DEFAULT N'Unchecked',
		SeverityCode NVARCHAR(30) NOT NULL CONSTRAINT DF_PolicyCheckItem_Severity DEFAULT N'Major',
		IsRequired BIT NOT NULL CONSTRAINT DF_PolicyCheckItem_IsRequired DEFAULT 1,
		Notes NVARCHAR(1000) NULL,
		CheckedByName NVARCHAR(200) NULL,
		CheckedDateUtc DATETIME2 NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_PolicyCheckItem_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCheckItem_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCheckItem_IsDeleted DEFAULT 0
	);
	CREATE INDEX IX_PolicyCheckItem_Check ON Policy.PolicyCheckItem (PolicyCheckId) WHERE IsDeleted = 0;
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Workflow: Discrepancies (tracked to resolution)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.PolicyCheckDiscrepancy', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCheckDiscrepancy
	(
		PolicyCheckDiscrepancyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_PolicyCheckDiscrepancy PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyCheckId UNIQUEIDENTIFIER NOT NULL,
		PolicyCheckItemId UNIQUEIDENTIFIER NULL,
		TypeCode NVARCHAR(50) NOT NULL,
		TypeName NVARCHAR(120) NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL CONSTRAINT DF_PolicyCheckDisc_Severity DEFAULT N'Major',
		StatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_PolicyCheckDisc_Status DEFAULT N'Open',
		Description NVARCHAR(1000) NOT NULL,
		CarrierNotified BIT NOT NULL CONSTRAINT DF_PolicyCheckDisc_Notified DEFAULT 0,
		CarrierNotifiedDateUtc DATETIME2 NULL,
		CarrierReferenceNumber NVARCHAR(100) NULL,
		ResolutionNotes NVARCHAR(1000) NULL,
		ResolvedByName NVARCHAR(200) NULL,
		ResolvedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCheckDisc_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCheckDisc_IsDeleted DEFAULT 0
	);
	CREATE INDEX IX_PolicyCheckDisc_Check ON Policy.PolicyCheckDiscrepancy (PolicyCheckId) WHERE IsDeleted = 0;
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Workflow: Activity / audit trail
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Policy.PolicyCheckActivity', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyCheckActivity
	(
		ActivityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Policy_PolicyCheckActivity PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyCheckId UNIQUEIDENTIFIER NOT NULL,
		ActivityType NVARCHAR(50) NOT NULL,
		Subject NVARCHAR(200) NOT NULL,
		Notes NVARCHAR(2000) NULL,
		CreatedByName NVARCHAR(200) NOT NULL,
		ActivityDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCheckActivity_Date DEFAULT SYSUTCDATETIME(),
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCheckActivity_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCheckActivity_IsDeleted DEFAULT 0
	);
	CREATE INDEX IX_PolicyCheckActivity_Check ON Policy.PolicyCheckActivity (PolicyCheckId) WHERE IsDeleted = 0;
END;

-- ────────────────────────────────────────────────────────────────────────────
-- Seed: statuses per tenant
-- ────────────────────────────────────────────────────────────────────────────
;WITH StatusSeed AS (
	SELECT * FROM (VALUES
		(N'Pending',            N'Pending',              N'Issued policy received; check not started.',          N'#6c757d', 0, 1, 10),
		(N'InProgress',         N'In Progress',          N'Checker is comparing issued policy against bound terms.', N'#0d6efd', 0, 0, 20),
		(N'Passed',             N'Passed',               N'All items match bound terms.',                          N'#198754', 1, 0, 30),
		(N'PassedWithNotes',    N'Passed with Notes',    N'Minor variances documented; no carrier action needed.', N'#20c997', 1, 0, 40),
		(N'DiscrepanciesFound', N'Discrepancies Found',  N'One or more items do not match; discrepancies logged.', N'#dc3545', 0, 0, 50),
		(N'SentToCarrier',      N'Sent to Carrier',      N'Discrepancies reported to carrier for correction.',     N'#fd7e14', 0, 0, 60),
		(N'Resolved',           N'Resolved',             N'Carrier issued corrections; verified against request.', N'#198754', 0, 0, 70),
		(N'Closed',             N'Closed',               N'Check complete and filed.',                             N'#495057', 1, 0, 80)
	) AS v(StatusCode, StatusName, Description, ColorHex, IsTerminal, IsDefault, SortOrder)
)
INSERT INTO Policy.PolicyCheckStatus (TenantId, StatusCode, StatusName, Description, ColorHex, IsTerminal, IsDefault, SortOrder)
SELECT t.TenantId, s.StatusCode, s.StatusName, s.Description, s.ColorHex, s.IsTerminal, s.IsDefault, s.SortOrder
FROM Core.Tenant t
CROSS JOIN StatusSeed s
WHERE t.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Policy.PolicyCheckStatus x WHERE x.TenantId = t.TenantId AND x.StatusCode = s.StatusCode);

-- ────────────────────────────────────────────────────────────────────────────
-- Seed: check item definitions per tenant
-- ────────────────────────────────────────────────────────────────────────────
;WITH ItemSeed AS (
	SELECT * FROM (VALUES
		(N'NAMED_INSURED',       N'Named Insured',              N'INSURED',   N'Insured Information',  N'Legal name and DBA match the application and bound quote.',   N'Critical', 1, 10),
		(N'MAILING_ADDRESS',     N'Mailing Address',            N'INSURED',   N'Insured Information',  N'Mailing address matches the account record.',                  N'Minor',    1, 20),
		(N'POLICY_NUMBER',       N'Policy Number',              N'POLICY',    N'Policy Terms',         N'Policy number matches binder/carrier confirmation.',           N'Critical', 1, 30),
		(N'EFFECTIVE_DATE',      N'Effective Date',             N'POLICY',    N'Policy Terms',         N'Effective date matches the bound quote and binder.',           N'Critical', 1, 40),
		(N'EXPIRATION_DATE',     N'Expiration Date',            N'POLICY',    N'Policy Terms',         N'Expiration date matches the bound term.',                      N'Critical', 1, 50),
		(N'CARRIER_WRITING_CO',  N'Carrier / Writing Company',  N'POLICY',    N'Policy Terms',         N'Issuing carrier and writing company match the bound market.',  N'Critical', 1, 60),
		(N'PREMIUM',             N'Premium',                    N'FINANCIAL', N'Financial',            N'Written premium matches the bound quote premium.',             N'Critical', 1, 70),
		(N'TAXES_FEES',          N'Taxes, Fees & Surcharges',   N'FINANCIAL', N'Financial',            N'Taxes/fees/surcharges match the quote and invoice.',           N'Major',    1, 80),
		(N'COMMISSION',          N'Commission',                 N'FINANCIAL', N'Financial',            N'Commission rate matches the carrier agreement and quote.',     N'Major',    1, 90),
		(N'BILLING_PLAN',        N'Billing Plan',               N'FINANCIAL', N'Financial',            N'Billing type/payment plan matches what was requested.',        N'Major',    1, 100),
		(N'LIMITS',              N'Limits of Liability',        N'COVERAGE',  N'Coverage',             N'Each coverage limit matches the bound quote.',                 N'Critical', 1, 110),
		(N'DEDUCTIBLES',         N'Deductibles / Retentions',   N'COVERAGE',  N'Coverage',             N'Deductibles match the bound quote.',                           N'Critical', 1, 120),
		(N'COVERAGE_FORMS',      N'Coverage Forms',             N'COVERAGE',  N'Coverage',             N'Forms schedule matches quoted forms and editions.',            N'Major',    1, 130),
		(N'ENDORSEMENTS',        N'Endorsements Attached',      N'COVERAGE',  N'Coverage',             N'All requested/quoted endorsements are attached.',              N'Major',    1, 140),
		(N'EXCLUSIONS',          N'Exclusions',                 N'COVERAGE',  N'Coverage',             N'No unexpected exclusions beyond those quoted.',                N'Critical', 1, 150),
		(N'ADDITIONAL_INTERESTS',N'Additional Interests',       N'INTERESTS', N'Interests & Schedules',N'Additional insureds, mortgagees, loss payees listed correctly.', N'Major',  1, 160),
		(N'SCHEDULED_ITEMS',     N'Scheduled Items',            N'INTERESTS', N'Interests & Schedules',N'Vehicles, drivers, locations, property schedules match.',      N'Major',    1, 170),
		(N'SUBJECTIVITIES',      N'Subjectivities Cleared',     N'BINDING',   N'Binding',              N'All bind subjectivities satisfied or carried as conditions.',  N'Major',    1, 180)
	) AS v(ItemCode, ItemName, CategoryCode, CategoryName, Description, DefaultSeverityCode, IsRequired, SortOrder)
)
INSERT INTO Policy.PolicyCheckItemDefinition (TenantId, ItemCode, ItemName, CategoryCode, CategoryName, Description, DefaultSeverityCode, IsRequired, SortOrder)
SELECT t.TenantId, s.ItemCode, s.ItemName, s.CategoryCode, s.CategoryName, s.Description, s.DefaultSeverityCode, s.IsRequired, s.SortOrder
FROM Core.Tenant t
CROSS JOIN ItemSeed s
WHERE t.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Policy.PolicyCheckItemDefinition x WHERE x.TenantId = t.TenantId AND x.ItemCode = s.ItemCode);

-- ────────────────────────────────────────────────────────────────────────────
-- Seed: discrepancy types per tenant
-- ────────────────────────────────────────────────────────────────────────────
;WITH DiscSeed AS (
	SELECT * FROM (VALUES
		(N'WrongNamedInsured',  N'Incorrect Named Insured',      N'Issued policy shows a different or misspelled named insured.',  N'Critical', 1, 10),
		(N'WrongDates',         N'Incorrect Effective/Expiration Dates', N'Policy term dates differ from bound terms.',            N'Critical', 1, 20),
		(N'PremiumVariance',    N'Premium Variance',             N'Issued premium differs from bound quote premium.',              N'Critical', 1, 30),
		(N'LimitVariance',      N'Limit Variance',               N'One or more limits differ from bound quote.',                   N'Critical', 1, 40),
		(N'DeductibleVariance', N'Deductible Variance',          N'One or more deductibles differ from bound quote.',              N'Critical', 1, 50),
		(N'MissingEndorsement', N'Missing Endorsement/Form',     N'A quoted or requested form/endorsement is not attached.',       N'Major',    1, 60),
		(N'UnexpectedExclusion',N'Unexpected Exclusion',         N'An exclusion appears that was not part of the quote.',          N'Critical', 1, 70),
		(N'MissingInterest',    N'Missing Additional Interest',  N'An additional insured/mortgagee/loss payee is missing or wrong.', N'Major',  1, 80),
		(N'ScheduleError',      N'Schedule Error',               N'Vehicle/driver/location/property schedule mismatch.',           N'Major',    1, 90),
		(N'CommissionError',    N'Commission Error',             N'Commission rate differs from agreement or quote.',              N'Major',    1, 100),
		(N'BillingError',       N'Billing Error',                N'Billing plan or payment schedule differs from request.',        N'Minor',    0, 110),
		(N'TypoClerical',       N'Typographical / Clerical',     N'Clerical error not affecting coverage.',                        N'Minor',    0, 120)
	) AS v(TypeCode, TypeName, Description, DefaultSeverityCode, RequiresCarrierNotification, SortOrder)
)
INSERT INTO Policy.PolicyCheckDiscrepancyType (TenantId, TypeCode, TypeName, Description, DefaultSeverityCode, RequiresCarrierNotification, SortOrder)
SELECT t.TenantId, s.TypeCode, s.TypeName, s.Description, s.DefaultSeverityCode, s.RequiresCarrierNotification, s.SortOrder
FROM Core.Tenant t
CROSS JOIN DiscSeed s
WHERE t.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Policy.PolicyCheckDiscrepancyType x WHERE x.TenantId = t.TenantId AND x.TypeCode = s.TypeCode);

-- ────────────────────────────────────────────────────────────────────────────
-- Backfill: create checks for existing bound policies that have none yet.
-- Expected values are sourced from the bound quote (Submissions.Quote) and
-- the bound policy record itself; status synchronized with VerificationStatusCode.
-- ────────────────────────────────────────────────────────────────────────────
;WITH Numbered AS (
	SELECT bp.PolicyId, bp.TenantId, bp.AccountId, bp.QuoteId, bp.PolicyNumber, bp.CarrierId,
		   bp.LineOfBusiness, bp.EffectiveDate, bp.ExpirationDate, bp.VerificationStatusCode,
		   ROW_NUMBER() OVER (PARTITION BY bp.TenantId ORDER BY bp.BoundDateUtc, bp.PolicyId) AS Rn
	FROM Submissions.BoundPolicy bp
	WHERE bp.IsDeleted = 0
	  AND NOT EXISTS (SELECT 1 FROM Policy.PolicyCheck pc WHERE pc.PolicyId = bp.PolicyId AND pc.IsDeleted = 0)
)
INSERT INTO Policy.PolicyCheck
(PolicyCheckId, TenantId, PolicyId, AccountId, QuoteId, CheckNumber, PolicyNumber, AccountName, CarrierName,
 LineOfBusiness, PolicyEffectiveDate, PolicyExpirationDate, StatusCode, PriorityCode, CheckTypeCode,
 DueDate, ReceivedDateUtc, CompletedDateUtc, ItemsTotal, ItemsMatched, ItemsDiscrepant, Notes)
SELECT NEWID(), n.TenantId, n.PolicyId, n.AccountId, n.QuoteId,
	   CONCAT(N'CHK-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(n.Rn + ISNULL((SELECT COUNT(1) FROM Policy.PolicyCheck pc2 WHERE pc2.TenantId = n.TenantId), 0), N'0000')),
	   ISNULL(n.PolicyNumber, N''),
	   ISNULL(a.AccountName, N'Unknown Account'),
	   c.CarrierName,
	   n.LineOfBusiness,
	   n.EffectiveDate, n.ExpirationDate,
	   CASE WHEN n.VerificationStatusCode = N'Verified' THEN N'Passed' ELSE N'Pending' END,
	   N'Normal', N'NewBusiness',
	   DATEADD(day, 10, CAST(SYSUTCDATETIME() AS date)),
	   SYSUTCDATETIME(),
	   CASE WHEN n.VerificationStatusCode = N'Verified' THEN SYSUTCDATETIME() ELSE NULL END,
	   0, 0, 0,
	   N'Auto-created from existing bound policy during Policy Checking rollout.'
FROM Numbered n
LEFT JOIN Client.Account a ON a.AccountId = n.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = n.CarrierId;

-- Populate checklist items for checks that have no items yet, from tenant definitions,
-- with expected values pulled from the bound quote / policy record where derivable.
INSERT INTO Policy.PolicyCheckItem
(PolicyCheckItemId, TenantId, PolicyCheckId, PolicyCheckItemDefinitionId, ItemCode, ItemName, CategoryName,
 ExpectedValue, ActualValue, MatchStatusCode, SeverityCode, IsRequired, SortOrder)
SELECT NEWID(), pc.TenantId, pc.PolicyCheckId, d.PolicyCheckItemDefinitionId, d.ItemCode, d.ItemName, d.CategoryName,
	   CASE d.ItemCode
		   WHEN N'NAMED_INSURED'  THEN pc.AccountName
		   WHEN N'POLICY_NUMBER'  THEN pc.PolicyNumber
		   WHEN N'EFFECTIVE_DATE' THEN CONVERT(NVARCHAR(10), pc.PolicyEffectiveDate, 120)
		   WHEN N'EXPIRATION_DATE' THEN CONVERT(NVARCHAR(10), pc.PolicyExpirationDate, 120)
		   WHEN N'CARRIER_WRITING_CO' THEN pc.CarrierName
		   WHEN N'PREMIUM'        THEN CONVERT(NVARCHAR(30), q.AnnualPremium)
		   WHEN N'COMMISSION'     THEN CONVERT(NVARCHAR(30), q.CommissionPercent)
		   WHEN N'LIMITS'         THEN CONVERT(NVARCHAR(50), q.[Limit])
		   WHEN N'DEDUCTIBLES'    THEN CONVERT(NVARCHAR(50), q.Deductible)
		   WHEN N'BILLING_PLAN'   THEN q.PaymentTerms
		   WHEN N'SUBJECTIVITIES' THEN q.Subjectivities
		   ELSE NULL
	   END,
	   NULL,
	   CASE WHEN pc.StatusCode = N'Passed' THEN N'Match' ELSE N'Unchecked' END,
	   d.DefaultSeverityCode, d.IsRequired, d.SortOrder
FROM Policy.PolicyCheck pc
JOIN Policy.PolicyCheckItemDefinition d ON d.TenantId = pc.TenantId AND d.IsActive = 1 AND d.IsDeleted = 0
LEFT JOIN Submissions.Quote q ON q.QuoteId = pc.QuoteId
WHERE pc.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Policy.PolicyCheckItem i WHERE i.PolicyCheckId = pc.PolicyCheckId AND i.IsDeleted = 0);

-- Sync header item counters
UPDATE pc SET
	ItemsTotal = agg.Total,
	ItemsMatched = agg.Matched,
	ItemsDiscrepant = agg.Discrepant
FROM Policy.PolicyCheck pc
JOIN (
	SELECT PolicyCheckId,
		   COUNT(1) AS Total,
		   SUM(CASE WHEN MatchStatusCode = N'Match' THEN 1 ELSE 0 END) AS Matched,
		   SUM(CASE WHEN MatchStatusCode = N'Discrepancy' THEN 1 ELSE 0 END) AS Discrepant
	FROM Policy.PolicyCheckItem WHERE IsDeleted = 0 GROUP BY PolicyCheckId
) agg ON agg.PolicyCheckId = pc.PolicyCheckId
WHERE pc.IsDeleted = 0;

-- Log rollout activity for backfilled checks with no activity yet
INSERT INTO Policy.PolicyCheckActivity (ActivityId, TenantId, PolicyCheckId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc)
SELECT NEWID(), pc.TenantId, pc.PolicyCheckId, N'Created', N'Policy check created',
	   N'Check auto-created from bound policy ' + pc.PolicyNumber + N' during Policy Checking rollout.',
	   N'System', SYSUTCDATETIME()
FROM Policy.PolicyCheck pc
WHERE pc.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Policy.PolicyCheckActivity a WHERE a.PolicyCheckId = pc.PolicyCheckId AND a.IsDeleted = 0);
