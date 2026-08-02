SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Policy') EXEC(N'CREATE SCHEMA Policy');
GO

IF OBJECT_ID(N'Submissions.BoundPolicy', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Submissions.BoundPolicy', N'CurrentPolicyVersionId') IS NULL ALTER TABLE Submissions.BoundPolicy ADD CurrentPolicyVersionId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Submissions.BoundPolicy', N'CurrentVersionNumber') IS NULL ALTER TABLE Submissions.BoundPolicy ADD CurrentVersionNumber INT NOT NULL CONSTRAINT DF_BoundPolicy_CurrentVersion_0073 DEFAULT 1;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsement', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'PolicyVersionBeforeId') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD PolicyVersionBeforeId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'PolicyVersionAfterId') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD PolicyVersionAfterId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'ReasonCode') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD ReasonCode NVARCHAR(80) NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'CarrierMethodCode') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD CarrierMethodCode NVARCHAR(50) NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'CurrencyCode') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_PolicyEndorsement_Currency_0073 DEFAULT N'USD';
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'AgencyFeeDelta') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD AgencyFeeDelta DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyEndorsement_AgencyFee_0073 DEFAULT 0;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'TaxDelta') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD TaxDelta DECIMAL(18,2) NOT NULL CONSTRAINT DF_PolicyEndorsement_Tax_0073 DEFAULT 0;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'SubmittedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD SubmittedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'CompletedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD CompletedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'RejectedDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD RejectedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'CancelledDateUtc') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD CancelledDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'ReversalOfEndorsementId') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD ReversalOfEndorsementId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'ReversedByEndorsementId') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD ReversedByEndorsementId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Policy.PolicyEndorsement', N'RowVersion') IS NULL ALTER TABLE Policy.PolicyEndorsement ADD RowVersion ROWVERSION;

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Policy.PolicyEndorsement') AND name=N'UX_PolicyEndorsement_TenantId')
		CREATE UNIQUE INDEX UX_PolicyEndorsement_TenantId ON Policy.PolicyEndorsement(TenantId, EndorsementId);
	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Policy.PolicyEndorsement') AND name=N'IX_PolicyEndorsement_PolicyWorkflow')
		CREATE INDEX IX_PolicyEndorsement_PolicyWorkflow ON Policy.PolicyEndorsement(TenantId, PolicyId, Status, EffectiveDate DESC) INCLUDE (EndorsementNumber, PolicyVersionBeforeId, PolicyVersionAfterId, PremiumDelta, TotalCostDelta) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementChange PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementId UNIQUEIDENTIFIER NOT NULL,
		CategoryCode NVARCHAR(50) NOT NULL,
		OperationCode NVARCHAR(50) NOT NULL,
		EntityKey NVARCHAR(200) NULL,
		SequenceNumber INT NOT NULL CONSTRAINT DF_PolicyEndorsementChange_Sequence DEFAULT 1,
		Summary NVARCHAR(500) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyEndorsementChange_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyEndorsementChange_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementChange_TenantId ON Policy.PolicyEndorsementChange(TenantId, ChangeId);
	CREATE UNIQUE INDEX UX_PolicyEndorsementChange_Sequence ON Policy.PolicyEndorsementChange(TenantId, EndorsementId, SequenceNumber) WHERE IsDeleted=0;
	CREATE INDEX IX_PolicyEndorsementChange_Endorsement ON Policy.PolicyEndorsementChange(TenantId, EndorsementId, CategoryCode, IsDeleted);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementInsuredChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementInsuredChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementInsuredChange PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		BeforeName NVARCHAR(240) NULL, AfterName NVARCHAR(240) NULL,
		BeforeDba NVARCHAR(240) NULL, AfterDba NVARCHAR(240) NULL,
		BeforeFein NVARCHAR(30) NULL, AfterFein NVARCHAR(30) NULL,
		BeforePhone NVARCHAR(40) NULL, AfterPhone NVARCHAR(40) NULL,
		BeforeEmail NVARCHAR(254) NULL, AfterEmail NVARCHAR(254) NULL,
		BeforeMailingAddress NVARCHAR(1000) NULL, AfterMailingAddress NVARCHAR(1000) NULL,
		BeforeGaragingAddress NVARCHAR(1000) NULL, AfterGaragingAddress NVARCHAR(1000) NULL
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementInsuredChange_Tenant ON Policy.PolicyEndorsementInsuredChange(TenantId, ChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementVehicleChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementVehicleChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementVehicleChange PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		BeforeVehicleId UNIQUEIDENTIFIER NULL, AfterVehicleId UNIQUEIDENTIFIER NULL,
		BeforeVin NVARCHAR(50) NULL, AfterVin NVARCHAR(50) NULL,
		BeforeYear INT NULL, AfterYear INT NULL,
		BeforeMake NVARCHAR(100) NULL, AfterMake NVARCHAR(100) NULL,
		BeforeModel NVARCHAR(100) NULL, AfterModel NVARCHAR(100) NULL,
		BeforeUsageCode NVARCHAR(80) NULL, AfterUsageCode NVARCHAR(80) NULL,
		BeforeGaragingAddress NVARCHAR(1000) NULL, AfterGaragingAddress NVARCHAR(1000) NULL,
		BeforeLienholder NVARCHAR(240) NULL, AfterLienholder NVARCHAR(240) NULL
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementVehicleChange_Tenant ON Policy.PolicyEndorsementVehicleChange(TenantId, ChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementDriverChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementDriverChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementDriverChange PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		BeforeDriverId UNIQUEIDENTIFIER NULL, AfterDriverId UNIQUEIDENTIFIER NULL,
		BeforeName NVARCHAR(240) NULL, AfterName NVARCHAR(240) NULL,
		BeforeLicenseNumber NVARCHAR(100) NULL, AfterLicenseNumber NVARCHAR(100) NULL,
		BeforeLicenseState NVARCHAR(10) NULL, AfterLicenseState NVARCHAR(10) NULL,
		BeforeBirthDate DATE NULL, AfterBirthDate DATE NULL,
		BeforeExcluded BIT NULL, AfterExcluded BIT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementDriverChange_Tenant ON Policy.PolicyEndorsementDriverChange(TenantId, ChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementCoverageChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementCoverageChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementCoverageChange PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CoverageCode NVARCHAR(100) NULL,
		BeforeCoverageName NVARCHAR(240) NULL, AfterCoverageName NVARCHAR(240) NULL,
		BeforeLimitAmount DECIMAL(18,2) NULL, AfterLimitAmount DECIMAL(18,2) NULL,
		BeforeLimitDescription NVARCHAR(500) NULL, AfterLimitDescription NVARCHAR(500) NULL,
		BeforeDeductibleAmount DECIMAL(18,2) NULL, AfterDeductibleAmount DECIMAL(18,2) NULL,
		BeforePremiumAmount DECIMAL(18,2) NULL, AfterPremiumAmount DECIMAL(18,2) NULL
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementCoverageChange_Tenant ON Policy.PolicyEndorsementCoverageChange(TenantId, ChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementPropertyChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementPropertyChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementPropertyChange PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		BeforePropertyId UNIQUEIDENTIFIER NULL, AfterPropertyId UNIQUEIDENTIFIER NULL,
		BeforeLocationAddress NVARCHAR(1000) NULL, AfterLocationAddress NVARCHAR(1000) NULL,
		BeforeBuildingNumber NVARCHAR(80) NULL, AfterBuildingNumber NVARCHAR(80) NULL,
		BeforeOccupancyCode NVARCHAR(100) NULL, AfterOccupancyCode NVARCHAR(100) NULL,
		BeforeConstructionCode NVARCHAR(100) NULL, AfterConstructionCode NVARCHAR(100) NULL,
		BeforeSquareFeet INT NULL, AfterSquareFeet INT NULL,
		BeforeBuildingValue DECIMAL(18,2) NULL, AfterBuildingValue DECIMAL(18,2) NULL
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementPropertyChange_Tenant ON Policy.PolicyEndorsementPropertyChange(TenantId, ChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementCommercialChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementCommercialChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementCommercialChange PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ClassificationCode NVARCHAR(100) NULL,
		BeforePayrollAmount DECIMAL(18,2) NULL, AfterPayrollAmount DECIMAL(18,2) NULL,
		BeforeRevenueAmount DECIMAL(18,2) NULL, AfterRevenueAmount DECIMAL(18,2) NULL,
		BeforeEmployeeCount INT NULL, AfterEmployeeCount INT NULL,
		BeforeEquipmentValue DECIMAL(18,2) NULL, AfterEquipmentValue DECIMAL(18,2) NULL,
		BeforeBlanketLimit DECIMAL(18,2) NULL, AfterBlanketLimit DECIMAL(18,2) NULL,
		BeforeLocationCount INT NULL, AfterLocationCount INT NULL
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementCommercialChange_Tenant ON Policy.PolicyEndorsementCommercialChange(TenantId, ChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementFinancialChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementFinancialChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementFinancialChange PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		BeforeBillingPlanCode NVARCHAR(100) NULL, AfterBillingPlanCode NVARCHAR(100) NULL,
		BeforeFinancingProvider NVARCHAR(240) NULL, AfterFinancingProvider NVARCHAR(240) NULL,
		BeforeInstallmentCount INT NULL, AfterInstallmentCount INT NULL,
		BeforeCommissionRate DECIMAL(9,4) NULL, AfterCommissionRate DECIMAL(9,4) NULL,
		BeforeCommissionAmount DECIMAL(18,2) NULL, AfterCommissionAmount DECIMAL(18,2) NULL,
		BeforeFinancedAmount DECIMAL(18,2) NULL, AfterFinancedAmount DECIMAL(18,2) NULL
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementFinancialChange_Tenant ON Policy.PolicyEndorsementFinancialChange(TenantId, ChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementLegalChange', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementLegalChange
	(
		ChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementLegalChange PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PartyTypeCode NVARCHAR(100) NOT NULL,
		BeforePartyName NVARCHAR(240) NULL, AfterPartyName NVARCHAR(240) NULL,
		BeforeRelationshipCode NVARCHAR(100) NULL, AfterRelationshipCode NVARCHAR(100) NULL,
		BeforeAddress NVARCHAR(1000) NULL, AfterAddress NVARCHAR(1000) NULL,
		BeforeReferenceNumber NVARCHAR(100) NULL, AfterReferenceNumber NVARCHAR(100) NULL
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementLegalChange_Tenant ON Policy.PolicyEndorsementLegalChange(TenantId, ChangeId);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementStatusTransition', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementStatusTransition
	(
		StatusTransitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementStatusTransition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		FromStatusCode NVARCHAR(80) NOT NULL,
		ToStatusCode NVARCHAR(80) NOT NULL,
		RequiredPermissionCode NVARCHAR(120) NULL,
		RequiresApproval BIT NOT NULL CONSTRAINT DF_EndorsementTransition_Approval DEFAULT 0,
		RequiresCarrierSubmission BIT NOT NULL CONSTRAINT DF_EndorsementTransition_Carrier DEFAULT 0,
		CreatesPolicyVersion BIT NOT NULL CONSTRAINT DF_EndorsementTransition_Version DEFAULT 0,
		CreatesAccountingWork BIT NOT NULL CONSTRAINT DF_EndorsementTransition_Accounting DEFAULT 0,
		CreatesDocumentWork BIT NOT NULL CONSTRAINT DF_EndorsementTransition_Documents DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementTransition_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementTransition_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementTransition_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementTransition_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_EndorsementTransition_TenantStatus ON Policy.PolicyEndorsementStatusTransition(TenantId, FromStatusCode, ToStatusCode) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementApproval', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementApproval
	(
		ApprovalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementApproval PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementId UNIQUEIDENTIFIER NOT NULL,
		ApprovalLevelCode NVARCHAR(80) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_EndorsementApproval_Status DEFAULT N'Pending',
		RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementApproval_Requested DEFAULT SYSUTCDATETIME(),
		RequestedByUserId UNIQUEIDENTIFIER NULL,
		AssignedToUserId UNIQUEIDENTIFIER NULL,
		DecidedDateUtc DATETIME2 NULL,
		DecidedByUserId UNIQUEIDENTIFIER NULL,
		DecisionNotes NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementApproval_Created DEFAULT SYSUTCDATETIME(),
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementApproval_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_EndorsementApproval_Active ON Policy.PolicyEndorsementApproval(TenantId, EndorsementId, ApprovalLevelCode) WHERE IsDeleted=0;
	CREATE INDEX IX_EndorsementApproval_Assignee ON Policy.PolicyEndorsementApproval(TenantId, AssignedToUserId, StatusCode, RequestedDateUtc) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementEvent', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementEvent
	(
		EventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementEvent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		EventTypeCode NVARCHAR(100) NOT NULL,
		FromStatusCode NVARCHAR(80) NULL,
		ToStatusCode NVARCHAR(80) NULL,
		Description NVARCHAR(1000) NOT NULL,
		DataJson NVARCHAR(MAX) NULL,
		CorrelationId UNIQUEIDENTIFIER NOT NULL,
		OccurredDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementEvent_Occurred DEFAULT SYSUTCDATETIME(),
		ActorUserId UNIQUEIDENTIFIER NULL,
		CONSTRAINT CK_EndorsementEvent_DataJson CHECK (DataJson IS NULL OR ISJSON(DataJson)=1)
	);
	CREATE UNIQUE INDEX UX_EndorsementEvent_TenantId ON Policy.PolicyEndorsementEvent(TenantId, EventId);
	CREATE INDEX IX_EndorsementEvent_Timeline ON Policy.PolicyEndorsementEvent(TenantId, PolicyId, OccurredDateUtc DESC) INCLUDE (EndorsementId, EventTypeCode, ToStatusCode);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementCarrierConfiguration', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementCarrierConfiguration
	(
		CarrierConfigurationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementCarrierConfiguration PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		LineOfBusiness NVARCHAR(160) NULL,
		ChannelCode NVARCHAR(50) NOT NULL,
		EndpointUri NVARCHAR(1000) NULL,
		HttpMethod NVARCHAR(20) NULL,
		AuthenticationTypeCode NVARCHAR(50) NULL,
		SecretReference NVARCHAR(500) NULL,
		SenderAddress NVARCHAR(254) NULL,
		RecipientAddress NVARCHAR(500) NULL,
		PortalInstructions NVARCHAR(2000) NULL,
		PayloadTemplate NVARCHAR(MAX) NULL,
		HeaderTemplate NVARCHAR(MAX) NULL,
		MaxAttempts INT NOT NULL CONSTRAINT DF_EndorsementCarrierConfiguration_Attempts DEFAULT 5,
		TimeoutSeconds INT NOT NULL CONSTRAINT DF_EndorsementCarrierConfiguration_Timeout DEFAULT 100,
		IsConfigured BIT NOT NULL CONSTRAINT DF_EndorsementCarrierConfiguration_Configured DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementCarrierConfiguration_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementCarrierConfiguration_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementCarrierConfiguration_Deleted DEFAULT 0,
		CONSTRAINT CK_EndorsementCarrierConfiguration_Payload CHECK (PayloadTemplate IS NULL OR ISJSON(PayloadTemplate)=1),
		CONSTRAINT CK_EndorsementCarrierConfiguration_Headers CHECK (HeaderTemplate IS NULL OR ISJSON(HeaderTemplate)=1)
	);
	CREATE INDEX IX_EndorsementCarrierConfiguration_Lookup ON Policy.PolicyEndorsementCarrierConfiguration(TenantId, CarrierId, LineOfBusiness, ChannelCode, IsActive, IsDeleted);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementCarrierDispatch', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementCarrierDispatch
	(
		CarrierDispatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementCarrierDispatch PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementId UNIQUEIDENTIFIER NOT NULL,
		CarrierConfigurationId UNIQUEIDENTIFIER NULL,
		ChannelCode NVARCHAR(50) NOT NULL,
		IdempotencyKey NVARCHAR(200) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		RequestPayload NVARCHAR(MAX) NULL,
		ResponsePayload NVARCHAR(MAX) NULL,
		ExternalReferenceNumber NVARCHAR(200) NULL,
		AttemptCount INT NOT NULL CONSTRAINT DF_EndorsementCarrierDispatch_Attempts DEFAULT 0,
		MaxAttempts INT NOT NULL CONSTRAINT DF_EndorsementCarrierDispatch_MaxAttempts DEFAULT 5,
		NextAttemptDateUtc DATETIME2 NULL,
		ClaimedBy NVARCHAR(200) NULL,
		ClaimExpiresDateUtc DATETIME2 NULL,
		LastAttemptDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		ErrorCode NVARCHAR(100) NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementCarrierDispatch_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementCarrierDispatch_Deleted DEFAULT 0,
		CONSTRAINT CK_EndorsementCarrierDispatch_Request CHECK (RequestPayload IS NULL OR ISJSON(RequestPayload)=1),
		CONSTRAINT CK_EndorsementCarrierDispatch_Response CHECK (ResponsePayload IS NULL OR ISJSON(ResponsePayload)=1)
	);
	CREATE UNIQUE INDEX UX_EndorsementCarrierDispatch_Idempotency ON Policy.PolicyEndorsementCarrierDispatch(TenantId, IdempotencyKey) WHERE IsDeleted=0;
	CREATE INDEX IX_EndorsementCarrierDispatch_Queue ON Policy.PolicyEndorsementCarrierDispatch(StatusCode, NextAttemptDateUtc, CreatedDateUtc) INCLUDE (TenantId, EndorsementId, AttemptCount, MaxAttempts) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementCarrierAttempt', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementCarrierAttempt
	(
		CarrierAttemptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementCarrierAttempt PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CarrierDispatchId UNIQUEIDENTIFIER NOT NULL,
		AttemptNumber INT NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		RequestPayload NVARCHAR(MAX) NULL,
		ResponsePayload NVARCHAR(MAX) NULL,
		HttpStatusCode INT NULL,
		ErrorCode NVARCHAR(100) NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		StartedDateUtc DATETIME2 NOT NULL,
		CompletedDateUtc DATETIME2 NOT NULL,
		CONSTRAINT CK_EndorsementCarrierAttempt_Request CHECK (RequestPayload IS NULL OR ISJSON(RequestPayload)=1),
		CONSTRAINT CK_EndorsementCarrierAttempt_Response CHECK (ResponsePayload IS NULL OR ISJSON(ResponsePayload)=1)
	);
	CREATE UNIQUE INDEX UX_EndorsementCarrierAttempt_Number ON Policy.PolicyEndorsementCarrierAttempt(TenantId, CarrierDispatchId, AttemptNumber);
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementAccountingWork', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementAccountingWork
	(
		AccountingWorkId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementAccountingWork PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		WorkTypeCode NVARCHAR(80) NOT NULL,
		IdempotencyKey NVARCHAR(200) NOT NULL,
		CurrencyCode NVARCHAR(3) NOT NULL,
		PremiumAmount DECIMAL(18,2) NOT NULL,
		FeeAmount DECIMAL(18,2) NOT NULL,
		TaxAmount DECIMAL(18,2) NOT NULL,
		TotalAmount DECIMAL(18,2) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_EndorsementAccountingWork_Status DEFAULT N'Queued',
		AttemptCount INT NOT NULL CONSTRAINT DF_EndorsementAccountingWork_Attempts DEFAULT 0,
		MaxAttempts INT NOT NULL CONSTRAINT DF_EndorsementAccountingWork_MaxAttempts DEFAULT 8,
		NextAttemptDateUtc DATETIME2 NULL,
		ClaimedBy NVARCHAR(200) NULL,
		ClaimExpiresDateUtc DATETIME2 NULL,
		ResultEntityName NVARCHAR(160) NULL,
		ResultEntityId UNIQUEIDENTIFIER NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		CompletedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementAccountingWork_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementAccountingWork_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_EndorsementAccountingWork_Idempotency ON Policy.PolicyEndorsementAccountingWork(TenantId, IdempotencyKey) WHERE IsDeleted=0;
	CREATE INDEX IX_EndorsementAccountingWork_Queue ON Policy.PolicyEndorsementAccountingWork(StatusCode, NextAttemptDateUtc, CreatedDateUtc) INCLUDE (TenantId, EndorsementId, AttemptCount, MaxAttempts) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Policy.PolicyEndorsementDocumentWork', N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementDocumentWork
	(
		DocumentWorkId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementDocumentWork PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementId UNIQUEIDENTIFIER NOT NULL,
		PolicyId UNIQUEIDENTIFIER NOT NULL,
		DocumentTypeCode NVARCHAR(100) NOT NULL,
		IdempotencyKey NVARCHAR(200) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_EndorsementDocumentWork_Status DEFAULT N'Queued',
		AttemptCount INT NOT NULL CONSTRAINT DF_EndorsementDocumentWork_Attempts DEFAULT 0,
		MaxAttempts INT NOT NULL CONSTRAINT DF_EndorsementDocumentWork_MaxAttempts DEFAULT 5,
		NextAttemptDateUtc DATETIME2 NULL,
		ClaimedBy NVARCHAR(200) NULL,
		ClaimExpiresDateUtc DATETIME2 NULL,
		DocumentId UNIQUEIDENTIFIER NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		CompletedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementDocumentWork_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementDocumentWork_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_EndorsementDocumentWork_Idempotency ON Policy.PolicyEndorsementDocumentWork(TenantId, IdempotencyKey) WHERE IsDeleted=0;
	CREATE INDEX IX_EndorsementDocumentWork_Queue ON Policy.PolicyEndorsementDocumentWork(StatusCode, NextAttemptDateUtc, CreatedDateUtc) INCLUDE (TenantId, EndorsementId, AttemptCount, MaxAttempts) WHERE IsDeleted=0;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementChange_Endorsement')
	ALTER TABLE Policy.PolicyEndorsementChange ADD CONSTRAINT FK_EndorsementChange_Endorsement FOREIGN KEY (TenantId, EndorsementId) REFERENCES Policy.PolicyEndorsement(TenantId, EndorsementId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementInsuredChange_Change')
	ALTER TABLE Policy.PolicyEndorsementInsuredChange ADD CONSTRAINT FK_EndorsementInsuredChange_Change FOREIGN KEY (TenantId, ChangeId) REFERENCES Policy.PolicyEndorsementChange(TenantId, ChangeId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementVehicleChange_Change')
	ALTER TABLE Policy.PolicyEndorsementVehicleChange ADD CONSTRAINT FK_EndorsementVehicleChange_Change FOREIGN KEY (TenantId, ChangeId) REFERENCES Policy.PolicyEndorsementChange(TenantId, ChangeId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementDriverChange_Change')
	ALTER TABLE Policy.PolicyEndorsementDriverChange ADD CONSTRAINT FK_EndorsementDriverChange_Change FOREIGN KEY (TenantId, ChangeId) REFERENCES Policy.PolicyEndorsementChange(TenantId, ChangeId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementCoverageChange_Change')
	ALTER TABLE Policy.PolicyEndorsementCoverageChange ADD CONSTRAINT FK_EndorsementCoverageChange_Change FOREIGN KEY (TenantId, ChangeId) REFERENCES Policy.PolicyEndorsementChange(TenantId, ChangeId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementPropertyChange_Change')
	ALTER TABLE Policy.PolicyEndorsementPropertyChange ADD CONSTRAINT FK_EndorsementPropertyChange_Change FOREIGN KEY (TenantId, ChangeId) REFERENCES Policy.PolicyEndorsementChange(TenantId, ChangeId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementCommercialChange_Change')
	ALTER TABLE Policy.PolicyEndorsementCommercialChange ADD CONSTRAINT FK_EndorsementCommercialChange_Change FOREIGN KEY (TenantId, ChangeId) REFERENCES Policy.PolicyEndorsementChange(TenantId, ChangeId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementFinancialChange_Change')
	ALTER TABLE Policy.PolicyEndorsementFinancialChange ADD CONSTRAINT FK_EndorsementFinancialChange_Change FOREIGN KEY (TenantId, ChangeId) REFERENCES Policy.PolicyEndorsementChange(TenantId, ChangeId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementLegalChange_Change')
	ALTER TABLE Policy.PolicyEndorsementLegalChange ADD CONSTRAINT FK_EndorsementLegalChange_Change FOREIGN KEY (TenantId, ChangeId) REFERENCES Policy.PolicyEndorsementChange(TenantId, ChangeId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementApproval_Endorsement')
	ALTER TABLE Policy.PolicyEndorsementApproval ADD CONSTRAINT FK_EndorsementApproval_Endorsement FOREIGN KEY (TenantId, EndorsementId) REFERENCES Policy.PolicyEndorsement(TenantId, EndorsementId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementEvent_Endorsement')
	ALTER TABLE Policy.PolicyEndorsementEvent ADD CONSTRAINT FK_EndorsementEvent_Endorsement FOREIGN KEY (TenantId, EndorsementId) REFERENCES Policy.PolicyEndorsement(TenantId, EndorsementId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementCarrierDispatch_Endorsement')
	ALTER TABLE Policy.PolicyEndorsementCarrierDispatch ADD CONSTRAINT FK_EndorsementCarrierDispatch_Endorsement FOREIGN KEY (TenantId, EndorsementId) REFERENCES Policy.PolicyEndorsement(TenantId, EndorsementId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementAccountingWork_Endorsement')
	ALTER TABLE Policy.PolicyEndorsementAccountingWork ADD CONSTRAINT FK_EndorsementAccountingWork_Endorsement FOREIGN KEY (TenantId, EndorsementId) REFERENCES Policy.PolicyEndorsement(TenantId, EndorsementId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_EndorsementDocumentWork_Endorsement')
	ALTER TABLE Policy.PolicyEndorsementDocumentWork ADD CONSTRAINT FK_EndorsementDocumentWork_Endorsement FOREIGN KEY (TenantId, EndorsementId) REFERENCES Policy.PolicyEndorsement(TenantId, EndorsementId);
GO

IF OBJECT_ID(N'tempdb..#EndorsementTenants') IS NOT NULL DROP TABLE #EndorsementTenants;
CREATE TABLE #EndorsementTenants (TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
	INSERT #EndorsementTenants(TenantId) SELECT TenantId FROM Core.Tenant WHERE IsDeleted=0;
INSERT #EndorsementTenants(TenantId)
SELECT DISTINCT TenantId FROM Policy.PolicyEndorsement endorsement
WHERE NOT EXISTS (SELECT 1 FROM #EndorsementTenants tenant WHERE tenant.TenantId=endorsement.TenantId);

IF OBJECT_ID(N'tempdb..#EndorsementOptions0073') IS NOT NULL DROP TABLE #EndorsementOptions0073;
CREATE TABLE #EndorsementOptions0073
(
	OptionGroupCode NVARCHAR(50) NOT NULL,
	OptionCode NVARCHAR(80) NOT NULL,
	DisplayName NVARCHAR(160) NOT NULL,
	Description NVARCHAR(500) NULL,
	IsDefault BIT NOT NULL,
	SortOrder INT NOT NULL,
	PRIMARY KEY (OptionGroupCode, OptionCode)
);
INSERT #EndorsementOptions0073 VALUES
(N'Status',N'Draft',N'Draft',N'Endorsement is editable and has not entered review.',1,10),
(N'Status',N'PendingReview',N'Pending Review',N'Endorsement is awaiting internal review.',0,20),
(N'Status',N'SubmittedToCarrier',N'Submitted to Carrier',N'Endorsement has been queued or submitted through the selected carrier channel.',0,30),
(N'Status',N'CarrierProcessing',N'Carrier Processing',N'Carrier acknowledged the endorsement and is processing it.',0,40),
(N'Status',N'CarrierApproved',N'Carrier Approved',N'Carrier approved the requested policy changes.',0,50),
(N'Status',N'PolicyUpdated',N'Policy Updated',N'An immutable policy version was activated for the approved endorsement.',0,60),
(N'Status',N'InvoiceCreated',N'Invoice Created',N'Accounting effects were synchronized.',0,70),
(N'Status',N'DocumentsGenerated',N'Documents Generated',N'Required endorsement documents were generated and linked.',0,80),
(N'Status',N'Completed',N'Completed',N'All endorsement workflow obligations are complete.',0,90),
(N'Status',N'Rejected',N'Rejected',N'Internal review or the carrier rejected the endorsement.',0,100),
(N'Status',N'Cancelled',N'Cancelled',N'The endorsement transaction was cancelled without changing the policy.',0,110),
(N'Status',N'NeedMoreInfo',N'Need More Info',N'Additional customer, underwriting, or carrier information is required.',0,120),
(N'Status',N'Expired',N'Expired',N'The endorsement request expired before completion.',0,130),
(N'Status',N'Reversed',N'Reversed',N'A completed endorsement was reversed by a linked transaction.',0,140),
(N'Reason',N'CustomerRequest',N'Customer Request',N'Change requested by the insured or authorized customer contact.',1,10),
(N'Reason',N'CarrierRequirement',N'Carrier Requirement',N'Change required by the carrier.',0,20),
(N'Reason',N'Correction',N'Correction',N'Correction of policy or submission data.',0,30),
(N'Reason',N'Audit',N'Audit',N'Change resulting from a premium or exposure audit.',0,40),
(N'Reason',N'AgencyError',N'Agency Error',N'Correction of an agency processing error.',0,50),
(N'Reason',N'Other',N'Other',N'Other documented endorsement reason.',0,60),
(N'ChangeEntityCategory',N'Insured',N'Insured',N'Named insured, DBA, FEIN, contact, mailing, or garaging changes.',0,10),
(N'ChangeEntityCategory',N'Vehicle',N'Vehicle',N'Vehicle, VIN, usage, garaging, or lienholder changes.',0,20),
(N'ChangeEntityCategory',N'Driver',N'Driver',N'Driver, license, or exclusion changes.',0,30),
(N'ChangeEntityCategory',N'Coverage',N'Coverage',N'Coverage, limit, deductible, or premium changes.',0,40),
(N'ChangeEntityCategory',N'Property',N'Property',N'Building, occupancy, construction, location, or valuation changes.',0,50),
(N'ChangeEntityCategory',N'Commercial',N'Commercial',N'Payroll, revenue, employee, equipment, blanket limit, or location changes.',0,60),
(N'ChangeEntityCategory',N'Financial',N'Financial',N'Financing, installment, premium, fee, tax, or commission changes.',0,70),
(N'ChangeEntityCategory',N'Legal',N'Legal',N'Named insured, additional insured, mortgagee, loss payee, or certificate holder changes.',0,80),
(N'ChangeEntityCategory',N'Legacy',N'Legacy / General',N'Preserved historical change that predates typed endorsement records.',0,90),
(N'ChangeOperation',N'Add',N'Add',N'Add an entity or policy term.',0,10),
(N'ChangeOperation',N'Remove',N'Remove',N'Remove an entity or policy term.',0,20),
(N'ChangeOperation',N'Replace',N'Replace',N'Replace an existing entity or policy term.',0,30),
(N'ChangeOperation',N'Update',N'Update',N'Update an existing entity or policy term.',1,40),
(N'ChangeOperation',N'Correct',N'Correct',N'Correct inaccurate policy data.',0,50),
(N'ChangeOperation',N'Exclude',N'Exclude',N'Exclude an entity from coverage.',0,60),
(N'CarrierChannel',N'Manual',N'Manual',N'Agency staff completes and records submission outside the system.',1,10),
(N'CarrierChannel',N'Email',N'Email',N'Submit through the tenant email delivery provider.',0,20),
(N'CarrierChannel',N'Portal',N'Carrier Portal',N'Queue portal instructions and track manual carrier portal completion.',0,30),
(N'CarrierChannel',N'EDI',N'EDI',N'Submit a configured electronic data interchange payload.',0,40),
(N'CarrierChannel',N'HttpApi',N'HTTP API',N'Submit through a configured carrier HTTP API.',0,50),
(N'DocumentType',N'UpdatedDeclaration',N'Updated Declaration',N'Updated policy declarations.',0,10),
(N'DocumentType',N'EndorsementSchedule',N'Endorsement Schedule',N'Before-and-after endorsement change schedule.',1,20),
(N'DocumentType',N'Invoice',N'Invoice',N'Invoice or credit document for premium impact.',0,30),
(N'DocumentType',N'Receipt',N'Receipt',N'Payment receipt when applicable.',0,40),
(N'DocumentType',N'CoverageSummary',N'Coverage Summary',N'Updated coverage summary.',0,50),
(N'DocumentType',N'CarrierLetter',N'Carrier Letter',N'Carrier-facing endorsement letter.',0,60),
(N'DocumentType',N'ClientLetter',N'Client Letter',N'Client-facing explanation of changes and financial impact.',0,70),
(N'DocumentType',N'Email',N'Email',N'Email delivery evidence.',0,80),
(N'AccountingWorkType',N'Invoice',N'Invoice',N'Create an invoice for additional premium and charges.',0,10),
(N'AccountingWorkType',N'Refund',N'Refund / Credit',N'Create a credit or refund for return premium.',0,20),
(N'AccountingWorkType',N'Commission',N'Commission',N'Recalculate agency commission.',0,30),
(N'AccountingWorkType',N'ProducerCommission',N'Producer Commission',N'Recalculate producer commission splits.',0,40),
(N'AccountingWorkType',N'AgencyPayable',N'Agency Payable',N'Create or adjust agency payable records.',0,50),
(N'AccountingWorkType',N'CarrierPayable',N'Carrier Payable',N'Create or adjust carrier payable records.',0,60),
(N'AccountingWorkType',N'GLPosting',N'GL Posting',N'Post balanced general ledger entries.',0,70);

IF OBJECT_ID(N'Policy.PolicyEndorsementOption', N'U') IS NOT NULL
BEGIN
	UPDATE existing SET DisplayName=source.DisplayName,Description=source.Description,IsDefault=source.IsDefault,IsActive=1,SortOrder=source.SortOrder,ModifiedDateUtc=SYSUTCDATETIME(),IsDeleted=0
	FROM Policy.PolicyEndorsementOption existing
	JOIN #EndorsementOptions0073 source ON source.OptionGroupCode=existing.OptionGroupCode AND source.OptionCode=existing.OptionCode;
	INSERT Policy.PolicyEndorsementOption(OptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),tenant.TenantId,source.OptionGroupCode,source.OptionCode,source.DisplayName,source.Description,source.IsDefault,1,source.SortOrder,SYSUTCDATETIME(),0
	FROM #EndorsementTenants tenant CROSS JOIN #EndorsementOptions0073 source
	WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyEndorsementOption existing WHERE existing.TenantId=tenant.TenantId AND existing.OptionGroupCode=source.OptionGroupCode AND existing.OptionCode=source.OptionCode AND existing.IsDeleted=0);
END;
GO

IF OBJECT_ID(N'Policy.EndorsementType', N'U') IS NOT NULL
BEGIN
	DECLARE @Types0073 TABLE(TypeCode NVARCHAR(50) PRIMARY KEY,TypeName NVARCHAR(120),Description NVARCHAR(500),SortOrder INT);
	INSERT @Types0073 VALUES
	(N'InsuredName',N'Insured Name Change',N'Change legal or display name of an insured.',10),(N'Dba',N'DBA Change',N'Add or change a doing-business-as name.',20),(N'MailingAddress',N'Mailing Address',N'Change insured mailing address.',30),(N'GaragingAddress',N'Garaging Address',N'Change vehicle garaging address.',40),(N'Fein',N'FEIN Correction',N'Correct the insured federal tax identifier.',50),(N'InsuredContact',N'Phone / Email',N'Change insured contact information.',60),
	(N'AddVehicle',N'Add Vehicle',N'Add a covered vehicle.',100),(N'RemoveVehicle',N'Remove Vehicle',N'Remove a covered vehicle.',110),(N'ReplaceVehicle',N'Replace Vehicle',N'Replace a covered vehicle.',120),(N'VinCorrection',N'VIN Correction',N'Correct a vehicle identification number.',130),(N'Lienholder',N'Lienholder',N'Add, remove, or change a lienholder.',140),
	(N'AddDriver',N'Add Driver',N'Add a driver.',200),(N'RemoveDriver',N'Remove Driver',N'Remove a driver.',210),(N'LicenseCorrection',N'License Correction',N'Correct driver license information.',220),(N'ExcludedDriver',N'Excluded Driver',N'Add or remove a driver exclusion.',230),
	(N'IncreaseLimit',N'Increase Limit',N'Increase a coverage limit.',300),(N'DecreaseLimit',N'Decrease Limit',N'Decrease a coverage limit.',310),(N'DeductibleChange',N'Deductible Change',N'Change a deductible.',320),(N'AddCoverage',N'Add Coverage',N'Add policy coverage.',330),(N'RemoveCoverage',N'Remove Coverage',N'Remove policy coverage.',340),
	(N'AddBuilding',N'Add Building',N'Add a building or property location.',400),(N'RemoveBuilding',N'Remove Building',N'Remove a building or property location.',410),(N'Occupancy',N'Occupancy',N'Change occupancy information.',420),(N'Construction',N'Construction',N'Change construction information.',430),(N'SquareFootage',N'Square Footage',N'Change reported square footage.',440),
	(N'Payroll',N'Payroll',N'Change payroll exposure.',500),(N'Revenue',N'Revenue',N'Change revenue exposure.',510),(N'EmployeeCount',N'Employee Count',N'Change employee count.',520),(N'EquipmentSchedule',N'Equipment Schedule',N'Change scheduled equipment.',530),(N'BlanketLimits',N'Blanket Limits',N'Change blanket limits.',540),(N'AdditionalLocations',N'Additional Locations',N'Add or remove business locations.',550),
	(N'PremiumFinancing',N'Premium Financing',N'Change premium financing.',600),(N'InstallmentPlan',N'Installment Plan',N'Change installment plan.',610),(N'CommissionAdjustment',N'Commission Adjustment',N'Adjust commission treatment.',620),
	(N'NamedInsured',N'Named Insured',N'Add, remove, or correct a named insured.',700),(N'AdditionalInsured',N'Additional Insured',N'Add, remove, or change an additional insured.',710),(N'Mortgagee',N'Mortgagee',N'Add, remove, or change a mortgagee.',720),(N'LossPayee',N'Loss Payee',N'Add, remove, or change a loss payee.',730),(N'CertificateHolder',N'Certificate Holder',N'Add, remove, or change a certificate holder.',740);
	UPDATE existing SET TypeName=source.TypeName,Description=source.Description,IsActive=1,SortOrder=source.SortOrder,ModifiedDateUtc=SYSUTCDATETIME(),IsDeleted=0
	FROM Policy.EndorsementType existing JOIN @Types0073 source ON source.TypeCode=existing.TypeCode;
	INSERT Policy.EndorsementType(EndorsementTypeId,TenantId,TypeCode,TypeName,Description,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),tenant.TenantId,source.TypeCode,source.TypeName,source.Description,1,source.SortOrder,SYSUTCDATETIME(),0
	FROM #EndorsementTenants tenant CROSS JOIN @Types0073 source
	WHERE NOT EXISTS (SELECT 1 FROM Policy.EndorsementType existing WHERE existing.TenantId=tenant.TenantId AND existing.TypeCode=source.TypeCode AND existing.IsDeleted=0);
END;
GO

DECLARE @Transitions0073 TABLE(FromStatusCode NVARCHAR(80),ToStatusCode NVARCHAR(80),PermissionCode NVARCHAR(120),RequiresApproval BIT,RequiresCarrier BIT,CreatesVersion BIT,CreatesAccounting BIT,CreatesDocuments BIT,SortOrder INT,PRIMARY KEY(FromStatusCode,ToStatusCode));
INSERT @Transitions0073 VALUES
(N'Draft',N'PendingReview',N'ENDORSEMENT_SUBMIT',0,0,0,0,0,10),(N'Draft',N'Cancelled',N'ENDORSEMENT_VOID',0,0,0,0,0,20),
(N'PendingReview',N'SubmittedToCarrier',N'ENDORSEMENT_APPROVE',1,1,0,0,0,30),(N'PendingReview',N'NeedMoreInfo',N'ENDORSEMENT_REVIEW',0,0,0,0,0,40),(N'PendingReview',N'Rejected',N'ENDORSEMENT_APPROVE',0,0,0,0,0,50),(N'PendingReview',N'Cancelled',N'ENDORSEMENT_VOID',0,0,0,0,0,60),
(N'NeedMoreInfo',N'PendingReview',N'ENDORSEMENT_EDIT_DRAFT',0,0,0,0,0,70),(N'NeedMoreInfo',N'Cancelled',N'ENDORSEMENT_VOID',0,0,0,0,0,80),(N'NeedMoreInfo',N'Expired',N'ENDORSEMENT_MANAGE',0,0,0,0,0,90),
(N'SubmittedToCarrier',N'CarrierProcessing',N'ENDORSEMENT_CARRIER_SUBMIT',0,0,0,0,0,100),(N'SubmittedToCarrier',N'NeedMoreInfo',N'ENDORSEMENT_REVIEW',0,0,0,0,0,110),(N'SubmittedToCarrier',N'Rejected',N'ENDORSEMENT_REVIEW',0,0,0,0,0,120),(N'SubmittedToCarrier',N'Cancelled',N'ENDORSEMENT_VOID',0,0,0,0,0,130),
(N'CarrierProcessing',N'CarrierApproved',N'ENDORSEMENT_REVIEW',0,0,0,0,0,140),(N'CarrierProcessing',N'NeedMoreInfo',N'ENDORSEMENT_REVIEW',0,0,0,0,0,150),(N'CarrierProcessing',N'Rejected',N'ENDORSEMENT_REVIEW',0,0,0,0,0,160),
(N'CarrierApproved',N'PolicyUpdated',N'ENDORSEMENT_APPLY_POLICY',0,0,1,0,0,170),(N'PolicyUpdated',N'InvoiceCreated',N'ENDORSEMENT_ACCOUNTING',0,0,0,1,0,180),(N'InvoiceCreated',N'DocumentsGenerated',N'ENDORSEMENT_DOCUMENT_GENERATE',0,0,0,0,1,190),(N'DocumentsGenerated',N'Completed',N'ENDORSEMENT_MANAGE',0,0,0,0,0,200),(N'Completed',N'Reversed',N'ENDORSEMENT_REVERSE',1,0,1,1,1,210);
UPDATE existing SET RequiredPermissionCode=source.PermissionCode,RequiresApproval=source.RequiresApproval,RequiresCarrierSubmission=source.RequiresCarrier,CreatesPolicyVersion=source.CreatesVersion,CreatesAccountingWork=source.CreatesAccounting,CreatesDocumentWork=source.CreatesDocuments,IsActive=1,SortOrder=source.SortOrder,ModifiedDateUtc=SYSUTCDATETIME(),IsDeleted=0
FROM Policy.PolicyEndorsementStatusTransition existing JOIN @Transitions0073 source ON source.FromStatusCode=existing.FromStatusCode AND source.ToStatusCode=existing.ToStatusCode;
INSERT Policy.PolicyEndorsementStatusTransition(StatusTransitionId,TenantId,FromStatusCode,ToStatusCode,RequiredPermissionCode,RequiresApproval,RequiresCarrierSubmission,CreatesPolicyVersion,CreatesAccountingWork,CreatesDocumentWork,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),tenant.TenantId,source.FromStatusCode,source.ToStatusCode,source.PermissionCode,source.RequiresApproval,source.RequiresCarrier,source.CreatesVersion,source.CreatesAccounting,source.CreatesDocuments,1,source.SortOrder,SYSUTCDATETIME(),0
FROM #EndorsementTenants tenant CROSS JOIN @Transitions0073 source
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyEndorsementStatusTransition existing WHERE existing.TenantId=tenant.TenantId AND existing.FromStatusCode=source.FromStatusCode AND existing.ToStatusCode=source.ToStatusCode AND existing.IsDeleted=0);
GO

IF OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
BEGIN
	DECLARE @PermissionTenantId0073 UNIQUEIDENTIFIER=(SELECT TOP 1 TenantId FROM #EndorsementTenants ORDER BY TenantId);
	DECLARE @ReadActionId0073 INT=(SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode)=N'READ' OR UPPER(ActionName)=N'READ' ORDER BY PermissionActionId);
	DECLARE @WriteActionId0073 INT=(SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode) IN(N'WRITE',N'MANAGE') OR UPPER(ActionName) IN(N'WRITE',N'MANAGE') ORDER BY PermissionActionId);
	DECLARE @Permissions0073 TABLE(PermissionCode NVARCHAR(200) PRIMARY KEY,PermissionName NVARCHAR(200),ActionCode NVARCHAR(100),PermissionActionId INT,Description NVARCHAR(500));
	INSERT @Permissions0073 VALUES
	(N'ENDORSEMENT_VIEW',N'View Endorsements',N'Read',@ReadActionId0073,N'View endorsement transactions, changes, versions, documents, timeline, and permitted financial data.'),(N'ENDORSEMENT_CREATE',N'Create Endorsements',N'Write',@WriteActionId0073,N'Create endorsement drafts for policies in the authenticated tenant.'),(N'ENDORSEMENT_EDIT_DRAFT',N'Edit Endorsement Drafts',N'Write',@WriteActionId0073,N'Edit draft or information-requested endorsements.'),(N'ENDORSEMENT_SUBMIT',N'Submit Endorsements',N'Write',@WriteActionId0073,N'Submit endorsements for internal review.'),(N'ENDORSEMENT_REVIEW',N'Review Endorsements',N'Manage',@WriteActionId0073,N'Review endorsement completeness and carrier responses.'),(N'ENDORSEMENT_APPROVE',N'Approve Endorsements',N'Manage',@WriteActionId0073,N'Approve endorsements for carrier submission or reversal.'),(N'ENDORSEMENT_VOID',N'Cancel Endorsements',N'Manage',@WriteActionId0073,N'Cancel endorsements before completion.'),(N'ENDORSEMENT_CARRIER_SUBMIT',N'Submit Endorsements to Carrier',N'Write',@WriteActionId0073,N'Submit through configured carrier channels and manage dispatch evidence.'),(N'ENDORSEMENT_APPLY_POLICY',N'Apply Endorsements to Policy',N'Manage',@WriteActionId0073,N'Activate approved policy versions.'),(N'ENDORSEMENT_ACCOUNTING',N'Process Endorsement Accounting',N'Manage',@WriteActionId0073,N'Create endorsement invoice, refund, commission, payable, and GL effects.'),(N'ENDORSEMENT_DOCUMENT_GENERATE',N'Generate Endorsement Documents',N'Write',@WriteActionId0073,N'Generate and deliver endorsement documents.'),(N'ENDORSEMENT_BACKDATE',N'Backdate Endorsements',N'Manage',@WriteActionId0073,N'Override normal effective-date validation.'),(N'ENDORSEMENT_FINANCIAL_VIEW',N'View Endorsement Financial Impact',N'Read',@ReadActionId0073,N'View premium, fee, tax, invoice, refund, and commission impact.'),(N'ENDORSEMENT_REVERSE',N'Reverse Completed Endorsements',N'Manage',@WriteActionId0073,N'Reverse a completed endorsement through a linked compensating transaction.'),(N'ENDORSEMENT_MANAGE',N'Manage Endorsements',N'Manage',@WriteActionId0073,N'Administer enterprise endorsement workflow and configuration.');
	UPDATE existing SET PermissionName=source.PermissionName,ResourceCode=N'PolicyEndorsement',ActionCode=source.ActionCode,PermissionActionId=COALESCE(source.PermissionActionId,existing.PermissionActionId),ModuleCode=N'Policy',Description=source.Description,IsBuiltIn=1,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	FROM IAM.Permission existing JOIN @Permissions0073 source ON source.PermissionCode=existing.PermissionCode;
	INSERT IAM.Permission(PermissionId,TenantId,PermissionCode,PermissionName,ResourceCode,ActionCode,PermissionActionId,ModuleCode,Description,IsBuiltIn,IsActive,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),@PermissionTenantId0073,source.PermissionCode,source.PermissionName,N'PolicyEndorsement',source.ActionCode,source.PermissionActionId,N'Policy',source.Description,1,1,SYSUTCDATETIME(),0
	FROM @Permissions0073 source WHERE @PermissionTenantId0073 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM IAM.Permission existing WHERE existing.PermissionCode=source.PermissionCode);
END;
GO

UPDATE endorsement
SET Status=CASE endorsement.Status WHEN N'Pending' THEN N'PendingReview' WHEN N'In Review' THEN N'PendingReview' WHEN N'Info Needed' THEN N'NeedMoreInfo' WHEN N'Approved' THEN N'CarrierApproved' WHEN N'Issued' THEN N'Completed' WHEN N'Declined' THEN N'Rejected' ELSE endorsement.Status END,
	WorkflowStage=CASE endorsement.Status WHEN N'Pending' THEN N'InternalReview' WHEN N'In Review' THEN N'InternalReview' WHEN N'Info Needed' THEN N'AwaitingInformation' WHEN N'Approved' THEN N'CarrierApproved' WHEN N'Issued' THEN N'Completed' WHEN N'Declined' THEN N'Rejected' ELSE COALESCE(endorsement.WorkflowStage,N'Intake') END,
	ReasonCode=COALESCE(endorsement.ReasonCode,N'Other'),
	TaxDelta=CASE WHEN endorsement.TaxDelta=0 THEN endorsement.TaxFeeDelta ELSE endorsement.TaxDelta END,
	TotalCostDelta=CASE WHEN endorsement.TotalCostDelta=0 THEN endorsement.PremiumDelta+endorsement.AgencyFeeDelta+CASE WHEN endorsement.TaxDelta=0 THEN endorsement.TaxFeeDelta ELSE endorsement.TaxDelta END ELSE endorsement.TotalCostDelta END,
	SubmittedDateUtc=COALESCE(endorsement.SubmittedDateUtc,endorsement.CarrierSubmissionDateUtc),
	CompletedDateUtc=CASE WHEN endorsement.Status=N'Issued' THEN COALESCE(endorsement.CompletedDateUtc,endorsement.IssuedDateUtc,endorsement.ModifiedDateUtc,endorsement.CreatedDateUtc) ELSE endorsement.CompletedDateUtc END,
	RejectedDateUtc=CASE WHEN endorsement.Status=N'Declined' THEN COALESCE(endorsement.RejectedDateUtc,endorsement.ModifiedDateUtc,endorsement.CreatedDateUtc) ELSE endorsement.RejectedDateUtc END,
	ModifiedDateUtc=COALESCE(endorsement.ModifiedDateUtc,SYSUTCDATETIME())
FROM Policy.PolicyEndorsement endorsement;

UPDATE endorsement SET PolicyVersionBeforeId=version.PolicyVersionId
FROM Policy.PolicyEndorsement endorsement
OUTER APPLY (SELECT TOP 1 PolicyVersionId FROM Policy.PolicyVersion version WHERE version.TenantId=endorsement.TenantId AND version.PolicyId=endorsement.PolicyId AND version.IsDeleted=0 ORDER BY version.VersionNumber DESC) version
WHERE endorsement.PolicyId IS NOT NULL AND endorsement.PolicyVersionBeforeId IS NULL;

UPDATE policy SET CurrentPolicyVersionId=version.PolicyVersionId,CurrentVersionNumber=version.VersionNumber
FROM Submissions.BoundPolicy policy
OUTER APPLY (SELECT TOP 1 PolicyVersionId,VersionNumber FROM Policy.PolicyVersion version WHERE version.TenantId=policy.TenantId AND version.PolicyId=policy.PolicyId AND version.IsDeleted=0 ORDER BY version.VersionNumber DESC) version
WHERE version.PolicyVersionId IS NOT NULL AND (policy.CurrentPolicyVersionId IS NULL OR policy.CurrentVersionNumber<>version.VersionNumber);

INSERT Policy.PolicyEndorsementChange(ChangeId,TenantId,EndorsementId,CategoryCode,OperationCode,EntityKey,SequenceNumber,Summary,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT delta.DeltaId,delta.TenantId,delta.EndorsementId,N'Legacy',N'Update',CONVERT(NVARCHAR(36),delta.DeltaId),ROW_NUMBER() OVER(PARTITION BY delta.TenantId,delta.EndorsementId ORDER BY delta.CreatedDateUtc,delta.DeltaId),CONCAT(delta.FieldName,N': ',delta.BeforeValue,N' → ',delta.AfterValue),delta.CreatedDateUtc,delta.CreatedByUserId,delta.IsDeleted
FROM Policy.PolicyEndorsementDelta delta
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyEndorsementChange change WHERE change.TenantId=delta.TenantId AND change.ChangeId=delta.DeltaId);

INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId)
SELECT NEWID(),endorsement.TenantId,endorsement.EndorsementId,endorsement.PolicyId,N'LegacyMigrated',endorsement.Status,N'Existing endorsement history migrated to the enterprise transaction workflow.',JSON_OBJECT(N'endorsementNumber':endorsement.EndorsementNumber,N'legacyStatus':endorsement.Status),NEWID(),endorsement.CreatedDateUtc,endorsement.CreatedByUserId
FROM Policy.PolicyEndorsement endorsement
WHERE endorsement.PolicyId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Policy.PolicyEndorsementEvent event WHERE event.TenantId=endorsement.TenantId AND event.EndorsementId=endorsement.EndorsementId);
GO
