IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Agency') EXEC(N'CREATE SCHEMA Agency');

IF OBJECT_ID(N'Agency.CarrierRuleOption', N'U') IS NULL
BEGIN
	CREATE TABLE Agency.CarrierRuleOption
	(
		CarrierRuleOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_CarrierRuleOption PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		OptionType NVARCHAR(80) NOT NULL,
		OptionCode NVARCHAR(80) NOT NULL,
		DisplayName NVARCHAR(160) NOT NULL,
		OptionValue NVARCHAR(240) NOT NULL,
		Description NVARCHAR(500) NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_CarrierRuleOption_Sort_0076 DEFAULT 100,
		IsActive BIT NOT NULL CONSTRAINT DF_CarrierRuleOption_Active_0076 DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierRuleOption_Created_0076 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierRuleOption_Deleted_0076 DEFAULT 0
	);
END;

IF OBJECT_ID(N'Agency.CarrierProductCatalog', N'U') IS NULL
BEGIN
	CREATE TABLE Agency.CarrierProductCatalog
	(
		CarrierProductCatalogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_CarrierProductCatalog PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		LineOfBusinessId UNIQUEIDENTIFIER NULL,
		ProductCode NVARCHAR(80) NOT NULL,
		ProductName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(500) NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_CarrierProductCatalog_Sort_0076 DEFAULT 100,
		IsActive BIT NOT NULL CONSTRAINT DF_CarrierProductCatalog_Active_0076 DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierProductCatalog_Created_0076 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierProductCatalog_Deleted_0076 DEFAULT 0
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierRuleOption') AND name = N'UX_CarrierRuleOption_Tenant_Type_Code_0076')
	CREATE UNIQUE INDEX UX_CarrierRuleOption_Tenant_Type_Code_0076 ON Agency.CarrierRuleOption(TenantId, OptionType, OptionCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierRuleOption') AND name = N'IX_CarrierRuleOption_Lookup_0076')
	CREATE INDEX IX_CarrierRuleOption_Lookup_0076 ON Agency.CarrierRuleOption(TenantId, OptionType, IsDeleted, IsActive, SortOrder, DisplayName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierProductCatalog') AND name = N'UX_CarrierProductCatalog_Tenant_Code_0076')
	CREATE UNIQUE INDEX UX_CarrierProductCatalog_Tenant_Code_0076 ON Agency.CarrierProductCatalog(TenantId, ProductCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierProductCatalog') AND name = N'IX_CarrierProductCatalog_Scope_0076')
	CREATE INDEX IX_CarrierProductCatalog_Scope_0076 ON Agency.CarrierProductCatalog(TenantId, CarrierId, LineOfBusinessId, IsDeleted, IsActive, SortOrder, ProductName);

DECLARE @Tenants0076 TABLE (TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
	INSERT INTO @Tenants0076 (TenantId) SELECT TenantId FROM Core.Tenant WHERE ISNULL(IsDeleted, 0) = 0;
IF NOT EXISTS (SELECT 1 FROM @Tenants0076) AND OBJECT_ID(N'Agency.CarrierProductRule', N'U') IS NOT NULL
	INSERT INTO @Tenants0076 (TenantId) SELECT DISTINCT TenantId FROM Agency.CarrierProductRule WHERE TenantId IS NOT NULL;

DECLARE @Options0076 TABLE (OptionType NVARCHAR(80), OptionCode NVARCHAR(80), DisplayName NVARCHAR(160), OptionValue NVARCHAR(240), Description NVARCHAR(500), SortOrder INT);
INSERT INTO @Options0076 VALUES
(N'BillingType',N'AGENCY_BILL',N'Agency Bill',N'Agency Bill',N'Agency collects premium and remits to the carrier.',10),
(N'BillingType',N'DIRECT_BILL',N'Direct Bill',N'Direct Bill',N'Carrier bills the insured directly.',20),
(N'BillingType',N'PREMIUM_FINANCE',N'Premium Finance',N'Premium Finance',N'Premium is funded through an approved finance arrangement.',30),
(N'BillingType',N'PAY_IN_FULL',N'Pay in Full',N'Pay in Full',N'Full premium is due according to the carrier rule.',40),
(N'CommissionPaymentMethod',N'ACH',N'ACH',N'ACH',N'Automated Clearing House payment.',10),
(N'CommissionPaymentMethod',N'EFT',N'Electronic Funds Transfer',N'EFT',N'Electronic funds transfer payment.',20),
(N'CommissionPaymentMethod',N'DIRECT_DEPOSIT',N'Direct Deposit',N'Direct Deposit',N'Direct deposit to the configured account.',30),
(N'CommissionPaymentMethod',N'CHECK',N'Check',N'Check',N'Paper check payment.',40),
(N'CommissionPaymentMethod',N'STATEMENT_OFFSET',N'Statement Offset',N'Statement Offset',N'Commission is offset against the carrier statement.',50),
(N'BindingCutoff',N'1200',N'12:00 PM',N'12:00',N'Noon local carrier cutoff.',10),
(N'BindingCutoff',N'1500',N'3:00 PM',N'15:00',N'3:00 PM local carrier cutoff.',20),
(N'BindingCutoff',N'1600',N'4:00 PM',N'16:00',N'4:00 PM local carrier cutoff.',30),
(N'BindingCutoff',N'1700',N'5:00 PM',N'17:00',N'5:00 PM local carrier cutoff.',40),
(N'BindingCutoff',N'1800',N'6:00 PM',N'18:00',N'6:00 PM local carrier cutoff.',50),
(N'Jurisdiction',N'AL',N'Alabama',N'AL',NULL,10),(N'Jurisdiction',N'AK',N'Alaska',N'AK',NULL,20),(N'Jurisdiction',N'AZ',N'Arizona',N'AZ',NULL,30),(N'Jurisdiction',N'AR',N'Arkansas',N'AR',NULL,40),(N'Jurisdiction',N'CA',N'California',N'CA',NULL,50),
(N'Jurisdiction',N'CO',N'Colorado',N'CO',NULL,60),(N'Jurisdiction',N'CT',N'Connecticut',N'CT',NULL,70),(N'Jurisdiction',N'DE',N'Delaware',N'DE',NULL,80),(N'Jurisdiction',N'FL',N'Florida',N'FL',NULL,90),(N'Jurisdiction',N'GA',N'Georgia',N'GA',NULL,100),
(N'Jurisdiction',N'HI',N'Hawaii',N'HI',NULL,110),(N'Jurisdiction',N'ID',N'Idaho',N'ID',NULL,120),(N'Jurisdiction',N'IL',N'Illinois',N'IL',NULL,130),(N'Jurisdiction',N'IN',N'Indiana',N'IN',NULL,140),(N'Jurisdiction',N'IA',N'Iowa',N'IA',NULL,150),
(N'Jurisdiction',N'KS',N'Kansas',N'KS',NULL,160),(N'Jurisdiction',N'KY',N'Kentucky',N'KY',NULL,170),(N'Jurisdiction',N'LA',N'Louisiana',N'LA',NULL,180),(N'Jurisdiction',N'ME',N'Maine',N'ME',NULL,190),(N'Jurisdiction',N'MD',N'Maryland',N'MD',NULL,200),
(N'Jurisdiction',N'MA',N'Massachusetts',N'MA',NULL,210),(N'Jurisdiction',N'MI',N'Michigan',N'MI',NULL,220),(N'Jurisdiction',N'MN',N'Minnesota',N'MN',NULL,230),(N'Jurisdiction',N'MS',N'Mississippi',N'MS',NULL,240),(N'Jurisdiction',N'MO',N'Missouri',N'MO',NULL,250),
(N'Jurisdiction',N'MT',N'Montana',N'MT',NULL,260),(N'Jurisdiction',N'NE',N'Nebraska',N'NE',NULL,270),(N'Jurisdiction',N'NV',N'Nevada',N'NV',NULL,280),(N'Jurisdiction',N'NH',N'New Hampshire',N'NH',NULL,290),(N'Jurisdiction',N'NJ',N'New Jersey',N'NJ',NULL,300),
(N'Jurisdiction',N'NM',N'New Mexico',N'NM',NULL,310),(N'Jurisdiction',N'NY',N'New York',N'NY',NULL,320),(N'Jurisdiction',N'NC',N'North Carolina',N'NC',NULL,330),(N'Jurisdiction',N'ND',N'North Dakota',N'ND',NULL,340),(N'Jurisdiction',N'OH',N'Ohio',N'OH',NULL,350),
(N'Jurisdiction',N'OK',N'Oklahoma',N'OK',NULL,360),(N'Jurisdiction',N'OR',N'Oregon',N'OR',NULL,370),(N'Jurisdiction',N'PA',N'Pennsylvania',N'PA',NULL,380),(N'Jurisdiction',N'RI',N'Rhode Island',N'RI',NULL,390),(N'Jurisdiction',N'SC',N'South Carolina',N'SC',NULL,400),
(N'Jurisdiction',N'SD',N'South Dakota',N'SD',NULL,410),(N'Jurisdiction',N'TN',N'Tennessee',N'TN',NULL,420),(N'Jurisdiction',N'TX',N'Texas',N'TX',NULL,430),(N'Jurisdiction',N'UT',N'Utah',N'UT',NULL,440),(N'Jurisdiction',N'VT',N'Vermont',N'VT',NULL,450),
(N'Jurisdiction',N'VA',N'Virginia',N'VA',NULL,460),(N'Jurisdiction',N'WA',N'Washington',N'WA',NULL,470),(N'Jurisdiction',N'WV',N'West Virginia',N'WV',NULL,480),(N'Jurisdiction',N'WI',N'Wisconsin',N'WI',NULL,490),(N'Jurisdiction',N'WY',N'Wyoming',N'WY',NULL,500),
(N'Jurisdiction',N'DC',N'District of Columbia',N'DC',NULL,510);

INSERT INTO Agency.CarrierRuleOption (CarrierRuleOptionId,TenantId,OptionType,OptionCode,DisplayName,OptionValue,Description,SortOrder,IsActive,CreatedDateUtc,IsDeleted)
SELECT NEWID(), t.TenantId, o.OptionType, o.OptionCode, o.DisplayName, o.OptionValue, o.Description, o.SortOrder, 1, SYSUTCDATETIME(), 0
FROM @Tenants0076 t CROSS JOIN @Options0076 o
WHERE NOT EXISTS (SELECT 1 FROM Agency.CarrierRuleOption x WHERE x.TenantId=t.TenantId AND x.OptionType=o.OptionType AND x.OptionCode=o.OptionCode AND x.IsDeleted=0);

IF OBJECT_ID(N'Agency.CarrierProductRule', N'U') IS NOT NULL
BEGIN
	INSERT INTO Agency.CarrierProductCatalog (CarrierProductCatalogId,TenantId,CarrierId,LineOfBusinessId,ProductCode,ProductName,Description,SortOrder,IsActive,CreatedDateUtc,IsDeleted)
	SELECT NEWID(), r.TenantId, r.CarrierId, r.LineOfBusinessId,
		   COALESCE(NULLIF(r.CarrierProductCode,N''), LEFT(REPLACE(UPPER(r.CarrierProductName),N' ',N'_'),80)),
		   r.CarrierProductName, N'Synchronized from existing carrier product rules.', 100, 1, SYSUTCDATETIME(), 0
	FROM Agency.CarrierProductRule r
	WHERE r.IsDeleted=0 AND NULLIF(r.CarrierProductName,N'') IS NOT NULL
	  AND NOT EXISTS (SELECT 1 FROM Agency.CarrierProductCatalog p WHERE p.TenantId=r.TenantId AND p.ProductCode=COALESCE(NULLIF(r.CarrierProductCode,N''), LEFT(REPLACE(UPPER(r.CarrierProductName),N' ',N'_'),80)) AND p.IsDeleted=0)
	GROUP BY r.TenantId,r.CarrierId,r.LineOfBusinessId,r.CarrierProductCode,r.CarrierProductName;
END;
