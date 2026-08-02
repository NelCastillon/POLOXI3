SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ClientAcceptanceId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ClientAcceptanceId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'BindingAuthorityCode') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD BindingAuthorityCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'BindingMethodCode') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD BindingMethodCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ProducerNotes') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ProducerNotes NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CarrierInstructions') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CarrierInstructions NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'SpecialConditions') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD SpecialConditions NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ApprovalRequired') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ApprovalRequired BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_ApprovalRequired DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'PaymentRequired') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD PaymentRequired BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_PaymentRequired DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'PaymentVerified') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD PaymentVerified BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_PaymentVerified DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'PreparedDateUtc') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD PreparedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'SubmittedDateUtc') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD SubmittedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ReceivedDateUtc') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ReceivedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ResponseDueDateUtc') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ResponseDueDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'RetryCount') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD RetryCount INT NOT NULL CONSTRAINT DF_PolicyBindTransaction_RetryCount DEFAULT 0;
GO

IF OBJECT_ID(N'Submissions.BindRequirement', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindRequirement
	(
		BindRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindRequirement PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		LineOfBusiness NVARCHAR(100) NULL,
		RequirementCode NVARCHAR(100) NOT NULL,
		RequirementName NVARCHAR(200) NOT NULL,
		RequirementTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(500) NULL,
		DocumentCategoryCode NVARCHAR(100) NULL,
		IsRequired BIT NOT NULL CONSTRAINT DF_BindRequirement_Required DEFAULT 1,
		BlocksSubmission BIT NOT NULL CONSTRAINT DF_BindRequirement_Blocks DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_BindRequirement_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_BindRequirement_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindRequirement_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindRequirement_Deleted DEFAULT 0
	);
END;
GO

IF OBJECT_ID(N'Submissions.BindStatusTransition', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindStatusTransition
	(
		BindStatusTransitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindStatusTransition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		FromStatusCode NVARCHAR(50) NOT NULL,
		ToStatusCode NVARCHAR(50) NOT NULL,
		RequiresValidation BIT NOT NULL CONSTRAINT DF_BindStatusTransition_Validation DEFAULT 0,
		RequiresApproval BIT NOT NULL CONSTRAINT DF_BindStatusTransition_Approval DEFAULT 0,
		RequiresCarrierResponse BIT NOT NULL CONSTRAINT DF_BindStatusTransition_Carrier DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_BindStatusTransition_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindStatusTransition_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindStatusTransition_Deleted DEFAULT 0
	);
END;
GO

IF OBJECT_ID(N'Submissions.BindApprovalRule', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindApprovalRule
	(
		BindApprovalRuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindApprovalRule PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		LineOfBusiness NVARCHAR(100) NULL,
		ApprovalReasonCode NVARCHAR(100) NOT NULL,
		MinimumPremium DECIMAL(18,2) NULL,
		RequiresCommissionOverride BIT NOT NULL CONSTRAINT DF_BindApprovalRule_Commission DEFAULT 0,
		AssignedApproverUserId UNIQUEIDENTIFIER NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_BindApprovalRule_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_BindApprovalRule_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindApprovalRule_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindApprovalRule_Deleted DEFAULT 0
	);
END;
GO

IF OBJECT_ID(N'Submissions.BindPackage', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindPackage
	(
		BindPackageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindPackage PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL,
		PackageNumber NVARCHAR(80) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		PreparedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindPackage_Prepared DEFAULT SYSUTCDATETIME(),
		PreparedByUserId UNIQUEIDENTIFIER NULL,
		DocumentCount INT NOT NULL CONSTRAINT DF_BindPackage_DocumentCount DEFAULT 0,
		Notes NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindPackage_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindPackage_Deleted DEFAULT 0
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BindValidationResult_Request') ALTER TABLE Submissions.BindValidationResult ADD CONSTRAINT FK_BindValidationResult_Request FOREIGN KEY (PolicyBindTransactionId) REFERENCES Submissions.PolicyBindTransaction(PolicyBindTransactionId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BindValidationResult_Requirement') ALTER TABLE Submissions.BindValidationResult ADD CONSTRAINT FK_BindValidationResult_Requirement FOREIGN KEY (BindRequirementId) REFERENCES Submissions.BindRequirement(BindRequirementId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BindStatusHistory_Request') ALTER TABLE Submissions.BindStatusHistory ADD CONSTRAINT FK_BindStatusHistory_Request FOREIGN KEY (PolicyBindTransactionId) REFERENCES Submissions.PolicyBindTransaction(PolicyBindTransactionId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BindApproval_Request') ALTER TABLE Submissions.BindApproval ADD CONSTRAINT FK_BindApproval_Request FOREIGN KEY (PolicyBindTransactionId) REFERENCES Submissions.PolicyBindTransaction(PolicyBindTransactionId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BindDocument_Request') ALTER TABLE Submissions.BindDocument ADD CONSTRAINT FK_BindDocument_Request FOREIGN KEY (PolicyBindTransactionId) REFERENCES Submissions.PolicyBindTransaction(PolicyBindTransactionId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BindCarrierMessage_Request') ALTER TABLE Submissions.BindCarrierMessage ADD CONSTRAINT FK_BindCarrierMessage_Request FOREIGN KEY (PolicyBindTransactionId) REFERENCES Submissions.PolicyBindTransaction(PolicyBindTransactionId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BindPackage_Request') ALTER TABLE Submissions.BindPackage ADD CONSTRAINT FK_BindPackage_Request FOREIGN KEY (PolicyBindTransactionId) REFERENCES Submissions.PolicyBindTransaction(PolicyBindTransactionId);
GO

IF OBJECT_ID(N'Submissions.BindValidationResult', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindValidationResult
	(
		BindValidationResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindValidationResult PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL,
		BindRequirementId UNIQUEIDENTIFIER NULL,
		RequirementCode NVARCHAR(100) NOT NULL,
		RequirementName NVARCHAR(200) NOT NULL,
		RequirementTypeCode NVARCHAR(50) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		IsBlocking BIT NOT NULL,
		Message NVARCHAR(1000) NULL,
		EvidenceDocumentId UNIQUEIDENTIFIER NULL,
		ValidatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindValidationResult_Validated DEFAULT SYSUTCDATETIME(),
		ValidatedByUserId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindValidationResult_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindValidationResult_Deleted DEFAULT 0,
		CONSTRAINT CK_BindValidationResult_Status CHECK (StatusCode IN (N'Passed', N'Failed', N'Pending', N'Waived'))
	);
END;
GO

IF OBJECT_ID(N'Submissions.BindStatusHistory', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindStatusHistory
	(
		BindStatusHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindStatusHistory PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL,
		OldStatusCode NVARCHAR(50) NULL,
		NewStatusCode NVARCHAR(50) NOT NULL,
		Comments NVARCHAR(2000) NULL,
		IpAddress NVARCHAR(64) NULL,
		DeviceInfo NVARCHAR(500) NULL,
		ChangedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindStatusHistory_Changed DEFAULT SYSUTCDATETIME(),
		ChangedByUserId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindStatusHistory_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindStatusHistory_Deleted DEFAULT 0
	);
END;
GO

IF OBJECT_ID(N'Submissions.BindApproval', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindApproval
	(
		BindApprovalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindApproval PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL,
		ApprovalReasonCode NVARCHAR(100) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		RequestedByUserId UNIQUEIDENTIFIER NULL,
		RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindApproval_Requested DEFAULT SYSUTCDATETIME(),
		AssignedApproverUserId UNIQUEIDENTIFIER NULL,
		DecisionByUserId UNIQUEIDENTIFIER NULL,
		DecisionDateUtc DATETIME2 NULL,
		DecisionNotes NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindApproval_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindApproval_Deleted DEFAULT 0,
		CONSTRAINT CK_BindApproval_Status CHECK (StatusCode IN (N'Pending', N'Approved', N'Rejected', N'Cancelled'))
	);
END;
GO

IF OBJECT_ID(N'Submissions.BindDocument', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindDocument
	(
		BindDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindDocument PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL,
		DocumentId UNIQUEIDENTIFIER NOT NULL,
		DocumentRoleCode NVARCHAR(100) NOT NULL,
		IsRequiredEvidence BIT NOT NULL CONSTRAINT DF_BindDocument_Required DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindDocument_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindDocument_Deleted DEFAULT 0
	);
END;
GO

IF OBJECT_ID(N'Submissions.BindCarrierMessage', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.BindCarrierMessage
	(
		BindCarrierMessageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BindCarrierMessage PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL,
		DirectionCode NVARCHAR(20) NOT NULL,
		MessageTypeCode NVARCHAR(50) NOT NULL,
		DeliveryMethodCode NVARCHAR(50) NULL,
		ExternalMessageId NVARCHAR(200) NULL,
		Subject NVARCHAR(300) NULL,
		MessageBody NVARCHAR(MAX) NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		SentReceivedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindCarrierMessage_Date DEFAULT SYSUTCDATETIME(),
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BindCarrierMessage_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_BindCarrierMessage_Deleted DEFAULT 0,
		CONSTRAINT CK_BindCarrierMessage_Direction CHECK (DirectionCode IN (N'Outbound', N'Inbound'))
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PolicyBindTransaction_Tenant_Status' AND object_id = OBJECT_ID(N'Submissions.PolicyBindTransaction')) CREATE INDEX IX_PolicyBindTransaction_Tenant_Status ON Submissions.PolicyBindTransaction(TenantId, BindStatusCode, IsDeleted) INCLUDE (SubmissionId, QuoteId, CarrierId, RequestedDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BindRequirement_Tenant_Carrier' AND object_id = OBJECT_ID(N'Submissions.BindRequirement')) CREATE INDEX IX_BindRequirement_Tenant_Carrier ON Submissions.BindRequirement(TenantId, CarrierId, LineOfBusiness, IsActive, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BindValidationResult_Request' AND object_id = OBJECT_ID(N'Submissions.BindValidationResult')) CREATE INDEX IX_BindValidationResult_Request ON Submissions.BindValidationResult(TenantId, PolicyBindTransactionId, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BindStatusHistory_Request' AND object_id = OBJECT_ID(N'Submissions.BindStatusHistory')) CREATE INDEX IX_BindStatusHistory_Request ON Submissions.BindStatusHistory(TenantId, PolicyBindTransactionId, ChangedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BindDocument_Active' AND object_id = OBJECT_ID(N'Submissions.BindDocument')) CREATE UNIQUE INDEX UX_BindDocument_Active ON Submissions.BindDocument(TenantId, PolicyBindTransactionId, DocumentId, DocumentRoleCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BindApproval_Request' AND object_id = OBJECT_ID(N'Submissions.BindApproval')) CREATE INDEX IX_BindApproval_Request ON Submissions.BindApproval(TenantId, PolicyBindTransactionId, StatusCode, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BindCarrierMessage_Request' AND object_id = OBJECT_ID(N'Submissions.BindCarrierMessage')) CREATE INDEX IX_BindCarrierMessage_Request ON Submissions.BindCarrierMessage(TenantId, PolicyBindTransactionId, SentReceivedDateUtc DESC, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BindStatusTransition_Active' AND object_id = OBJECT_ID(N'Submissions.BindStatusTransition')) CREATE UNIQUE INDEX UX_BindStatusTransition_Active ON Submissions.BindStatusTransition(TenantId, FromStatusCode, ToStatusCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BindApprovalRule_Scope' AND object_id = OBJECT_ID(N'Submissions.BindApprovalRule')) CREATE INDEX IX_BindApprovalRule_Scope ON Submissions.BindApprovalRule(TenantId, CarrierId, LineOfBusiness, IsActive, IsDeleted);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BindPackage_Request' AND object_id = OBJECT_ID(N'Submissions.BindPackage')) CREATE INDEX IX_BindPackage_Request ON Submissions.BindPackage(TenantId, PolicyBindTransactionId, PreparedDateUtc DESC, IsDeleted);
GO

DECLARE @StatusSeed TABLE (StatusCode NVARCHAR(50), StatusName NVARCHAR(100), Description NVARCHAR(500), IsTerminal BIT, CreatesPolicy BIT, IsDefault BIT, SortOrder INT);
INSERT INTO @StatusSeed VALUES
(N'Draft', N'Draft', N'Bind request is being prepared.', 0, 0, 1, 10),
(N'Ready', N'Ready', N'All blocking readiness checks have passed.', 0, 0, 0, 20),
(N'Submitted', N'Submitted', N'Bind request has been sent to the carrier.', 0, 0, 0, 30),
(N'Received', N'Received', N'Carrier acknowledged receipt.', 0, 0, 0, 40),
(N'UnderReview', N'Under Review', N'Carrier is reviewing the bind request.', 0, 0, 0, 50),
(N'NeedInformation', N'Need Information', N'Carrier requires additional information.', 0, 0, 0, 60),
(N'PendingPayment', N'Pending Payment', N'Required payment has not been verified.', 0, 0, 0, 70),
(N'PendingApproval', N'Pending Approval', N'Agency approval is required.', 0, 0, 0, 80),
(N'Approved', N'Approved', N'Carrier approved the request but coverage is not yet confirmed bound.', 0, 0, 0, 90),
(N'Rejected', N'Rejected', N'Carrier rejected the bind request.', 1, 0, 0, 100),
(N'Expired', N'Expired', N'Bind request expired.', 1, 0, 0, 110),
(N'Withdrawn', N'Withdrawn', N'Agency withdrew the bind request.', 1, 0, 0, 120),
(N'Bound', N'Bound', N'Carrier authoritatively confirmed coverage bound.', 1, 1, 0, 130),
(N'Cancelled', N'Cancelled', N'Bind request was cancelled.', 1, 0, 0, 140);

MERGE Submissions.PolicyBindStatus AS target
USING (SELECT t.TenantId, s.* FROM Core.Tenant t CROSS JOIN @StatusSeed s) AS source
ON target.TenantId = source.TenantId AND target.StatusCode = source.StatusCode
WHEN MATCHED THEN UPDATE SET StatusName = source.StatusName, Description = source.Description, IsTerminal = source.IsTerminal, CreatesPolicy = source.CreatesPolicy, IsDefault = source.IsDefault, IsActive = 1, SortOrder = source.SortOrder, IsDeleted = 0, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted) VALUES (source.TenantId, source.StatusCode, source.StatusName, source.Description, source.IsTerminal, source.CreatesPolicy, source.IsDefault, 1, source.SortOrder, SYSUTCDATETIME(), 0);
GO

DECLARE @TransitionSeed TABLE (FromStatusCode NVARCHAR(50), ToStatusCode NVARCHAR(50), RequiresValidation BIT, RequiresApproval BIT, RequiresCarrierResponse BIT);
INSERT INTO @TransitionSeed VALUES
(N'Draft', N'Ready', 1, 1, 0), (N'Draft', N'PendingApproval', 0, 0, 0), (N'Draft', N'Cancelled', 0, 0, 0),
(N'PendingApproval', N'Ready', 1, 1, 0), (N'PendingApproval', N'Draft', 0, 0, 0), (N'Ready', N'Submitted', 1, 1, 0),
(N'Ready', N'Cancelled', 0, 0, 0), (N'Submitted', N'Received', 0, 0, 1), (N'Submitted', N'UnderReview', 0, 0, 1),
(N'Submitted', N'NeedInformation', 0, 0, 1), (N'Submitted', N'Rejected', 0, 0, 1), (N'Submitted', N'Withdrawn', 0, 0, 0),
(N'Received', N'UnderReview', 0, 0, 1), (N'Received', N'NeedInformation', 0, 0, 1), (N'Received', N'Approved', 0, 0, 1),
(N'Received', N'Bound', 0, 0, 1), (N'UnderReview', N'NeedInformation', 0, 0, 1), (N'UnderReview', N'PendingPayment', 0, 0, 1),
(N'UnderReview', N'Approved', 0, 0, 1), (N'UnderReview', N'Rejected', 0, 0, 1), (N'UnderReview', N'Bound', 0, 0, 1),
(N'NeedInformation', N'Ready', 1, 1, 0), (N'NeedInformation', N'Submitted', 1, 1, 0), (N'PendingPayment', N'Approved', 0, 0, 1),
(N'PendingPayment', N'Bound', 0, 0, 1), (N'Approved', N'Bound', 0, 0, 1), (N'Approved', N'Rejected', 0, 0, 1);
MERGE Submissions.BindStatusTransition AS target
USING (SELECT t.TenantId, s.* FROM Core.Tenant t CROSS JOIN @TransitionSeed s) AS source
ON target.TenantId = source.TenantId AND target.FromStatusCode = source.FromStatusCode AND target.ToStatusCode = source.ToStatusCode
WHEN MATCHED THEN UPDATE SET RequiresValidation = source.RequiresValidation, RequiresApproval = source.RequiresApproval, RequiresCarrierResponse = source.RequiresCarrierResponse, IsActive = 1, IsDeleted = 0, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (TenantId, FromStatusCode, ToStatusCode, RequiresValidation, RequiresApproval, RequiresCarrierResponse, IsActive, CreatedDateUtc, IsDeleted) VALUES (source.TenantId, source.FromStatusCode, source.ToStatusCode, source.RequiresValidation, source.RequiresApproval, source.RequiresCarrierResponse, 1, SYSUTCDATETIME(), 0);
GO

DECLARE @OptionSeed TABLE (OptionGroup NVARCHAR(100), OptionCode NVARCHAR(100), OptionName NVARCHAR(200), Description NVARCHAR(500), SortOrder INT);
INSERT INTO @OptionSeed VALUES
(N'BindMethod', N'CarrierApi', N'Carrier API', N'Submit through a configured carrier integration.', 10),
(N'BindMethod', N'Email', N'Email', N'Send the binder package by email.', 20),
(N'BindMethod', N'CarrierPortal', N'Carrier Portal', N'Submit through the carrier portal.', 30),
(N'BindMethod', N'Phone', N'Phone / Verbal', N'Request binding by phone with required written follow-up.', 40),
(N'BindMethod', N'Manual', N'Manual', N'Externally coordinated manual bind request.', 50),
(N'BindingAuthority', N'Carrier', N'Carrier Authority', N'Carrier or authorized underwriter must confirm binding.', 10),
(N'BindingAuthority', N'Mga', N'MGA Authority', N'Authorized managing general agent confirms binding.', 20),
(N'BindingAuthority', N'Wholesaler', N'Wholesaler Authority', N'Authorized wholesaler confirms binding.', 30),
(N'BindingAuthority', N'Agency', N'Agency Binding Authority', N'Agency authority applies subject to configured limits.', 40),
(N'BindApprovalReason', N'PremiumThreshold', N'Premium Threshold', N'Premium exceeds the agency approval threshold.', 10),
(N'BindApprovalReason', N'CommissionOverride', N'Commission Override', N'Commission terms require approval.', 20),
(N'BindApprovalReason', N'AuthorityException', N'Authority Exception', N'Binding authority exception requires approval.', 30),
(N'BindCarrierMessageType', N'CarrierResponse', N'Carrier Response', N'General response from the carrier or authorized market.', 10),
(N'BindCarrierMessageType', N'NeedInformation', N'Need Information', N'Carrier requested additional underwriting information.', 20),
(N'BindCarrierMessageType', N'BindingDecision', N'Binding Decision', N'Carrier communicated an approval, rejection, or bind confirmation.', 30);

MERGE Submissions.SubmissionReferenceOption AS target
USING (SELECT t.TenantId, s.* FROM Core.Tenant t CROSS JOIN @OptionSeed s) AS source
ON target.TenantId = source.TenantId AND target.OptionGroup = source.OptionGroup AND target.OptionCode = source.OptionCode
WHEN MATCHED THEN UPDATE SET OptionName = source.OptionName, Description = source.Description, SortOrder = source.SortOrder, IsActive = 1, IsDeleted = 0, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (TenantId, OptionGroup, OptionCode, OptionName, Description, SortOrder, IsActive, CreatedDateUtc, IsDeleted) VALUES (source.TenantId, source.OptionGroup, source.OptionCode, source.OptionName, source.Description, source.SortOrder, 1, SYSUTCDATETIME(), 0);
GO

DECLARE @RequirementSeed TABLE (RequirementCode NVARCHAR(100), RequirementName NVARCHAR(200), RequirementTypeCode NVARCHAR(50), DocumentCategoryCode NVARCHAR(100), SortOrder INT);
INSERT INTO @RequirementSeed VALUES
(N'SignedApplication', N'Signed Application', N'Document', N'SignedApplication', 10),
(N'SignedProposal', N'Signed Proposal', N'Document', N'SignedProposal', 20),
(N'AcordForms', N'ACORD Forms', N'Document', N'ACORD', 30),
(N'SupplementalForms', N'Supplemental Forms', N'Document', N'Supplemental', 40),
(N'LossRuns', N'Loss Runs', N'Document', N'LossRuns', 50),
(N'Photos', N'Photos', N'Document', N'Photos', 60),
(N'Inspection', N'Inspection', N'Underwriting', N'Inspection', 70),
(N'PaymentAuthorization', N'Payment Authorization', N'Payment', N'PaymentAuthorization', 80),
(N'FinancingAgreement', N'Financing Agreement', N'Payment', N'FinancingAgreement', 90),
(N'CarrierQuestionnaire', N'Carrier Questionnaire', N'Underwriting', N'CarrierQuestionnaire', 100),
(N'ProposalAccepted', N'Proposal Accepted', N'Compliance', NULL, 110),
(N'QuoteBindable', N'Quote Is Bindable', N'Underwriting', NULL, 120),
(N'EffectiveDateValid', N'Effective Date Valid', N'Compliance', NULL, 130),
(N'UnderwritingComplete', N'Underwriting Questions Complete', N'Underwriting', NULL, 140),
(N'ProducerLicensed', N'Producer Licensed', N'Compliance', NULL, 150),
(N'CarrierAppointment', N'Carrier Appointment Active', N'Compliance', NULL, 160),
(N'AgencyAppointment', N'Agency Appointment Active', N'Compliance', NULL, 170),
(N'PremiumCalculated', N'Premium Calculated', N'Financial', NULL, 180),
(N'TaxesCalculated', N'Taxes And Fees Calculated', N'Financial', NULL, 190),
(N'RequiredNotes', N'Required Notes Entered', N'Compliance', NULL, 200),
(N'RequiredActivities', N'Required Activities Complete', N'Activity', NULL, 210),
(N'ComplianceClear', N'Compliance And DNC Clear', N'Compliance', NULL, 220),
(N'OutstandingInspection', N'No Pending Inspection', N'Underwriting', NULL, 230),
(N'OutstandingSurvey', N'No Pending Survey', N'Underwriting', NULL, 240),
(N'OutstandingMvr', N'No Pending MVR', N'Underwriting', NULL, 250),
(N'OutstandingClaimsReview', N'No Pending Claims Review', N'Underwriting', NULL, 260),
(N'DownPaymentVerified', N'Down Payment Verified', N'Payment', NULL, 270);

MERGE Submissions.BindRequirement AS target
USING (SELECT t.TenantId, s.* FROM Core.Tenant t CROSS JOIN @RequirementSeed s) AS source
ON target.TenantId = source.TenantId AND target.CarrierId IS NULL AND target.LineOfBusiness IS NULL AND target.RequirementCode = source.RequirementCode
WHEN MATCHED THEN UPDATE SET RequirementName = source.RequirementName, RequirementTypeCode = source.RequirementTypeCode, DocumentCategoryCode = source.DocumentCategoryCode, IsRequired = 1, BlocksSubmission = 1, IsActive = 1, SortOrder = source.SortOrder, IsDeleted = 0, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (TenantId, RequirementCode, RequirementName, RequirementTypeCode, DocumentCategoryCode, IsRequired, BlocksSubmission, IsActive, SortOrder, CreatedDateUtc, IsDeleted) VALUES (source.TenantId, source.RequirementCode, source.RequirementName, source.RequirementTypeCode, source.DocumentCategoryCode, 1, 1, 1, source.SortOrder, SYSUTCDATETIME(), 0);
GO
