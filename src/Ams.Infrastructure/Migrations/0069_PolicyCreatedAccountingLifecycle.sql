SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Accounting') EXEC(N'CREATE SCHEMA Accounting');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Finance') EXEC(N'CREATE SCHEMA Finance');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Accounting.PolicyCreatedEvent', N'U') IS NULL
BEGIN
	CREATE TABLE Accounting.PolicyCreatedEvent
	(
		PolicyCreatedEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyCreatedEvent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyTermId UNIQUEIDENTIFIER NOT NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NULL,
		EventTypeCode NVARCHAR(80) NOT NULL CONSTRAINT DF_PolicyCreatedEvent_Type DEFAULT N'PolicyCreated',
		EventVersion INT NOT NULL CONSTRAINT DF_PolicyCreatedEvent_Version DEFAULT 1,
		CorrelationId UNIQUEIDENTIFIER NOT NULL,
		PayloadJson NVARCHAR(MAX) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyCreatedEvent_Status DEFAULT N'Pending',
		AttemptCount INT NOT NULL CONSTRAINT DF_PolicyCreatedEvent_Attempts DEFAULT 0,
		NextAttemptDateUtc DATETIME2 NULL,
		ProcessingStartedDateUtc DATETIME2 NULL,
		ProcessedDateUtc DATETIME2 NULL,
		WorkerId NVARCHAR(200) NULL,
		ErrorDetails NVARCHAR(4000) NULL,
		OccurredDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCreatedEvent_Occurred DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCreatedEvent_Deleted DEFAULT 0,
		CONSTRAINT CK_PolicyCreatedEvent_PayloadJson CHECK (ISJSON(PayloadJson) = 1)
	);
END;

IF OBJECT_ID(N'Accounting.PolicyCommissionSplit', N'U') IS NULL
BEGIN
	CREATE TABLE Accounting.PolicyCommissionSplit
	(
		PolicyCommissionSplitId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyCommissionSplit PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyTermId UNIQUEIDENTIFIER NOT NULL,
		PolicyCreatedEventId UNIQUEIDENTIFIER NOT NULL,
		CommissionTransactionId UNIQUEIDENTIFIER NULL,
		PayeeId UNIQUEIDENTIFIER NULL,
		PayeeTypeCode NVARCHAR(50) NOT NULL,
		SplitPercent DECIMAL(9,4) NOT NULL,
		SplitAmount DECIMAL(18,2) NOT NULL,
		ExpectedDate DATE NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyCommissionSplit_Status DEFAULT N'PendingEarned',
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCommissionSplit_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCommissionSplit_Deleted DEFAULT 0
	);
END;

IF OBJECT_ID(N'Accounting.PolicyAccountingOption', N'U') IS NULL
BEGIN
	CREATE TABLE Accounting.PolicyAccountingOption
	(
		PolicyAccountingOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyAccountingOption PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		OptionGroupCode NVARCHAR(80) NOT NULL,
		OptionCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(1000) NULL,
		TextValue NVARCHAR(500) NULL,
		NumericValue DECIMAL(18,4) NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_PolicyAccountingOption_Default DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_PolicyAccountingOption_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_PolicyAccountingOption_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyAccountingOption_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyAccountingOption_Deleted DEFAULT 0
	);
END;

IF OBJECT_ID(N'Accounting.PolicyAccountingState', N'U') IS NULL
BEGIN
	CREATE TABLE Accounting.PolicyAccountingState
	(
		PolicyAccountingStateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyAccountingState PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyTermId UNIQUEIDENTIFIER NOT NULL,
		PolicyCreatedEventId UNIQUEIDENTIFIER NOT NULL,
		BillingTypeCode NVARCHAR(50) NOT NULL,
		CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_PolicyAccountingState_Currency DEFAULT N'USD',
		PremiumAmount DECIMAL(18,2) NOT NULL,
		FeeAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyAccountingState_Fees DEFAULT 0,
		TaxAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyAccountingState_Taxes DEFAULT 0,
		InvoiceAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyAccountingState_Invoice DEFAULT 0,
		OutstandingBalance DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyAccountingState_Balance DEFAULT 0,
		CommissionRatePct DECIMAL(9,4) NOT NULL CONSTRAINT DF_PolicyAccountingState_CommissionRate DEFAULT 0,
		CommissionAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyAccountingState_Commission DEFAULT 0,
		CarrierPayableAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyAccountingState_CarrierPayable DEFAULT 0,
		InstallmentCount INT NOT NULL CONSTRAINT DF_PolicyAccountingState_Installments DEFAULT 0,
		InvoiceId UNIQUEIDENTIFIER NULL,
		AgencyBillReceivableId UNIQUEIDENTIFIER NULL,
		CarrierPayableId UNIQUEIDENTIFIER NULL,
		CommissionExpectedReceivableId UNIQUEIDENTIFIER NULL,
		JournalEntryId UNIQUEIDENTIFIER NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyAccountingState_Status DEFAULT N'Pending',
		SynchronizedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyAccountingState_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyAccountingState_Deleted DEFAULT 0
	);
END;

IF OBJECT_ID(N'Accounting.CarrierPayable', N'U') IS NULL
BEGIN
	CREATE TABLE Accounting.CarrierPayable
	(
		CarrierPayableId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CarrierPayable PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyTermId UNIQUEIDENTIFIER NOT NULL,
		PolicyCreatedEventId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NOT NULL,
		PayableNumber NVARCHAR(80) NOT NULL,
		PremiumAmount DECIMAL(18,2) NOT NULL,
		CommissionAmount DECIMAL(18,2) NOT NULL,
		FeeAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CarrierPayable_Fees DEFAULT 0,
		TaxAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CarrierPayable_Taxes DEFAULT 0,
		PayableAmount DECIMAL(18,2) NOT NULL,
		PaidAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CarrierPayable_Paid DEFAULT 0,
		DueDate DATE NOT NULL,
		CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_CarrierPayable_Currency DEFAULT N'USD',
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CarrierPayable_Status DEFAULT N'PendingRemittance',
		TrustRequired BIT NOT NULL CONSTRAINT DF_CarrierPayable_Trust DEFAULT 1,
		RemittedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierPayable_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierPayable_Deleted DEFAULT 0
	);
END;

IF OBJECT_ID(N'Accounting.PolicyAccountingWorkItem', N'U') IS NULL
BEGIN
	CREATE TABLE Accounting.PolicyAccountingWorkItem
	(
		PolicyAccountingWorkItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyAccountingWorkItem PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyTermId UNIQUEIDENTIFIER NULL,
		PolicyCreatedEventId UNIQUEIDENTIFIER NULL,
		WorkItemTypeCode NVARCHAR(80) NOT NULL,
		QueueCode NVARCHAR(80) NOT NULL,
		Title NVARCHAR(240) NOT NULL,
		ReferenceNumber NVARCHAR(100) NOT NULL,
		Amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyAccountingWorkItem_Amount DEFAULT 0,
		PriorityCode NVARCHAR(30) NOT NULL CONSTRAINT DF_PolicyAccountingWorkItem_Priority DEFAULT N'Normal',
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyAccountingWorkItem_Status DEFAULT N'Open',
		DueDateUtc DATETIME2 NULL,
		AssignedToUserId UNIQUEIDENTIFIER NULL,
		DetailUrl NVARCHAR(500) NULL,
		Notes NVARCHAR(2000) NULL,
		CompletedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyAccountingWorkItem_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyAccountingWorkItem_Deleted DEFAULT 0
	);
END;

IF OBJECT_ID(N'Accounting.PolicyAccountingAuditEvent', N'U') IS NULL
BEGIN
	CREATE TABLE Accounting.PolicyAccountingAuditEvent
	(
		PolicyAccountingAuditEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyAccountingAuditEvent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		PolicyCreatedEventId UNIQUEIDENTIFIER NULL,
		EventTypeCode NVARCHAR(80) NOT NULL,
		EventDescription NVARCHAR(1000) NOT NULL,
		DataJson NVARCHAR(MAX) NULL,
		ActorUserId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyAccountingAuditEvent_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT CK_PolicyAccountingAuditEvent_DataJson CHECK (DataJson IS NULL OR ISJSON(DataJson) = 1)
	);
END;

IF OBJECT_ID(N'Billing.Invoice', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.Invoice
	(
		InvoiceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BillingInvoice PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		InvoiceNumber NVARCHAR(80) NOT NULL,
		AccountId UNIQUEIDENTIFIER NOT NULL,
		AgreementId UNIQUEIDENTIFIER NULL,
		PolicyId UNIQUEIDENTIFIER NULL,
		PolicyTermId UNIQUEIDENTIFIER NULL,
		SourceEventId UNIQUEIDENTIFIER NULL,
		InvoiceDate DATE NOT NULL,
		DueDate DATE NOT NULL,
		TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_BillingInvoice_Total DEFAULT 0,
		BalanceAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_BillingInvoice_Balance DEFAULT 0,
		CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_BillingInvoice_Currency DEFAULT N'USD',
		BillingTypeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingInvoice_BillingType DEFAULT N'AgencyBill',
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingInvoice_Status DEFAULT N'Open',
		InvoiceStatusCodeId NVARCHAR(50) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BillingInvoice_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BillingInvoice_Deleted DEFAULT 0
	);
END;
IF COL_LENGTH(N'Billing.Invoice', N'IsDeleted') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD IsDeleted BIT NULL;');
EXEC(N'UPDATE Billing.Invoice SET IsDeleted=0 WHERE IsDeleted IS NULL;');
IF COL_LENGTH(N'Billing.Invoice', N'PolicyId') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD PolicyId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Billing.Invoice', N'PolicyTermId') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD PolicyTermId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Billing.Invoice', N'SourceEventId') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD SourceEventId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Billing.Invoice', N'InvoiceDate') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD InvoiceDate DATE NULL;');
IF COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD DueDate DATE NULL;');
IF COL_LENGTH(N'Billing.Invoice', N'CurrencyCode') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD CurrencyCode NVARCHAR(3) NULL;');
IF COL_LENGTH(N'Billing.Invoice', N'BillingTypeCode') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD BillingTypeCode NVARCHAR(50) NULL;');
IF COL_LENGTH(N'Billing.Invoice', N'StatusCode') IS NULL EXEC(N'ALTER TABLE Billing.Invoice ADD StatusCode NVARCHAR(50) NULL;');
EXEC(N'UPDATE Billing.Invoice SET InvoiceDate=COALESCE(InvoiceDate,CONVERT(date,CreatedDateUtc)),DueDate=COALESCE(DueDate,DATEADD(day,30,CONVERT(date,CreatedDateUtc))),CurrencyCode=COALESCE(NULLIF(CurrencyCode,N''''),N''USD''),BillingTypeCode=COALESCE(NULLIF(BillingTypeCode,N''''),N''AgencyBill''),StatusCode=COALESCE(NULLIF(StatusCode,N''''),CASE WHEN BalanceAmount<=0 THEN N''Paid'' ELSE N''Open'' END);');

IF OBJECT_ID(N'Billing.InvoiceLine', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.InvoiceLine
	(
		InvoiceLineId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BillingInvoiceLine PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		InvoiceId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NULL,
		PolicyTermId UNIQUEIDENTIFIER NULL,
		SourceEventId UNIQUEIDENTIFIER NULL,
		LineOrder INT NOT NULL,
		LineTypeCode NVARCHAR(50) NOT NULL,
		ItemCode NVARCHAR(80) NOT NULL,
		Description NVARCHAR(500) NOT NULL,
		Amount DECIMAL(18,2) NOT NULL,
		IsCarrierMoney BIT NOT NULL CONSTRAINT DF_BillingInvoiceLine_CarrierMoney DEFAULT 0,
		RevenueRecognitionCode NVARCHAR(50) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BillingInvoiceLine_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BillingInvoiceLine_Deleted DEFAULT 0
	);
END;
IF COL_LENGTH(N'Billing.InvoiceLine', N'TenantId') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD TenantId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'PolicyId') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD PolicyId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'PolicyTermId') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD PolicyTermId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'SourceEventId') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD SourceEventId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'LineTypeCode') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD LineTypeCode NVARCHAR(50) NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'ItemCode') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD ItemCode NVARCHAR(80) NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'Amount') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD Amount DECIMAL(18,2) NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'IsCarrierMoney') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD IsCarrierMoney BIT NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'RevenueRecognitionCode') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD RevenueRecognitionCode NVARCHAR(50) NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'CreatedByUserId') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD CreatedByUserId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Billing.InvoiceLine', N'IsDeleted') IS NULL EXEC(N'ALTER TABLE Billing.InvoiceLine ADD IsDeleted BIT NULL;');
EXEC(N'UPDATE Billing.InvoiceLine SET IsDeleted=0 WHERE IsDeleted IS NULL;');

IF COL_LENGTH(N'Billing.AgencyBillReceivable', N'SourceEventId') IS NULL EXEC(N'ALTER TABLE Billing.AgencyBillReceivable ADD SourceEventId UNIQUEIDENTIFIER NULL;');

IF COL_LENGTH(N'Commission.CommissionExpectedReceivable', N'PolicyCreatedEventId') IS NULL EXEC(N'ALTER TABLE Commission.CommissionExpectedReceivable ADD PolicyCreatedEventId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Commission.CommissionExpectedReceivable', N'CommissionTransactionId') IS NULL EXEC(N'ALTER TABLE Commission.CommissionExpectedReceivable ADD CommissionTransactionId UNIQUEIDENTIFIER NULL;');

IF COL_LENGTH(N'Finance.JournalEntry', N'PolicyId') IS NULL EXEC(N'ALTER TABLE Finance.JournalEntry ADD PolicyId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Finance.JournalEntry', N'PolicyTermId') IS NULL EXEC(N'ALTER TABLE Finance.JournalEntry ADD PolicyTermId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Finance.JournalEntry', N'SourceEventId') IS NULL EXEC(N'ALTER TABLE Finance.JournalEntry ADD SourceEventId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Finance.JournalEntry', N'EntryTypeCode') IS NULL EXEC(N'ALTER TABLE Finance.JournalEntry ADD EntryTypeCode NVARCHAR(80) NULL;');

IF OBJECT_ID(N'Finance.JournalEntryLine', N'U') IS NULL
BEGIN
	CREATE TABLE Finance.JournalEntryLine
	(
		LineId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JournalEntryLine PRIMARY KEY DEFAULT NEWID(),
		JournalEntryId UNIQUEIDENTIFIER NOT NULL,
		GLAccountId UNIQUEIDENTIFIER NOT NULL,
		DebitAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_JournalEntryLine_Debit DEFAULT 0,
		CreditAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_JournalEntryLine_Credit DEFAULT 0,
		Description NVARCHAR(1000) NULL,
		LineOrder INT NOT NULL CONSTRAINT DF_JournalEntryLine_Order DEFAULT 0,
		TenantId UNIQUEIDENTIFIER NULL,
		PolicyId UNIQUEIDENTIFIER NULL,
		AccountingCategoryCode NVARCHAR(80) NULL
	);
END;
IF COL_LENGTH(N'Finance.JournalEntryLine', N'TenantId') IS NULL EXEC(N'ALTER TABLE Finance.JournalEntryLine ADD TenantId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Finance.JournalEntryLine', N'PolicyId') IS NULL EXEC(N'ALTER TABLE Finance.JournalEntryLine ADD PolicyId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(N'Finance.JournalEntryLine', N'AccountingCategoryCode') IS NULL EXEC(N'ALTER TABLE Finance.JournalEntryLine ADD AccountingCategoryCode NVARCHAR(80) NULL;');

IF COL_LENGTH(N'Finance.Vendor', N'CarrierId') IS NULL EXEC(N'ALTER TABLE Finance.Vendor ADD CarrierId UNIQUEIDENTIFIER NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Accounting.PolicyCreatedEvent') AND name=N'UX_PolicyCreatedEvent_Tenant_PolicyTerm') CREATE UNIQUE INDEX UX_PolicyCreatedEvent_Tenant_PolicyTerm ON Accounting.PolicyCreatedEvent(TenantId,PolicyId,PolicyTermId,EventTypeCode,EventVersion) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Accounting.PolicyCreatedEvent') AND name=N'IX_PolicyCreatedEvent_Queue') CREATE INDEX IX_PolicyCreatedEvent_Queue ON Accounting.PolicyCreatedEvent(StatusCode,NextAttemptDateUtc,OccurredDateUtc) INCLUDE(TenantId,PolicyId,PolicyTermId,AttemptCount) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Accounting.PolicyAccountingOption') AND name=N'UX_PolicyAccountingOption_Tenant_Group_Code') CREATE UNIQUE INDEX UX_PolicyAccountingOption_Tenant_Group_Code ON Accounting.PolicyAccountingOption(TenantId,OptionGroupCode,OptionCode) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Accounting.PolicyAccountingState') AND name=N'UX_PolicyAccountingState_Tenant_PolicyTerm') CREATE UNIQUE INDEX UX_PolicyAccountingState_Tenant_PolicyTerm ON Accounting.PolicyAccountingState(TenantId,PolicyId,PolicyTermId) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Accounting.CarrierPayable') AND name=N'UX_CarrierPayable_Tenant_PolicyTerm') CREATE UNIQUE INDEX UX_CarrierPayable_Tenant_PolicyTerm ON Accounting.CarrierPayable(TenantId,PolicyId,PolicyTermId) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Accounting.PolicyCommissionSplit') AND name=N'UX_PolicyCommissionSplit_Source_PayeeType') CREATE UNIQUE INDEX UX_PolicyCommissionSplit_Source_PayeeType ON Accounting.PolicyCommissionSplit(TenantId,PolicyId,PolicyTermId,PayeeTypeCode) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Accounting.PolicyAccountingWorkItem') AND name=N'UX_PolicyAccountingWorkItem_Source_Type') CREATE UNIQUE INDEX UX_PolicyAccountingWorkItem_Source_Type ON Accounting.PolicyAccountingWorkItem(TenantId,PolicyId,PolicyTermId,WorkItemTypeCode) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Accounting.PolicyAccountingWorkItem') AND name=N'IX_PolicyAccountingWorkItem_Queue') CREATE INDEX IX_PolicyAccountingWorkItem_Queue ON Accounting.PolicyAccountingWorkItem(TenantId,QueueCode,StatusCode,DueDateUtc) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.Invoice') AND name=N'UX_BillingInvoice_SourceEvent') EXEC(N'CREATE UNIQUE INDEX UX_BillingInvoice_SourceEvent ON Billing.Invoice(TenantId,SourceEventId) WHERE IsDeleted=0 AND SourceEventId IS NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.InvoiceLine') AND name=N'UX_BillingInvoiceLine_Source_Type') EXEC(N'CREATE UNIQUE INDEX UX_BillingInvoiceLine_Source_Type ON Billing.InvoiceLine(TenantId,SourceEventId,LineTypeCode) WHERE IsDeleted=0 AND SourceEventId IS NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.AgencyBillReceivable') AND name=N'UX_AgencyBillReceivable_SourceEvent') EXEC(N'CREATE UNIQUE INDEX UX_AgencyBillReceivable_SourceEvent ON Billing.AgencyBillReceivable(TenantId,SourceEventId) WHERE IsDeleted=0 AND SourceEventId IS NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Commission.CommissionExpectedReceivable') AND name=N'UX_CommissionExpectedReceivable_PolicyEvent') EXEC(N'CREATE UNIQUE INDEX UX_CommissionExpectedReceivable_PolicyEvent ON Commission.CommissionExpectedReceivable(TenantId,PolicyCreatedEventId) WHERE IsDeleted=0 AND PolicyCreatedEventId IS NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Finance.JournalEntry') AND name=N'UX_JournalEntry_SourceEvent_Type') EXEC(N'CREATE UNIQUE INDEX UX_JournalEntry_SourceEvent_Type ON Finance.JournalEntry(TenantId,SourceEventId,EntryTypeCode) WHERE IsDeleted=0 AND SourceEventId IS NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Finance.Vendor') AND name=N'UX_Vendor_Tenant_Carrier') EXEC(N'CREATE UNIQUE INDEX UX_Vendor_Tenant_Carrier ON Finance.Vendor(TenantId,CarrierId) WHERE IsDeleted=0 AND CarrierId IS NOT NULL;');

DECLARE @Tenants TABLE(TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
IF OBJECT_ID(N'Core.Tenant',N'U') IS NOT NULL INSERT INTO @Tenants SELECT TenantId FROM Core.Tenant WHERE IsDeleted=0;
IF NOT EXISTS(SELECT 1 FROM @Tenants) INSERT INTO @Tenants VALUES('00000000-0000-0000-0000-000000000001');

DECLARE @Options TABLE(OptionGroupCode NVARCHAR(80),OptionCode NVARCHAR(100),DisplayName NVARCHAR(200),Description NVARCHAR(1000),TextValue NVARCHAR(500),NumericValue DECIMAL(18,4),IsDefault BIT,SortOrder INT);
INSERT INTO @Options VALUES
(N'BillingType',N'AgencyBill',N'Agency Bill',N'Agency invoices the insured and manages receivables, carrier remittance, trust and deposits.',NULL,NULL,1,10),
(N'BillingType',N'DirectBill',N'Direct Bill',N'Carrier invoices the insured; agency records expected commission and reconciliation.',NULL,NULL,0,20),
(N'Installment',N'DefaultCount',N'Default installment count',N'Used when the policy payment plan does not provide a supported count.',NULL,1,1,10),
(N'Installment',N'GraceDays',N'Installment grace days',N'Days after an installment due date before delinquency processing.',NULL,10,1,20),
(N'PaymentTerms',N'InsuredDueDays',N'Insured invoice due days',N'Days after policy effective date that an agency-bill invoice is due.',NULL,30,1,10),
(N'PaymentTerms',N'CarrierDueDays',N'Carrier remittance due days',N'Days after policy effective date that carrier premium is due.',NULL,30,1,20),
(N'Trust',N'PremiumTrustRequired',N'Premium trust required',N'Controls whether agency-bill premium receipts are held in premium trust.',N'true',NULL,1,10),
(N'GLAccount',N'AccountsReceivable',N'Accounts Receivable',N'Debit account for agency-bill invoices.',N'1100',NULL,1,10),
(N'GLAccount',N'PremiumPayable',N'Premium Payable',N'Credit account for carrier premium obligations.',N'2100',NULL,1,20),
(N'GLAccount',N'AgencyFeeRevenue',N'Agency Fee Revenue',N'Credit account for agency fees.',N'4100',NULL,1,30),
(N'GLAccount',N'PremiumTaxPayable',N'Premium Tax Payable',N'Credit account for premium and surplus-lines taxes.',N'2200',NULL,1,40),
(N'GLAccount',N'CommissionReceivable',N'Commission Receivable',N'Debit account for direct-bill expected commission.',N'1200',NULL,1,50),
(N'GLAccount',N'CommissionRevenue',N'Commission Revenue',N'Credit account for expected agency commission.',N'4000',NULL,1,60),
(N'GLAccount',N'PremiumTrustCash',N'Premium Trust Cash',N'Cash account used when premium receipts clear.',N'1010',NULL,1,70);

INSERT INTO Accounting.PolicyAccountingOption(PolicyAccountingOptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,TextValue,NumericValue,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),t.TenantId,o.OptionGroupCode,o.OptionCode,o.DisplayName,o.Description,o.TextValue,o.NumericValue,o.IsDefault,1,o.SortOrder,SYSUTCDATETIME(),0
FROM @Tenants t CROSS JOIN @Options o
WHERE NOT EXISTS(SELECT 1 FROM Accounting.PolicyAccountingOption x WHERE x.TenantId=t.TenantId AND x.OptionGroupCode=o.OptionGroupCode AND x.OptionCode=o.OptionCode AND x.IsDeleted=0);

DECLARE @GlAccounts TABLE(AccountCode NVARCHAR(50),AccountName NVARCHAR(200),AccountTypeCode NVARCHAR(50),Description NVARCHAR(500));
INSERT INTO @GlAccounts VALUES
(N'1010',N'Premium Trust Cash',N'Asset',N'Cash held in trust for insured premium obligations.'),
(N'1100',N'Accounts Receivable',N'Asset',N'Amounts due from insureds.'),
(N'1200',N'Commission Receivable',N'Asset',N'Expected commission due from carriers.'),
(N'2100',N'Premium Payable',N'Liability',N'Net premium due to carriers.'),
(N'2200',N'Premium Tax Payable',N'Liability',N'Premium, surplus-lines and related tax obligations.'),
(N'4000',N'Commission Revenue',N'Revenue',N'Agency commission revenue.'),
(N'4100',N'Agency Fee Revenue',N'Revenue',N'Agency fee revenue.');
INSERT INTO Finance.GLAccount(GLAccountId,TenantId,AccountCode,AccountName,AccountTypeCode,Description,IsActive,IsDeleted,CreatedDateUtc)
SELECT NEWID(),t.TenantId,g.AccountCode,g.AccountName,g.AccountTypeCode,g.Description,1,0,SYSUTCDATETIME()
FROM @Tenants t CROSS JOIN @GlAccounts g
WHERE NOT EXISTS(SELECT 1 FROM Finance.GLAccount a WHERE a.TenantId=t.TenantId AND a.AccountCode=g.AccountCode AND a.IsDeleted=0);
