-- 0127_PremiumFinanceManagement.sql
-- Agency-facing Premium Finance Management. Extends the existing Billing.FinanceCompany
-- and Billing.FinanceAgreement foundation and adds tenant-scoped request, option, schedule,
-- provider transaction, document, and activity records. Idempotent.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'PremiumFinanceRequestNumberSequence' AND schema_id = SCHEMA_ID(N'Billing'))
	EXEC(N'CREATE SEQUENCE Billing.PremiumFinanceRequestNumberSequence AS BIGINT START WITH 1 INCREMENT BY 1');

IF OBJECT_ID(N'Billing.PremiumFinanceReferenceOption', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.PremiumFinanceReferenceOption
	(
		PremiumFinanceReferenceOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PremiumFinanceReferenceOption PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		OptionGroupCode NVARCHAR(80) NOT NULL,
		OptionCode NVARCHAR(80) NOT NULL,
		DisplayName NVARCHAR(160) NOT NULL,
		Description NVARCHAR(500) NULL,
		ColorHex NVARCHAR(10) NULL,
		IsTerminal BIT NOT NULL CONSTRAINT DF_PremiumFinanceReferenceOption_Terminal DEFAULT 0,
		IsDefault BIT NOT NULL CONSTRAINT DF_PremiumFinanceReferenceOption_Default DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_PremiumFinanceReferenceOption_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_PremiumFinanceReferenceOption_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PremiumFinanceReferenceOption_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PremiumFinanceReferenceOption_Deleted DEFAULT 0,
		CONSTRAINT UQ_PremiumFinanceReferenceOption_Tenant_Group_Code UNIQUE (TenantId, OptionGroupCode, OptionCode)
	);
END;

IF COL_LENGTH(N'Billing.FinanceCompany', N'ProviderKey') IS NULL ALTER TABLE Billing.FinanceCompany ADD ProviderKey NVARCHAR(100) NULL;
IF COL_LENGTH(N'Billing.FinanceCompany', N'IntegrationLevelCode') IS NULL ALTER TABLE Billing.FinanceCompany ADD IntegrationLevelCode NVARCHAR(50) NOT NULL CONSTRAINT DF_FinanceCompany_IntegrationLevel DEFAULT N'Manual';
IF COL_LENGTH(N'Billing.FinanceCompany', N'WebsiteUrl') IS NULL ALTER TABLE Billing.FinanceCompany ADD WebsiteUrl NVARCHAR(500) NULL;
IF COL_LENGTH(N'Billing.FinanceCompany', N'PortalUrl') IS NULL ALTER TABLE Billing.FinanceCompany ADD PortalUrl NVARCHAR(500) NULL;
IF COL_LENGTH(N'Billing.FinanceCompany', N'SupportsQuotes') IS NULL ALTER TABLE Billing.FinanceCompany ADD SupportsQuotes BIT NOT NULL CONSTRAINT DF_FinanceCompany_SupportsQuotes DEFAULT 1;
IF COL_LENGTH(N'Billing.FinanceCompany', N'SupportsApplications') IS NULL ALTER TABLE Billing.FinanceCompany ADD SupportsApplications BIT NOT NULL CONSTRAINT DF_FinanceCompany_SupportsApplications DEFAULT 1;
IF COL_LENGTH(N'Billing.FinanceCompany', N'SupportsAgreements') IS NULL ALTER TABLE Billing.FinanceCompany ADD SupportsAgreements BIT NOT NULL CONSTRAINT DF_FinanceCompany_SupportsAgreements DEFAULT 1;
IF COL_LENGTH(N'Billing.FinanceCompany', N'SupportsPaymentSchedules') IS NULL ALTER TABLE Billing.FinanceCompany ADD SupportsPaymentSchedules BIT NOT NULL CONSTRAINT DF_FinanceCompany_SupportsSchedules DEFAULT 1;
IF COL_LENGTH(N'Billing.FinanceCompany', N'SupportsAccountStatus') IS NULL ALTER TABLE Billing.FinanceCompany ADD SupportsAccountStatus BIT NOT NULL CONSTRAINT DF_FinanceCompany_SupportsStatus DEFAULT 1;
IF COL_LENGTH(N'Billing.FinanceCompany', N'SupportsPayoff') IS NULL ALTER TABLE Billing.FinanceCompany ADD SupportsPayoff BIT NOT NULL CONSTRAINT DF_FinanceCompany_SupportsPayoff DEFAULT 1;
IF COL_LENGTH(N'Billing.FinanceCompany', N'ExternalProviderId') IS NULL ALTER TABLE Billing.FinanceCompany ADD ExternalProviderId NVARCHAR(160) NULL;

IF OBJECT_ID(N'Billing.PremiumFinanceRequest', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.PremiumFinanceRequest
	(
		PremiumFinanceRequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PremiumFinanceRequest PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		RequestNumber NVARCHAR(50) NOT NULL,
		SourceTypeCode NVARCHAR(40) NOT NULL,
		QuoteId UNIQUEIDENTIFIER NULL,
		PolicyId UNIQUEIDENTIFIER NULL,
		RenewalId UNIQUEIDENTIFIER NULL,
		SubmissionId UNIQUEIDENTIFIER NULL,
		AccountId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		ProducerUserId UNIQUEIDENTIFIER NULL,
		AssignedToUserId UNIQUEIDENTIFIER NULL,
		InsuredName NVARCHAR(200) NOT NULL,
		AgencyName NVARCHAR(200) NULL,
		ProducerName NVARCHAR(200) NULL,
		CarrierName NVARCHAR(200) NULL,
		PolicyOrQuoteNumber NVARCHAR(120) NOT NULL,
		LineOfBusiness NVARCHAR(160) NOT NULL,
		EffectiveDate DATE NOT NULL,
		PremiumAmount DECIMAL(18,2) NOT NULL,
		TaxAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PremiumFinanceRequest_Tax DEFAULT 0,
		FeeAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PremiumFinanceRequest_Fee DEFAULT 0,
		TotalCostAmount AS (PremiumAmount + TaxAmount + FeeAmount) PERSISTED,
		RequestedDownPaymentAmount DECIMAL(18,2) NULL,
		RequestedInstallmentCount INT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PremiumFinanceRequest_Status DEFAULT N'Draft',
		PreferredFinanceCompanyId UNIQUEIDENTIFIER NULL,
		SelectedQuoteOptionId UNIQUEIDENTIFIER NULL,
		CustomerEmail NVARCHAR(254) NULL,
		CustomerPhone NVARCHAR(50) NULL,
		Notes NVARCHAR(2000) NULL,
		RequestedDateUtc DATETIME2 NULL,
		SubmittedDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		CancelledDateUtc DATETIME2 NULL,
		CancellationReason NVARCHAR(1000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PremiumFinanceRequest_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PremiumFinanceRequest_Deleted DEFAULT 0,
		CONSTRAINT CK_PremiumFinanceRequest_Amounts CHECK (PremiumAmount >= 0 AND TaxAmount >= 0 AND FeeAmount >= 0),
		CONSTRAINT CK_PremiumFinanceRequest_Installments CHECK (RequestedInstallmentCount IS NULL OR RequestedInstallmentCount BETWEEN 1 AND 120)
	);
END;

IF OBJECT_ID(N'Billing.PremiumFinanceQuoteOption', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.PremiumFinanceQuoteOption
	(
		PremiumFinanceQuoteOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PremiumFinanceQuoteOption PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PremiumFinanceRequestId UNIQUEIDENTIFIER NOT NULL,
		FinanceCompanyId UNIQUEIDENTIFIER NOT NULL,
		ProviderQuoteReference NVARCHAR(160) NULL,
		OptionName NVARCHAR(160) NOT NULL,
		DownPaymentPercent DECIMAL(9,4) NOT NULL,
		DownPaymentAmount DECIMAL(18,2) NOT NULL,
		AmountFinanced DECIMAL(18,2) NOT NULL,
		AprPercent DECIMAL(9,4) NOT NULL,
		FinanceChargeAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PremiumFinanceQuoteOption_Charge DEFAULT 0,
		PaymentCount INT NOT NULL,
		PaymentAmount DECIMAL(18,2) NOT NULL,
		FirstPaymentDate DATE NULL,
		QuoteExpirationDate DATE NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PremiumFinanceQuoteOption_Status DEFAULT N'Received',
		TermsSummary NVARCHAR(2000) NULL,
		IsSelected BIT NOT NULL CONSTRAINT DF_PremiumFinanceQuoteOption_Selected DEFAULT 0,
		SelectedDateUtc DATETIME2 NULL,
		SelectedByUserId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PremiumFinanceQuoteOption_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PremiumFinanceQuoteOption_Deleted DEFAULT 0,
		CONSTRAINT FK_PremiumFinanceQuoteOption_Request FOREIGN KEY (PremiumFinanceRequestId) REFERENCES Billing.PremiumFinanceRequest(PremiumFinanceRequestId),
		CONSTRAINT FK_PremiumFinanceQuoteOption_Company FOREIGN KEY (FinanceCompanyId) REFERENCES Billing.FinanceCompany(FinanceCompanyId),
		CONSTRAINT CK_PremiumFinanceQuoteOption_Terms CHECK (DownPaymentPercent BETWEEN 0 AND 100 AND DownPaymentAmount >= 0 AND AmountFinanced >= 0 AND AprPercent >= 0 AND PaymentCount BETWEEN 1 AND 120 AND PaymentAmount >= 0)
	);
END;

IF COL_LENGTH(N'Billing.FinanceAgreement', N'PremiumFinanceRequestId') IS NULL ALTER TABLE Billing.FinanceAgreement ADD PremiumFinanceRequestId UNIQUEIDENTIFIER NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Billing.FinanceAgreement') AND name = N'AgencyBillReceivableId' AND is_nullable = 0) ALTER TABLE Billing.FinanceAgreement ALTER COLUMN AgencyBillReceivableId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'PremiumFinanceQuoteOptionId') IS NULL ALTER TABLE Billing.FinanceAgreement ADD PremiumFinanceQuoteOptionId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'PolicyId') IS NULL ALTER TABLE Billing.FinanceAgreement ADD PolicyId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'QuoteId') IS NULL ALTER TABLE Billing.FinanceAgreement ADD QuoteId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'AccountId') IS NULL ALTER TABLE Billing.FinanceAgreement ADD AccountId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'OriginalPremiumAmount') IS NULL ALTER TABLE Billing.FinanceAgreement ADD OriginalPremiumAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'TaxAndFeeAmount') IS NULL ALTER TABLE Billing.FinanceAgreement ADD TaxAndFeeAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'AprPercent') IS NULL ALTER TABLE Billing.FinanceAgreement ADD AprPercent DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'FinanceChargeAmount') IS NULL ALTER TABLE Billing.FinanceAgreement ADD FinanceChargeAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'PaymentCount') IS NULL ALTER TABLE Billing.FinanceAgreement ADD PaymentCount INT NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'PaymentAmount') IS NULL ALTER TABLE Billing.FinanceAgreement ADD PaymentAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'NextPaymentDate') IS NULL ALTER TABLE Billing.FinanceAgreement ADD NextPaymentDate DATE NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'ApplicationStatusCode') IS NULL ALTER TABLE Billing.FinanceAgreement ADD ApplicationStatusCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'SignatureStatusCode') IS NULL ALTER TABLE Billing.FinanceAgreement ADD SignatureStatusCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'AccountStatusCode') IS NULL ALTER TABLE Billing.FinanceAgreement ADD AccountStatusCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'ProviderApplicationReference') IS NULL ALTER TABLE Billing.FinanceAgreement ADD ProviderApplicationReference NVARCHAR(160) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'DocumentId') IS NULL ALTER TABLE Billing.FinanceAgreement ADD DocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'ESignEnvelopeId') IS NULL ALTER TABLE Billing.FinanceAgreement ADD ESignEnvelopeId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'ApprovedDateUtc') IS NULL ALTER TABLE Billing.FinanceAgreement ADD ApprovedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'ActivatedDateUtc') IS NULL ALTER TABLE Billing.FinanceAgreement ADD ActivatedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'LastSynchronizedDateUtc') IS NULL ALTER TABLE Billing.FinanceAgreement ADD LastSynchronizedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'PayoffAmount') IS NULL ALTER TABLE Billing.FinanceAgreement ADD PayoffAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Billing.FinanceAgreement', N'PayoffGoodThroughDate') IS NULL ALTER TABLE Billing.FinanceAgreement ADD PayoffGoodThroughDate DATE NULL;

IF OBJECT_ID(N'Billing.PremiumFinancePaymentSchedule', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.PremiumFinancePaymentSchedule
	(
		PremiumFinancePaymentScheduleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PremiumFinancePaymentSchedule PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		FinanceAgreementId UNIQUEIDENTIFIER NOT NULL,
		InstallmentNumber INT NOT NULL,
		DueDate DATE NOT NULL,
		ScheduledAmount DECIMAL(18,2) NOT NULL,
		PrincipalAmount DECIMAL(18,2) NULL,
		FinanceChargeAmount DECIMAL(18,2) NULL,
		PaidAmount DECIMAL(18,2) NULL,
		PaidDate DATE NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PremiumFinancePaymentSchedule_Status DEFAULT N'Scheduled',
		ProviderPaymentReference NVARCHAR(160) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PremiumFinancePaymentSchedule_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PremiumFinancePaymentSchedule_Deleted DEFAULT 0,
		CONSTRAINT FK_PremiumFinancePaymentSchedule_Agreement FOREIGN KEY (FinanceAgreementId) REFERENCES Billing.FinanceAgreement(FinanceAgreementId),
		CONSTRAINT CK_PremiumFinancePaymentSchedule_Amounts CHECK (InstallmentNumber > 0 AND ScheduledAmount >= 0 AND (PaidAmount IS NULL OR PaidAmount >= 0))
	);
END;

IF OBJECT_ID(N'Billing.PremiumFinanceActivity', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.PremiumFinanceActivity
	(
		PremiumFinanceActivityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PremiumFinanceActivity PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PremiumFinanceRequestId UNIQUEIDENTIFIER NULL,
		FinanceAgreementId UNIQUEIDENTIFIER NULL,
		ActivityTypeCode NVARCHAR(80) NOT NULL,
		Subject NVARCHAR(200) NOT NULL,
		Notes NVARCHAR(2000) NULL,
		OldStatusCode NVARCHAR(50) NULL,
		NewStatusCode NVARCHAR(50) NULL,
		ProviderReference NVARCHAR(160) NULL,
		ActivityDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PremiumFinanceActivity_Date DEFAULT SYSUTCDATETIME(),
		CreatedByName NVARCHAR(200) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PremiumFinanceActivity_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PremiumFinanceActivity_Deleted DEFAULT 0,
		CONSTRAINT CK_PremiumFinanceActivity_Parent CHECK (PremiumFinanceRequestId IS NOT NULL OR FinanceAgreementId IS NOT NULL)
	);
END;

IF OBJECT_ID(N'Billing.PremiumFinanceDocument', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.PremiumFinanceDocument
	(
		PremiumFinanceDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PremiumFinanceDocument PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PremiumFinanceRequestId UNIQUEIDENTIFIER NULL,
		FinanceAgreementId UNIQUEIDENTIFIER NULL,
		DocumentId UNIQUEIDENTIFIER NOT NULL,
		DocumentRoleCode NVARCHAR(80) NOT NULL,
		IsCurrent BIT NOT NULL CONSTRAINT DF_PremiumFinanceDocument_Current DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PremiumFinanceDocument_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PremiumFinanceDocument_Deleted DEFAULT 0,
		CONSTRAINT CK_PremiumFinanceDocument_Parent CHECK (PremiumFinanceRequestId IS NOT NULL OR FinanceAgreementId IS NOT NULL)
	);
END;

IF OBJECT_ID(N'Billing.PremiumFinanceProviderTransaction', N'U') IS NULL
BEGIN
	CREATE TABLE Billing.PremiumFinanceProviderTransaction
	(
		PremiumFinanceProviderTransactionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PremiumFinanceProviderTransaction PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		FinanceCompanyId UNIQUEIDENTIFIER NOT NULL,
		PremiumFinanceRequestId UNIQUEIDENTIFIER NULL,
		FinanceAgreementId UNIQUEIDENTIFIER NULL,
		OperationCode NVARCHAR(80) NOT NULL,
		CorrelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PremiumFinanceProviderTransaction_Correlation DEFAULT NEWID(),
		ExternalTransactionId NVARCHAR(160) NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		RequestPayloadJson NVARCHAR(MAX) NULL,
		ResponsePayloadJson NVARCHAR(MAX) NULL,
		AttemptCount INT NOT NULL CONSTRAINT DF_PremiumFinanceProviderTransaction_Attempts DEFAULT 1,
		ErrorDetails NVARCHAR(4000) NULL,
		CompletedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PremiumFinanceProviderTransaction_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PremiumFinanceProviderTransaction_Deleted DEFAULT 0,
		CONSTRAINT CK_PremiumFinanceProviderTransaction_RequestJson CHECK (RequestPayloadJson IS NULL OR ISJSON(RequestPayloadJson) = 1),
		CONSTRAINT CK_PremiumFinanceProviderTransaction_ResponseJson CHECK (ResponsePayloadJson IS NULL OR ISJSON(ResponsePayloadJson) = 1)
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinanceRequest') AND name = N'UX_PremiumFinanceRequest_Tenant_Number') CREATE UNIQUE INDEX UX_PremiumFinanceRequest_Tenant_Number ON Billing.PremiumFinanceRequest(TenantId, RequestNumber) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinanceRequest') AND name = N'IX_PremiumFinanceRequest_Workbench') CREATE INDEX IX_PremiumFinanceRequest_Workbench ON Billing.PremiumFinanceRequest(TenantId, StatusCode, EffectiveDate) INCLUDE(AccountId, PolicyId, QuoteId, PremiumAmount, SelectedQuoteOptionId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinanceRequest') AND name = N'IX_PremiumFinanceRequest_Account') CREATE INDEX IX_PremiumFinanceRequest_Account ON Billing.PremiumFinanceRequest(TenantId, AccountId, CreatedDateUtc DESC) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinanceQuoteOption') AND name = N'IX_PremiumFinanceQuoteOption_Request') CREATE INDEX IX_PremiumFinanceQuoteOption_Request ON Billing.PremiumFinanceQuoteOption(TenantId, PremiumFinanceRequestId, IsSelected, StatusCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinanceQuoteOption') AND name = N'UX_PremiumFinanceQuoteOption_Selected') CREATE UNIQUE INDEX UX_PremiumFinanceQuoteOption_Selected ON Billing.PremiumFinanceQuoteOption(PremiumFinanceRequestId) WHERE IsSelected = 1 AND IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.FinanceAgreement') AND name = N'IX_FinanceAgreement_PremiumFinanceRequest') EXEC(N'CREATE INDEX IX_FinanceAgreement_PremiumFinanceRequest ON Billing.FinanceAgreement(TenantId, PremiumFinanceRequestId, StatusCode) WHERE PremiumFinanceRequestId IS NOT NULL AND IsDeleted = 0');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.FinanceAgreement') AND name = N'IX_FinanceAgreement_Policy') EXEC(N'CREATE INDEX IX_FinanceAgreement_Policy ON Billing.FinanceAgreement(TenantId, PolicyId, StatusCode) WHERE PolicyId IS NOT NULL AND IsDeleted = 0');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinancePaymentSchedule') AND name = N'UX_PremiumFinancePaymentSchedule_Agreement_Number') CREATE UNIQUE INDEX UX_PremiumFinancePaymentSchedule_Agreement_Number ON Billing.PremiumFinancePaymentSchedule(FinanceAgreementId, InstallmentNumber) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinancePaymentSchedule') AND name = N'IX_PremiumFinancePaymentSchedule_Due') CREATE INDEX IX_PremiumFinancePaymentSchedule_Due ON Billing.PremiumFinancePaymentSchedule(TenantId, StatusCode, DueDate) INCLUDE(FinanceAgreementId, ScheduledAmount, PaidAmount) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinanceActivity') AND name = N'IX_PremiumFinanceActivity_Request') CREATE INDEX IX_PremiumFinanceActivity_Request ON Billing.PremiumFinanceActivity(TenantId, PremiumFinanceRequestId, ActivityDateUtc DESC) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.PremiumFinanceProviderTransaction') AND name = N'UX_PremiumFinanceProviderTransaction_Correlation') CREATE UNIQUE INDEX UX_PremiumFinanceProviderTransaction_Correlation ON Billing.PremiumFinanceProviderTransaction(TenantId, CorrelationId) WHERE IsDeleted = 0;

;WITH OptionSeed AS
(
	SELECT * FROM (VALUES
		(N'IntegrationLevel', N'Manual', N'Manual PFC', N'Terms and servicing updates are recorded by agency users.', N'#6c757d', 0, 1, 10),
		(N'IntegrationLevel', N'Assisted', N'Assisted Integration', N'AgencyBinder prepares application packages while communication may occur externally.', N'#0dcaf0', 0, 0, 20),
		(N'IntegrationLevel', N'Api', N'Full API', N'Provider supports automated quote, application, agreement, and servicing synchronization.', N'#198754', 0, 0, 30),
		(N'RequestStatus', N'Draft', N'Draft', N'Finance request is being prepared.', N'#6c757d', 0, 1, 10),
		(N'RequestStatus', N'OptionsRequested', N'Options Requested', N'Financing options have been requested from one or more providers.', N'#0d6efd', 0, 0, 20),
		(N'RequestStatus', N'OptionsReceived', N'Options Received', N'Provider financing options are available for comparison.', N'#0dcaf0', 0, 0, 30),
		(N'RequestStatus', N'OptionSelected', N'Option Selected', N'A financing option has been selected.', N'#6f42c1', 0, 0, 40),
		(N'RequestStatus', N'ApplicationSubmitted', N'Application Submitted', N'The financing application was submitted to the provider.', N'#fd7e14', 0, 0, 50),
		(N'RequestStatus', N'PendingSignature', N'Pending Signature', N'The agreement is awaiting customer signature.', N'#ffc107', 0, 0, 60),
		(N'RequestStatus', N'PendingApproval', N'Pending PFC Approval', N'The signed agreement is awaiting provider approval.', N'#fd7e14', 0, 0, 70),
		(N'RequestStatus', N'Approved', N'Approved', N'The provider approved financing.', N'#20c997', 0, 0, 80),
		(N'RequestStatus', N'Active', N'Active', N'Financing is active and linked to the policy.', N'#198754', 1, 0, 90),
		(N'RequestStatus', N'Declined', N'Declined', N'The provider declined the financing application.', N'#dc3545', 1, 0, 100),
		(N'RequestStatus', N'Cancelled', N'Cancelled', N'The finance request was cancelled.', N'#6c757d', 1, 0, 110),
		(N'QuoteOptionStatus', N'Received', N'Received', N'Provider terms were received.', N'#0dcaf0', 0, 1, 10),
		(N'QuoteOptionStatus', N'Selected', N'Selected', N'This option was selected.', N'#198754', 1, 0, 20),
		(N'QuoteOptionStatus', N'NotSelected', N'Not Selected', N'Another option was selected.', N'#6c757d', 1, 0, 30),
		(N'QuoteOptionStatus', N'Expired', N'Expired', N'The provider terms expired.', N'#dc3545', 1, 0, 40),
		(N'SignatureStatus', N'NotSent', N'Not Sent', N'Agreement has not been sent for signature.', N'#6c757d', 0, 1, 10),
		(N'SignatureStatus', N'Sent', N'Sent', N'Agreement was sent to the customer.', N'#0d6efd', 0, 0, 20),
		(N'SignatureStatus', N'Delivered', N'Delivered', N'Customer received the agreement.', N'#0dcaf0', 0, 0, 30),
		(N'SignatureStatus', N'Signed', N'Signed', N'Customer completed the agreement.', N'#198754', 1, 0, 40),
		(N'SignatureStatus', N'Declined', N'Declined', N'Customer declined to sign.', N'#dc3545', 1, 0, 50),
		(N'AgreementStatus', N'Pending', N'Pending', N'Agreement is being finalized.', N'#ffc107', 0, 1, 10),
		(N'AgreementStatus', N'Active', N'Active', N'Agreement is approved and active.', N'#198754', 0, 0, 20),
		(N'AgreementStatus', N'PaidOff', N'Paid Off', N'Provider reports the financed balance paid off.', N'#20c997', 1, 0, 30),
		(N'AgreementStatus', N'Cancelled', N'Cancelled', N'Provider reports the agreement cancelled.', N'#6c757d', 1, 0, 40),
		(N'AccountStatus', N'Current', N'Current', N'Provider reports the account current.', N'#198754', 0, 1, 10),
		(N'AccountStatus', N'PastDue', N'Past Due', N'Provider reports a past-due installment requiring attention.', N'#dc3545', 0, 0, 20),
		(N'AccountStatus', N'CancellationPending', N'Cancellation Pending', N'Provider reports cancellation action pending.', N'#dc3545', 0, 0, 30),
		(N'AccountStatus', N'Cancelled', N'Cancelled', N'Provider reports financing cancelled.', N'#6c757d', 1, 0, 40),
		(N'AccountStatus', N'PaidOff', N'Paid Off', N'Provider reports the account paid off.', N'#20c997', 1, 0, 50),
		(N'PaymentStatus', N'Scheduled', N'Scheduled', N'Installment is scheduled by the provider.', N'#0d6efd', 0, 1, 10),
		(N'PaymentStatus', N'Due', N'Due', N'Installment is currently due.', N'#fd7e14', 0, 0, 20),
		(N'PaymentStatus', N'Paid', N'Paid', N'Provider reports the installment paid.', N'#198754', 1, 0, 30),
		(N'PaymentStatus', N'PastDue', N'Past Due', N'Provider reports the installment past due.', N'#dc3545', 0, 0, 40),
		(N'DeliveryMethod', N'Email', N'Email', N'Send using agency email workflow.', N'#0d6efd', 0, 1, 10),
		(N'DeliveryMethod', N'ESign', N'E-Signature', N'Send using the configured e-sign provider.', N'#6f42c1', 0, 0, 20),
		(N'DeliveryMethod', N'ProviderPortal', N'Provider Portal', N'Complete delivery in the provider portal.', N'#0dcaf0', 0, 0, 30),
		(N'ActivityType', N'Note', N'Note', N'General premium finance note.', N'#6c757d', 0, 1, 10),
		(N'ActivityType', N'StatusChanged', N'Status Changed', N'Workflow status changed.', N'#0d6efd', 0, 0, 20),
		(N'ActivityType', N'ProviderContact', N'Provider Contact', N'Agency contacted the premium finance provider.', N'#0dcaf0', 0, 0, 30),
		(N'ActivityType', N'CustomerContact', N'Customer Contact', N'Agency contacted the customer.', N'#6f42c1', 0, 0, 40),
		(N'ActivityType', N'Document', N'Document', N'A premium finance document was created or received.', N'#198754', 0, 0, 50)
	) seed(OptionGroupCode, OptionCode, DisplayName, Description, ColorHex, IsTerminal, IsDefault, SortOrder)
)
INSERT INTO Billing.PremiumFinanceReferenceOption(TenantId, OptionGroupCode, OptionCode, DisplayName, Description, ColorHex, IsTerminal, IsDefault, SortOrder)
SELECT tenant.TenantId, seed.OptionGroupCode, seed.OptionCode, seed.DisplayName, seed.Description, seed.ColorHex, seed.IsTerminal, seed.IsDefault, seed.SortOrder
FROM Core.Tenant tenant
CROSS JOIN OptionSeed seed
WHERE tenant.IsDeleted = 0
  AND NOT EXISTS
  (
	SELECT 1 FROM Billing.PremiumFinanceReferenceOption existing
	WHERE existing.TenantId = tenant.TenantId
	  AND existing.OptionGroupCode = seed.OptionGroupCode
	  AND existing.OptionCode = seed.OptionCode
  );
