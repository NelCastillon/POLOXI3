IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Agency') EXEC(N'CREATE SCHEMA Agency');

IF OBJECT_ID(N'Agency.CarrierRuleCategory', N'U') IS NULL
BEGIN
	CREATE TABLE Agency.CarrierRuleCategory
	(
		CarrierRuleCategoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_CarrierRuleCategory PRIMARY KEY DEFAULT NEWID(),
		RuleCategoryCode NVARCHAR(80) NOT NULL,
		DisplayName NVARCHAR(160) NOT NULL,
		Description NVARCHAR(500) NULL,
		IconCssClass NVARCHAR(80) NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_CarrierRuleCategory_Sort_0075 DEFAULT 100,
		IsActive BIT NOT NULL CONSTRAINT DF_CarrierRuleCategory_IsActive_0075 DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierRuleCategory_Created_0075 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierRuleCategory_IsDeleted_0075 DEFAULT 0
	);
END;

IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'RuleCategoryCode') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD RuleCategoryCode NVARCHAR(80) NOT NULL CONSTRAINT DF_CarrierRuleCategory_Code_0075 DEFAULT N'General';
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'DisplayName') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD DisplayName NVARCHAR(160) NOT NULL CONSTRAINT DF_CarrierRuleCategory_Name_0075 DEFAULT N'General';
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'Description') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD Description NVARCHAR(500) NULL;
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'IconCssClass') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD IconCssClass NVARCHAR(80) NULL;
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'SortOrder') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD SortOrder INT NOT NULL CONSTRAINT DF_CarrierRuleCategory_Sort_Add_0075 DEFAULT 100;
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'IsActive') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD IsActive BIT NOT NULL CONSTRAINT DF_CarrierRuleCategory_IsActive_Add_0075 DEFAULT 1;
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'CreatedDateUtc') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierRuleCategory_Created_Add_0075 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'CreatedByUserId') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'ModifiedDateUtc') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'ModifiedByUserId') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Agency.CarrierRuleCategory', N'IsDeleted') IS NULL ALTER TABLE Agency.CarrierRuleCategory ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierRuleCategory_IsDeleted_Add_0075 DEFAULT 0;

IF OBJECT_ID(N'Agency.CarrierProductRule', N'U') IS NULL
BEGIN
	CREATE TABLE Agency.CarrierProductRule
	(
		CarrierProductRuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_CarrierProductRule PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		CarrierName NVARCHAR(200) NULL,
		CarrierNaic NVARCHAR(20) NULL,
		CarrierProductCode NVARCHAR(80) NULL,
		CarrierProductName NVARCHAR(200) NOT NULL,
		LineOfBusinessId UNIQUEIDENTIFIER NULL,
		LineOfBusinessCode NVARCHAR(80) NULL,
		StateCode NVARCHAR(2) NULL,
		RuleCategoryCode NVARCHAR(80) NOT NULL,
		RuleCode NVARCHAR(100) NOT NULL,
		RuleName NVARCHAR(240) NOT NULL,
		RuleDescription NVARCHAR(1000) NULL,
		EffectiveDate DATE NOT NULL CONSTRAINT DF_CarrierProductRule_Effective_0075 DEFAULT CONVERT(date, SYSUTCDATETIME()),
		ExpirationDate DATE NULL,
		Priority INT NOT NULL CONSTRAINT DF_CarrierProductRule_Priority_0075 DEFAULT 100,
		BillingType NVARCHAR(80) NULL,
		MinimumDownPaymentPercent DECIMAL(9,4) NULL,
		MinimumDownPaymentAmount DECIMAL(18,2) NULL,
		MaximumInstallments INT NULL,
		RequirePaymentBeforeBinding BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequirePaymentBeforeBinding_0075 DEFAULT 0,
		AllowPremiumFinance BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowPremiumFinance_0075 DEFAULT 0,
		AllowAgencyBill BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowAgencyBill_0075 DEFAULT 0,
		AllowDirectBill BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowDirectBill_0075 DEFAULT 1,
		AllowZeroDown BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowZeroDown_0075 DEFAULT 0,
		RequireSignedApplication BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireSignedApplication_0075 DEFAULT 0,
		RequirePayment BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequirePayment_0075 DEFAULT 0,
		RequireInspection BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireInspection_0075 DEFAULT 0,
		RequirePhotos BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequirePhotos_0075 DEFAULT 0,
		RequireLossRuns BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireLossRuns_0075 DEFAULT 0,
		AllowSameDayBind BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowSameDayBind_0075 DEFAULT 1,
		MaximumAdvanceBindDays INT NULL,
		AllowWeekendBinding BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowWeekendBinding_0075 DEFAULT 0,
		BindingTimeCutoff TIME(0) NULL,
		RequireUnderwriterApproval BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireUnderwriterApproval_0075 DEFAULT 0,
		RequireACORD125 BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireACORD125_0075 DEFAULT 0,
		RequireACORD126 BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireACORD126_0075 DEFAULT 0,
		RequireACORD127 BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireACORD127_0075 DEFAULT 0,
		RequireStatementOfValues BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireStatementOfValues_0075 DEFAULT 0,
		RequireFinancialStatement BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireFinancialStatement_0075 DEFAULT 0,
		RequireSupplementalForm BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireSupplementalForm_0075 DEFAULT 0,
		NewBusinessRate DECIMAL(9,4) NULL,
		RenewalRate DECIMAL(9,4) NULL,
		BrokerFeeAllowed BIT NOT NULL CONSTRAINT DF_CarrierProductRule_BrokerFeeAllowed_0075 DEFAULT 0,
		MaximumBrokerFee DECIMAL(18,2) NULL,
		CommissionSchedule NVARCHAR(240) NULL,
		CommissionPaymentMethod NVARCHAR(120) NULL,
		ValidateVIN BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateVIN_0075 DEFAULT 0,
		ValidateFEIN BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateFEIN_0075 DEFAULT 0,
		ValidateRoofAge BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateRoofAge_0075 DEFAULT 0,
		ValidateDriverAge BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateDriverAge_0075 DEFAULT 0,
		ValidatePayroll BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidatePayroll_0075 DEFAULT 0,
		ValidateSquareFootage BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateSquareFootage_0075 DEFAULT 0,
		ValidateClaimsHistory BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateClaimsHistory_0075 DEFAULT 0,
		RulePayloadJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CarrierProductRule_Payload_0075 DEFAULT N'{}',
		Notes NVARCHAR(1000) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_CarrierProductRule_IsActive_0075 DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierProductRule_Created_0075 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierProductRule_IsDeleted_0075 DEFAULT 0
	);
END;

DECLARE @CarrierProductRuleColumns TABLE (ColumnName SYSNAME NOT NULL, Definition NVARCHAR(MAX) NOT NULL);
INSERT INTO @CarrierProductRuleColumns (ColumnName, Definition) VALUES
(N'TenantId', N'UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CarrierProductRule_Tenant_Add_0075 DEFAULT ''00000000-0000-0000-0000-000000000001'''),
(N'CarrierId', N'UNIQUEIDENTIFIER NULL'),
(N'CarrierName', N'NVARCHAR(200) NULL'),
(N'CarrierNaic', N'NVARCHAR(20) NULL'),
(N'CarrierProductCode', N'NVARCHAR(80) NULL'),
(N'CarrierProductName', N'NVARCHAR(200) NOT NULL CONSTRAINT DF_CarrierProductRule_ProductName_Add_0075 DEFAULT N''Enterprise Product'''),
(N'LineOfBusinessId', N'UNIQUEIDENTIFIER NULL'),
(N'LineOfBusinessCode', N'NVARCHAR(80) NULL'),
(N'StateCode', N'NVARCHAR(2) NULL'),
(N'RuleCategoryCode', N'NVARCHAR(80) NOT NULL CONSTRAINT DF_CarrierProductRule_Category_Add_0075 DEFAULT N''General'''),
(N'RuleCode', N'NVARCHAR(100) NOT NULL CONSTRAINT DF_CarrierProductRule_Code_Add_0075 DEFAULT N''RULE'''),
(N'RuleName', N'NVARCHAR(240) NOT NULL CONSTRAINT DF_CarrierProductRule_Name_Add_0075 DEFAULT N''Carrier rule'''),
(N'RuleDescription', N'NVARCHAR(1000) NULL'),
(N'EffectiveDate', N'DATE NOT NULL CONSTRAINT DF_CarrierProductRule_Effective_Add_0075 DEFAULT CONVERT(date, SYSUTCDATETIME())'),
(N'ExpirationDate', N'DATE NULL'),
(N'Priority', N'INT NOT NULL CONSTRAINT DF_CarrierProductRule_Priority_Add_0075 DEFAULT 100'),
(N'BillingType', N'NVARCHAR(80) NULL'),
(N'MinimumDownPaymentPercent', N'DECIMAL(9,4) NULL'),
(N'MinimumDownPaymentAmount', N'DECIMAL(18,2) NULL'),
(N'MaximumInstallments', N'INT NULL'),
(N'RequirePaymentBeforeBinding', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequirePaymentBeforeBinding_Add_0075 DEFAULT 0'),
(N'AllowPremiumFinance', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowPremiumFinance_Add_0075 DEFAULT 0'),
(N'AllowAgencyBill', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowAgencyBill_Add_0075 DEFAULT 0'),
(N'AllowDirectBill', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowDirectBill_Add_0075 DEFAULT 1'),
(N'AllowZeroDown', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowZeroDown_Add_0075 DEFAULT 0'),
(N'RequireSignedApplication', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireSignedApplication_Add_0075 DEFAULT 0'),
(N'RequirePayment', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequirePayment_Add_0075 DEFAULT 0'),
(N'RequireInspection', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireInspection_Add_0075 DEFAULT 0'),
(N'RequirePhotos', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequirePhotos_Add_0075 DEFAULT 0'),
(N'RequireLossRuns', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireLossRuns_Add_0075 DEFAULT 0'),
(N'AllowSameDayBind', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowSameDayBind_Add_0075 DEFAULT 1'),
(N'MaximumAdvanceBindDays', N'INT NULL'),
(N'AllowWeekendBinding', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_AllowWeekendBinding_Add_0075 DEFAULT 0'),
(N'BindingTimeCutoff', N'TIME(0) NULL'),
(N'RequireUnderwriterApproval', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireUnderwriterApproval_Add_0075 DEFAULT 0'),
(N'RequireACORD125', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireACORD125_Add_0075 DEFAULT 0'),
(N'RequireACORD126', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireACORD126_Add_0075 DEFAULT 0'),
(N'RequireACORD127', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireACORD127_Add_0075 DEFAULT 0'),
(N'RequireStatementOfValues', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireStatementOfValues_Add_0075 DEFAULT 0'),
(N'RequireFinancialStatement', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireFinancialStatement_Add_0075 DEFAULT 0'),
(N'RequireSupplementalForm', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_RequireSupplementalForm_Add_0075 DEFAULT 0'),
(N'NewBusinessRate', N'DECIMAL(9,4) NULL'),
(N'RenewalRate', N'DECIMAL(9,4) NULL'),
(N'BrokerFeeAllowed', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_BrokerFeeAllowed_Add_0075 DEFAULT 0'),
(N'MaximumBrokerFee', N'DECIMAL(18,2) NULL'),
(N'CommissionSchedule', N'NVARCHAR(240) NULL'),
(N'CommissionPaymentMethod', N'NVARCHAR(120) NULL'),
(N'ValidateVIN', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateVIN_Add_0075 DEFAULT 0'),
(N'ValidateFEIN', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateFEIN_Add_0075 DEFAULT 0'),
(N'ValidateRoofAge', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateRoofAge_Add_0075 DEFAULT 0'),
(N'ValidateDriverAge', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateDriverAge_Add_0075 DEFAULT 0'),
(N'ValidatePayroll', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidatePayroll_Add_0075 DEFAULT 0'),
(N'ValidateSquareFootage', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateSquareFootage_Add_0075 DEFAULT 0'),
(N'ValidateClaimsHistory', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_ValidateClaimsHistory_Add_0075 DEFAULT 0'),
(N'RulePayloadJson', N'NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CarrierProductRule_Payload_Add_0075 DEFAULT N''{}'''),
(N'Notes', N'NVARCHAR(1000) NULL'),
(N'IsActive', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_IsActive_Add_0075 DEFAULT 1'),
(N'CreatedDateUtc', N'DATETIME2 NOT NULL CONSTRAINT DF_CarrierProductRule_Created_Add_0075 DEFAULT SYSUTCDATETIME()'),
(N'CreatedByUserId', N'UNIQUEIDENTIFIER NULL'),
(N'ModifiedDateUtc', N'DATETIME2 NULL'),
(N'ModifiedByUserId', N'UNIQUEIDENTIFIER NULL'),
(N'IsDeleted', N'BIT NOT NULL CONSTRAINT DF_CarrierProductRule_IsDeleted_Add_0075 DEFAULT 0');

DECLARE @ColumnName0075 SYSNAME;
DECLARE @Definition0075 NVARCHAR(MAX);
DECLARE @AlterColumnSql0075 NVARCHAR(MAX);
DECLARE carrier_rule_column_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT ColumnName, Definition FROM @CarrierProductRuleColumns;
OPEN carrier_rule_column_cursor;
FETCH NEXT FROM carrier_rule_column_cursor INTO @ColumnName0075, @Definition0075;
WHILE @@FETCH_STATUS = 0
BEGIN
	IF COL_LENGTH(N'Agency.CarrierProductRule', @ColumnName0075) IS NULL
	BEGIN
		SET @AlterColumnSql0075 = N'ALTER TABLE Agency.CarrierProductRule ADD ' + QUOTENAME(@ColumnName0075) + N' ' + @Definition0075 + N';';
		EXEC sys.sp_executesql @AlterColumnSql0075;
	END;
	FETCH NEXT FROM carrier_rule_column_cursor INTO @ColumnName0075, @Definition0075;
END;
CLOSE carrier_rule_column_cursor;
DEALLOCATE carrier_rule_column_cursor;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CarrierProductRule_DownPaymentPercent_0075' AND parent_object_id = OBJECT_ID(N'Agency.CarrierProductRule'))
	ALTER TABLE Agency.CarrierProductRule ADD CONSTRAINT CK_CarrierProductRule_DownPaymentPercent_0075 CHECK (MinimumDownPaymentPercent IS NULL OR (MinimumDownPaymentPercent >= 0 AND MinimumDownPaymentPercent <= 100));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CarrierProductRule_CommissionRates_0075' AND parent_object_id = OBJECT_ID(N'Agency.CarrierProductRule'))
	ALTER TABLE Agency.CarrierProductRule ADD CONSTRAINT CK_CarrierProductRule_CommissionRates_0075 CHECK ((NewBusinessRate IS NULL OR (NewBusinessRate >= 0 AND NewBusinessRate <= 100)) AND (RenewalRate IS NULL OR (RenewalRate >= 0 AND RenewalRate <= 100)));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CarrierProductRule_Installments_0075' AND parent_object_id = OBJECT_ID(N'Agency.CarrierProductRule'))
	ALTER TABLE Agency.CarrierProductRule ADD CONSTRAINT CK_CarrierProductRule_Installments_0075 CHECK (MaximumInstallments IS NULL OR MaximumInstallments >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CarrierProductRule_Expiration_0075' AND parent_object_id = OBJECT_ID(N'Agency.CarrierProductRule'))
	ALTER TABLE Agency.CarrierProductRule ADD CONSTRAINT CK_CarrierProductRule_Expiration_0075 CHECK (ExpirationDate IS NULL OR ExpirationDate >= EffectiveDate);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierRuleCategory') AND name = N'UX_CarrierRuleCategory_Code_0075')
	CREATE UNIQUE INDEX UX_CarrierRuleCategory_Code_0075 ON Agency.CarrierRuleCategory(RuleCategoryCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierRuleCategory') AND name = N'IX_CarrierRuleCategory_Active_0075')
	CREATE INDEX IX_CarrierRuleCategory_Active_0075 ON Agency.CarrierRuleCategory(IsDeleted, IsActive, SortOrder, DisplayName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierProductRule') AND name = N'UX_CarrierProductRule_Tenant_Code_0075')
	CREATE UNIQUE INDEX UX_CarrierProductRule_Tenant_Code_0075 ON Agency.CarrierProductRule(TenantId, RuleCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierProductRule') AND name = N'IX_CarrierProductRule_Tenant_Category_0075')
	CREATE INDEX IX_CarrierProductRule_Tenant_Category_0075 ON Agency.CarrierProductRule(TenantId, IsDeleted, RuleCategoryCode, IsActive, Priority, RuleName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Agency.CarrierProductRule') AND name = N'IX_CarrierProductRule_Tenant_Scope_0075')
	CREATE INDEX IX_CarrierProductRule_Tenant_Scope_0075 ON Agency.CarrierProductRule(TenantId, IsDeleted, CarrierId, CarrierNaic, LineOfBusinessCode, StateCode, EffectiveDate, ExpirationDate);

MERGE Agency.CarrierRuleCategory AS target
USING (VALUES
	(N'Billing', N'Billing Rules', N'Down payment, billing method, installment, premium finance, and pay-before-bind rules.', N'bi bi-receipt', 10),
	(N'Binding', N'Binding Rules', N'Signed application, payment, inspection, photo, loss run, same-day bind, cutoff, weekend, and underwriter approval rules.', N'bi bi-shield-check', 20),
	(N'Policy', N'Policy Rules', N'Policy issuance, servicing, and carrier policy lifecycle configuration.', N'bi bi-file-earmark-check', 30),
	(N'Document', N'Document Rules', N'ACORD, photo, statement of values, financial statement, and supplemental form requirements.', N'bi bi-folder-check', 40),
	(N'Commission', N'Commission Rules', N'New business and renewal rates, broker fees, schedules, and commission payment methods.', N'bi bi-percent', 50),
	(N'Download', N'Download Rules', N'Carrier download, IVANS, AL3, eDocs, and transaction normalization rules.', N'bi bi-arrow-down-circle', 60),
	(N'Validation', N'Validation Rules', N'VIN, FEIN, roof age, driver age, payroll, square footage, and claims-history validation rules.', N'bi bi-patch-check', 70),
	(N'Endorsement', N'Endorsement Rules', N'Carrier-specific endorsement submission and approval rules.', N'bi bi-pencil-square', 80),
	(N'Cancellation', N'Cancellation Rules', N'Carrier cancellation notice, reason, evidence, and workflow rules.', N'bi bi-x-octagon', 90),
	(N'Renewal', N'Renewal Rules', N'Renewal eligibility, remarketing, and renewal submission requirement rules.', N'bi bi-arrow-clockwise', 100)
) AS source (RuleCategoryCode, DisplayName, Description, IconCssClass, SortOrder)
ON target.RuleCategoryCode = source.RuleCategoryCode
WHEN MATCHED THEN UPDATE SET
	target.DisplayName = source.DisplayName,
	target.Description = source.Description,
	target.IconCssClass = source.IconCssClass,
	target.SortOrder = source.SortOrder,
	target.IsActive = 1,
	target.IsDeleted = 0,
	target.ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (CarrierRuleCategoryId, RuleCategoryCode, DisplayName, Description, IconCssClass, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), source.RuleCategoryCode, source.DisplayName, source.Description, source.IconCssClass, source.SortOrder, 1, SYSUTCDATETIME(), 0);

DECLARE @CarrierRuleTenants0075 TABLE (TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
BEGIN
	INSERT INTO @CarrierRuleTenants0075 (TenantId)
	SELECT TenantId FROM Core.Tenant WHERE ISNULL(IsDeleted, 0) = 0;
END;

IF NOT EXISTS (SELECT 1 FROM @CarrierRuleTenants0075)
BEGIN
	INSERT INTO @CarrierRuleTenants0075 (TenantId) VALUES ('00000000-0000-0000-0000-000000000001');
END;

INSERT INTO Agency.CarrierProductRule
(
	CarrierProductRuleId, TenantId, CarrierId, CarrierName, CarrierNaic, CarrierProductCode, CarrierProductName, LineOfBusinessCode, StateCode,
	RuleCategoryCode, RuleCode, RuleName, RuleDescription, EffectiveDate, Priority, BillingType, MinimumDownPaymentPercent, MinimumDownPaymentAmount,
	MaximumInstallments, RequirePaymentBeforeBinding, AllowPremiumFinance, AllowAgencyBill, AllowDirectBill, AllowZeroDown, RequireSignedApplication,
	RequirePayment, RequireInspection, RequirePhotos, RequireLossRuns, AllowSameDayBind, MaximumAdvanceBindDays, AllowWeekendBinding, BindingTimeCutoff,
	RequireUnderwriterApproval, RequireACORD125, RequireACORD126, RequireACORD127, RequireStatementOfValues, RequireFinancialStatement,
	RequireSupplementalForm, NewBusinessRate, RenewalRate, BrokerFeeAllowed, MaximumBrokerFee, CommissionSchedule, CommissionPaymentMethod,
	ValidateVIN, ValidateFEIN, ValidateRoofAge, ValidateDriverAge, ValidatePayroll, ValidateSquareFootage, ValidateClaimsHistory, RulePayloadJson,
	Notes, IsActive, CreatedDateUtc, IsDeleted
)
SELECT NEWID(), t.TenantId, NULL, N'Enterprise Carrier', NULL, seed.CarrierProductCode, seed.CarrierProductName, seed.LineOfBusinessCode, seed.StateCode,
	   seed.RuleCategoryCode, CONCAT(seed.RuleCodePrefix, N'-GEN-', seed.LineOfBusinessCode, N'-', seed.StateCode), seed.RuleName,
	   seed.RuleDescription, CONVERT(date, SYSUTCDATETIME()), seed.Priority, seed.BillingType, seed.MinimumDownPaymentPercent, seed.MinimumDownPaymentAmount,
	   seed.MaximumInstallments, seed.RequirePaymentBeforeBinding, seed.AllowPremiumFinance, seed.AllowAgencyBill, seed.AllowDirectBill, seed.AllowZeroDown,
	   seed.RequireSignedApplication, seed.RequirePayment, seed.RequireInspection, seed.RequirePhotos, seed.RequireLossRuns, seed.AllowSameDayBind,
	   seed.MaximumAdvanceBindDays, seed.AllowWeekendBinding, seed.BindingTimeCutoff, seed.RequireUnderwriterApproval, seed.RequireACORD125,
	   seed.RequireACORD126, seed.RequireACORD127, seed.RequireStatementOfValues, seed.RequireFinancialStatement, seed.RequireSupplementalForm,
	   seed.NewBusinessRate, seed.RenewalRate, seed.BrokerFeeAllowed, seed.MaximumBrokerFee, seed.CommissionSchedule, seed.CommissionPaymentMethod,
	   seed.ValidateVIN, seed.ValidateFEIN, seed.ValidateRoofAge, seed.ValidateDriverAge, seed.ValidatePayroll, seed.ValidateSquareFootage,
	   seed.ValidateClaimsHistory, seed.RulePayloadJson, seed.Notes, 1, SYSUTCDATETIME(), 0
FROM @CarrierRuleTenants0075 t
CROSS APPLY (VALUES
	(N'Billing', N'BILLING', N'CommercialAuto', N'CommercialAuto', N'Commercial Auto', N'CA', N'Billing - commercial auto down payment', N'Commercial auto billing setup with minimum down payment, installment, and bill method controls.', 10, N'Direct Bill', CAST(25.0000 AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(18,2)), 10, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, CAST(NULL AS INT), 0, CAST(NULL AS TIME(0)), 0, 0, 0, 0, 0, 0, 0, CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(9,4)), 0, CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS NVARCHAR(240)), CAST(NULL AS NVARCHAR(120)), 0, 0, 0, 0, 0, 0, 0, N'{"billingMethods":["DirectBill","AgencyBill","PremiumFinance"]}', N'Enterprise seed: billing controls are DB configurable per carrier product.'),
	(N'Binding', N'BINDING', N'BusinessOwnersPolicy', N'BusinessOwnersPolicy', N'Business Owners Policy', N'CA', N'Binding - BOP readiness', N'Binding requirements for BOP submissions including payment, inspections, photos, cutoffs, and underwriter approvals.', 20, CAST(NULL AS NVARCHAR(80)), CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS INT), 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 1, 30, 0, CAST('17:00:00' AS TIME(0)), 1, 0, 0, 0, 0, 0, 0, CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(9,4)), 0, CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS NVARCHAR(240)), CAST(NULL AS NVARCHAR(120)), 0, 0, 0, 0, 0, 0, 0, N'{"cutoffTimeZone":"AgencyLocal","confirmationSources":["API","Portal","Email","Phone"]}', N'Enterprise seed: bind controls remain DB configurable.'),
	(N'Document', N'DOCUMENT', N'GeneralLiability', N'GeneralLiability', N'General Liability', N'CA', N'Document - GL submission packet', N'Document requirements for GL carrier submissions and quote readiness.', 30, CAST(NULL AS NVARCHAR(80)), CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS INT), 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, CAST(NULL AS INT), 0, CAST(NULL AS TIME(0)), 0, 1, 1, 0, 0, 0, 1, CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(9,4)), 0, CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS NVARCHAR(240)), CAST(NULL AS NVARCHAR(120)), 0, 0, 0, 0, 0, 0, 0, N'{"requiredDocuments":["ACORD125","ACORD126","SupplementalForm"]}', N'Enterprise seed: document requirements are DB backed.'),
	(N'Commission', N'COMMISSION', N'WorkersComp', N'WorkersComp', N'Workers Compensation', N'CA', N'Commission - workers comp standard', N'Carrier product commission rate, broker fee, schedule, and payment method settings.', 40, CAST(NULL AS NVARCHAR(80)), CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS INT), 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, CAST(NULL AS INT), 0, CAST(NULL AS TIME(0)), 0, 0, 0, 0, 0, 0, 0, 12.5000, 10.0000, 1, 250.00, N'Standard WC Schedule', N'Carrier Statement', 0, 0, 0, 0, 0, 0, 0, N'{"scheduleBasis":"WrittenPremium","statementFrequency":"Monthly"}', N'Enterprise seed: commission settings are editable by tenant admins.'),
	(N'Validation', N'VALIDATION', N'CommercialProperty', N'CommercialProperty', N'Commercial Property', N'CA', N'Validation - property risk checks', N'Carrier product validation requirements for risk readiness and submission quality.', 50, CAST(NULL AS NVARCHAR(80)), CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS INT), 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, CAST(NULL AS INT), 0, CAST(NULL AS TIME(0)), 0, 0, 0, 0, 1, 0, 0, CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(9,4)), 0, CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS NVARCHAR(240)), CAST(NULL AS NVARCHAR(120)), 0, 1, 1, 0, 0, 1, 1, N'{"validationSeverity":"Blocking","source":"CarrierRule"}', N'Enterprise seed: validation switches drive carrier readiness checks.'),
	(N'Download', N'DOWNLOAD', N'CommercialPackage', N'CommercialPackage', N'Commercial Package', N'CA', N'Download - policy transaction normalization', N'Carrier download rules for policy transaction, document, billing, and commission feed normalization.', 60, CAST(NULL AS NVARCHAR(80)), CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS INT), 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, CAST(NULL AS INT), 0, CAST(NULL AS TIME(0)), 0, 0, 0, 0, 0, 0, 0, CAST(NULL AS DECIMAL(9,4)), CAST(NULL AS DECIMAL(9,4)), 0, CAST(NULL AS DECIMAL(18,2)), CAST(NULL AS NVARCHAR(240)), CAST(NULL AS NVARCHAR(120)), 0, 0, 0, 0, 0, 0, 0, N'{"transactions":["Policy","Billing","Commission","Claim"],"formats":["AL3","eDocs","API"]}', N'Enterprise seed: download behavior remains configuration-driven.')
) seed (RuleCategoryCode, RuleCodePrefix, CarrierProductCode, LineOfBusinessCode, CarrierProductName, StateCode, RuleName, RuleDescription, Priority, BillingType, MinimumDownPaymentPercent, MinimumDownPaymentAmount, MaximumInstallments, RequirePaymentBeforeBinding, AllowPremiumFinance, AllowAgencyBill, AllowDirectBill, AllowZeroDown, RequireSignedApplication, RequirePayment, RequireInspection, RequirePhotos, RequireLossRuns, AllowSameDayBind, MaximumAdvanceBindDays, AllowWeekendBinding, BindingTimeCutoff, RequireUnderwriterApproval, RequireACORD125, RequireACORD126, RequireACORD127, RequireStatementOfValues, RequireFinancialStatement, RequireSupplementalForm, NewBusinessRate, RenewalRate, BrokerFeeAllowed, MaximumBrokerFee, CommissionSchedule, CommissionPaymentMethod, ValidateVIN, ValidateFEIN, ValidateRoofAge, ValidateDriverAge, ValidatePayroll, ValidateSquareFootage, ValidateClaimsHistory, RulePayloadJson, Notes)
WHERE NOT EXISTS
(
	SELECT 1
	FROM Agency.CarrierProductRule r
	WHERE r.TenantId = t.TenantId
		AND r.RuleCode = CONCAT(seed.RuleCodePrefix, N'-GEN-', seed.LineOfBusinessCode, N'-', seed.StateCode)
	  AND r.IsDeleted = 0
);
