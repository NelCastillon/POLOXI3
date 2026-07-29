SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'Submissions.Proposal', N'GovernanceStatusCode') IS NULL ALTER TABLE Submissions.Proposal ADD GovernanceStatusCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'CurrentReviewId') IS NULL ALTER TABLE Submissions.Proposal ADD CurrentReviewId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ApprovedDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD ApprovedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ApprovedByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD ApprovedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ApprovalVersionNumber') IS NULL ALTER TABLE Submissions.Proposal ADD ApprovalVersionNumber INT NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ApprovedSnapshotHash') IS NULL ALTER TABLE Submissions.Proposal ADD ApprovedSnapshotHash CHAR(64) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ReadyToDeliverDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD ReadyToDeliverDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DeliveryConfirmedDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD DeliveryConfirmedDateUtc DATETIME2 NULL;
GO

UPDATE Submissions.Proposal
SET GovernanceStatusCode = CASE
	WHEN Status IN (N'Accepted', N'Bind Requested', N'Bound') THEN Status
	WHEN PresentedDateUtc IS NOT NULL OR Status = N'Presented' THEN N'Presented'
	WHEN DeliveryStatus = N'Delivered' OR Status = N'Delivered' THEN N'Delivered'
	WHEN Status IN (N'Approved', N'Ready to Deliver') THEN N'ReadyToDeliver'
	ELSE N'Draft'
END
WHERE GovernanceStatusCode IS NULL;
ALTER TABLE Submissions.Proposal ALTER COLUMN GovernanceStatusCode NVARCHAR(50) NOT NULL;
GO

UPDATE Submissions.SubmissionReferenceOption
SET IsDeleted = 1,
	ModifiedDateUtc = SYSUTCDATETIME()
WHERE OptionGroup = N'ProposalClientDecision'
	AND IsDeleted = 0;
GO

IF OBJECT_ID(N'Submissions.ProposalReview', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalReview
	(
		ProposalReviewId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalReview PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		ProposalVersionNumber INT NOT NULL,
		ReviewRound INT NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		AssignedReviewerUserId UNIQUEIDENTIFIER NOT NULL,
		RequestedByUserId UNIQUEIDENTIFIER NULL,
		RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalReview_Requested DEFAULT SYSUTCDATETIME(),
		DueDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		DecisionNotes NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalReview_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalReview_Deleted DEFAULT 0,
		CONSTRAINT CK_ProposalReview_Status CHECK (StatusCode IN (N'Pending', N'Approved', N'ChangesRequired', N'Rejected', N'Cancelled')),
		CONSTRAINT CK_ProposalReview_Round CHECK (ReviewRound > 0)
	);
END;
GO

IF OBJECT_ID(N'Submissions.ProposalRecipient', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalRecipient
	(
		ProposalRecipientId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalRecipient PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		ContactId UNIQUEIDENTIFIER NULL,
		RecipientTypeCode NVARCHAR(50) NOT NULL,
		RecipientName NVARCHAR(200) NOT NULL,
		RecipientEmail NVARCHAR(320) NOT NULL,
		SigningOrder INT NOT NULL CONSTRAINT DF_ProposalRecipient_Order DEFAULT 1,
		IsPrimary BIT NOT NULL CONSTRAINT DF_ProposalRecipient_Primary DEFAULT 0,
		IsSigner BIT NOT NULL CONSTRAINT DF_ProposalRecipient_Signer DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalRecipient_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalRecipient_Deleted DEFAULT 0,
		CONSTRAINT CK_ProposalRecipient_Type CHECK (RecipientTypeCode IN (N'Client', N'Cc', N'Signer', N'Agency')),
		CONSTRAINT CK_ProposalRecipient_Order CHECK (SigningOrder > 0)
	);
END;
GO

IF OBJECT_ID(N'Submissions.ProposalApprovedSnapshot', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalApprovedSnapshot
	(
		ProposalApprovedSnapshotId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalApprovedSnapshot PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		ProposalVersionNumber INT NOT NULL,
		SnapshotHash CHAR(64) NOT NULL,
		SnapshotJson NVARCHAR(MAX) NOT NULL,
		ApprovedDateUtc DATETIME2 NOT NULL,
		ApprovedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalSnapshot_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT CK_ProposalSnapshot_Json CHECK (ISJSON(SnapshotJson) = 1),
		CONSTRAINT CK_ProposalSnapshot_Hash CHECK (LEN(SnapshotHash) = 64)
	);
END;
GO

IF OBJECT_ID(N'Submissions.ProposalESignEnvelope', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalESignEnvelope
	(
		ProposalESignEnvelopeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalESignEnvelope PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		ProposalVersionNumber INT NOT NULL,
		ProposalDeliveryDispatchId UNIQUEIDENTIFIER NOT NULL,
		ESignRequestId UNIQUEIDENTIFIER NULL,
		ProposalDeliveryProviderId UNIQUEIDENTIFIER NOT NULL,
		ProviderCode NVARCHAR(100) NOT NULL,
		ExternalEnvelopeId NVARCHAR(500) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		SentDateUtc DATETIME2 NULL,
		DeliveredDateUtc DATETIME2 NULL,
		FirstViewedDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		DeclinedDateUtc DATETIME2 NULL,
		ExpiredDateUtc DATETIME2 NULL,
		VoidedDateUtc DATETIME2 NULL,
		SignedDocumentId UNIQUEIDENTIFIER NULL,
		CertificateDocumentId UNIQUEIDENTIFIER NULL,
		LastProviderEventId NVARCHAR(500) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalEnvelope_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalEnvelope_Deleted DEFAULT 0,
		CONSTRAINT CK_ProposalEnvelope_Status CHECK (StatusCode IN (N'Created', N'Sent', N'Delivered', N'Viewed', N'Downloaded', N'Signed', N'Declined', N'Expired', N'Bounced', N'Cancelled', N'Failed'))
	);
END;
GO

IF OBJECT_ID(N'Submissions.ProposalProviderCallback', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalProviderCallback
	(
		ProposalProviderCallbackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalProviderCallback PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ProposalDeliveryProviderId UNIQUEIDENTIFIER NOT NULL,
		ProposalDeliveryDispatchId UNIQUEIDENTIFIER NULL,
		ProposalESignEnvelopeId UNIQUEIDENTIFIER NULL,
		ProviderCode NVARCHAR(100) NOT NULL,
		ProviderEventId NVARCHAR(500) NOT NULL,
		ExternalEnvelopeId NVARCHAR(500) NULL,
		EventTypeCode NVARCHAR(100) NOT NULL,
		NormalizedStatusCode NVARCHAR(50) NOT NULL,
		SignatureHeader NVARCHAR(2000) NOT NULL,
		PayloadJson NVARCHAR(MAX) NOT NULL,
		PayloadHash CHAR(64) NOT NULL,
		IsSignatureValid BIT NOT NULL,
		IsProcessed BIT NOT NULL CONSTRAINT DF_ProposalCallback_Processed DEFAULT 0,
		ReceivedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalCallback_Received DEFAULT SYSUTCDATETIME(),
		ProcessedDateUtc DATETIME2 NULL,
		ProcessingError NVARCHAR(2000) NULL,
		CONSTRAINT CK_ProposalCallback_Json CHECK (ISJSON(PayloadJson) = 1),
		CONSTRAINT CK_ProposalCallback_Hash CHECK (LEN(PayloadHash) = 64),
		CONSTRAINT CK_ProposalCallback_Status CHECK (NormalizedStatusCode IN (N'Sent', N'Delivered', N'Viewed', N'Downloaded', N'Signed', N'Declined', N'Expired', N'Bounced', N'Cancelled', N'Failed'))
	);
END;
GO

IF OBJECT_ID(N'Submissions.ProposalSlaPolicy', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalSlaPolicy
	(
		ProposalSlaPolicyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalSlaPolicy PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EventCode NVARCHAR(100) NOT NULL,
		DueAfterMinutes INT NOT NULL,
		EscalateAfterMinutes INT NULL,
		PriorityCode NVARCHAR(50) NOT NULL,
		AssignedRoleCode NVARCHAR(100) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_ProposalSlaPolicy_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalSlaPolicy_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalSlaPolicy_Deleted DEFAULT 0,
		CONSTRAINT CK_ProposalSlaPolicy_Due CHECK (DueAfterMinutes > 0 AND (EscalateAfterMinutes IS NULL OR EscalateAfterMinutes >= DueAfterMinutes))
	);
END;
GO

IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'ProposalVersionNumber') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD ProposalVersionNumber INT NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'FirstViewedDateUtc') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD FirstViewedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'LastViewedDateUtc') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD LastViewedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'DownloadedDateUtc') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD DownloadedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'SignedDateUtc') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD SignedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'DeclinedDateUtc') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD DeclinedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'ExpiredDateUtc') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD ExpiredDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'BouncedDateUtc') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD BouncedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'CancelledDateUtc') IS NULL ALTER TABLE Submissions.ProposalDeliveryDispatch ADD CancelledDateUtc DATETIME2 NULL;
GO

UPDATE dispatch SET ProposalVersionNumber = proposal.VersionNumber
FROM Submissions.ProposalDeliveryDispatch dispatch INNER JOIN Submissions.Proposal proposal ON proposal.ProposalId = dispatch.ProposalId
WHERE dispatch.ProposalVersionNumber IS NULL;
ALTER TABLE Submissions.ProposalDeliveryDispatch ALTER COLUMN ProposalVersionNumber INT NOT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ProposalDeliveryDispatch_Status' AND parent_object_id = OBJECT_ID(N'Submissions.ProposalDeliveryDispatch'))
	ALTER TABLE Submissions.ProposalDeliveryDispatch DROP CONSTRAINT CK_ProposalDeliveryDispatch_Status;
ALTER TABLE Submissions.ProposalDeliveryDispatch WITH CHECK ADD CONSTRAINT CK_ProposalDeliveryDispatch_Status CHECK (StatusCode IN (N'Queued', N'Processing', N'Configuration Required', N'Sent', N'Delivered', N'Viewed', N'Downloaded', N'Signed', N'Declined', N'Expired', N'Bounced', N'Failed', N'Cancelled'));
GO

IF COL_LENGTH(N'DMS.ESignRequest', N'ProposalId') IS NULL ALTER TABLE DMS.ESignRequest ADD ProposalId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.ESignRequest', N'ProposalVersionNumber') IS NULL ALTER TABLE DMS.ESignRequest ADD ProposalVersionNumber INT NULL;
IF COL_LENGTH(N'DMS.ESignRequest', N'ProposalDeliveryDispatchId') IS NULL ALTER TABLE DMS.ESignRequest ADD ProposalDeliveryDispatchId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.ESignRequest', N'ProposalESignEnvelopeId') IS NULL ALTER TABLE DMS.ESignRequest ADD ProposalESignEnvelopeId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.ESignRequest', N'ProviderCode') IS NULL ALTER TABLE DMS.ESignRequest ADD ProviderCode NVARCHAR(100) NULL;
IF COL_LENGTH(N'DMS.ESignRequest', N'ExternalEnvelopeId') IS NULL ALTER TABLE DMS.ESignRequest ADD ExternalEnvelopeId NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalReview') AND name = N'UX_ProposalReview_Active') CREATE UNIQUE INDEX UX_ProposalReview_Active ON Submissions.ProposalReview(TenantId, ProposalId) WHERE IsDeleted = 0 AND StatusCode = N'Pending';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalRecipient') AND name = N'UX_ProposalRecipient_Email') CREATE UNIQUE INDEX UX_ProposalRecipient_Email ON Submissions.ProposalRecipient(TenantId, ProposalId, RecipientEmail, RecipientTypeCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalApprovedSnapshot') AND name = N'UX_ProposalSnapshot_Version') CREATE UNIQUE INDEX UX_ProposalSnapshot_Version ON Submissions.ProposalApprovedSnapshot(TenantId, ProposalId, ProposalVersionNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalESignEnvelope') AND name = N'UX_ProposalEnvelope_ProviderExternal') CREATE UNIQUE INDEX UX_ProposalEnvelope_ProviderExternal ON Submissions.ProposalESignEnvelope(TenantId, ProviderCode, ExternalEnvelopeId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalProviderCallback') AND name = N'UX_ProposalCallback_ProviderEvent') CREATE UNIQUE INDEX UX_ProposalCallback_ProviderEvent ON Submissions.ProposalProviderCallback(TenantId, ProviderCode, ProviderEventId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalSlaPolicy') AND name = N'UX_ProposalSlaPolicy_Event') CREATE UNIQUE INDEX UX_ProposalSlaPolicy_Event ON Submissions.ProposalSlaPolicy(TenantId, EventCode) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProposalReview_Proposal') ALTER TABLE Submissions.ProposalReview WITH CHECK ADD CONSTRAINT FK_ProposalReview_Proposal FOREIGN KEY (ProposalId) REFERENCES Submissions.Proposal(ProposalId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProposalRecipient_Proposal') ALTER TABLE Submissions.ProposalRecipient WITH CHECK ADD CONSTRAINT FK_ProposalRecipient_Proposal FOREIGN KEY (ProposalId) REFERENCES Submissions.Proposal(ProposalId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProposalSnapshot_Proposal') ALTER TABLE Submissions.ProposalApprovedSnapshot WITH CHECK ADD CONSTRAINT FK_ProposalSnapshot_Proposal FOREIGN KEY (ProposalId) REFERENCES Submissions.Proposal(ProposalId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProposalEnvelope_Proposal') ALTER TABLE Submissions.ProposalESignEnvelope WITH CHECK ADD CONSTRAINT FK_ProposalEnvelope_Proposal FOREIGN KEY (ProposalId) REFERENCES Submissions.Proposal(ProposalId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProposalEnvelope_Dispatch') ALTER TABLE Submissions.ProposalESignEnvelope WITH CHECK ADD CONSTRAINT FK_ProposalEnvelope_Dispatch FOREIGN KEY (ProposalDeliveryDispatchId) REFERENCES Submissions.ProposalDeliveryDispatch(ProposalDeliveryDispatchId);
GO

IF OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
BEGIN
	DECLARE @PermissionTenantId UNIQUEIDENTIFIER = (SELECT TOP 1 TenantId FROM Core.Tenant WHERE IsDeleted = 0 ORDER BY TenantId);
	DECLARE @ReadActionId INT = (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode) = N'READ' OR UPPER(ActionName) = N'READ' ORDER BY PermissionActionId);
	DECLARE @WriteActionId INT = (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode) IN (N'WRITE', N'MANAGE') OR UPPER(ActionName) IN (N'WRITE', N'MANAGE') ORDER BY PermissionActionId);
	DECLARE @ProposalPermissions TABLE (PermissionCode NVARCHAR(200) PRIMARY KEY, PermissionName NVARCHAR(200), ActionCode NVARCHAR(100), PermissionActionId INT, Description NVARCHAR(500));
	INSERT INTO @ProposalPermissions VALUES
		(N'PROPOSAL_VIEW', N'View Proposals', N'Read', @ReadActionId, N'View proposal versions, recipients, delivery, approval, and lifecycle evidence.'),
		(N'PROPOSAL_CREATE', N'Create Proposals', N'Write', @WriteActionId, N'Create and revise proposal drafts from persisted quote data.'),
		(N'PROPOSAL_REVIEW', N'Review Proposals', N'Manage', @WriteActionId, N'Perform assigned internal proposal reviews.'),
		(N'PROPOSAL_APPROVE', N'Approve Proposals', N'Manage', @WriteActionId, N'Approve immutable proposal versions for delivery.'),
		(N'PROPOSAL_DELIVER', N'Deliver Proposals', N'Write', @WriteActionId, N'Deliver approved proposal versions through configured providers.'),
		(N'PROPOSAL_WEBHOOK_MANAGE', N'Manage Proposal Provider Callbacks', N'Manage', @WriteActionId, N'Configure and audit authenticated provider callbacks.'),
		(N'PROPOSAL_SLA_MANAGE', N'Manage Proposal SLA Policies', N'Manage', @WriteActionId, N'Configure proposal review, delivery, and client-response SLA policies.');
	UPDATE existing SET PermissionName = source.PermissionName, ResourceCode = N'Proposal', ActionCode = source.ActionCode, PermissionActionId = COALESCE(source.PermissionActionId, existing.PermissionActionId), ModuleCode = N'Submissions', Description = source.Description, IsBuiltIn = 1, IsActive = 1, IsDeleted = 0, ModifiedDateUtc = SYSUTCDATETIME()
	FROM IAM.Permission existing INNER JOIN @ProposalPermissions source ON source.PermissionCode = existing.PermissionCode;
	INSERT INTO IAM.Permission (PermissionId, TenantId, PermissionCode, PermissionName, ResourceCode, ActionCode, PermissionActionId, ModuleCode, Description, IsBuiltIn, IsActive, CreatedDateUtc, IsDeleted)
	SELECT NEWID(), @PermissionTenantId, source.PermissionCode, source.PermissionName, N'Proposal', source.ActionCode, source.PermissionActionId, N'Submissions', source.Description, 1, 1, SYSUTCDATETIME(), 0
	FROM @ProposalPermissions source WHERE @PermissionTenantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM IAM.Permission existing WHERE existing.PermissionCode = source.PermissionCode);
END;
GO

IF OBJECT_ID(N'Submissions.SubmissionReferenceOption', N'U') IS NOT NULL
BEGIN
	INSERT INTO Submissions.SubmissionReferenceOption (SubmissionReferenceOptionId, TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
	SELECT NEWID(), tenant.TenantId, source.OptionGroup, source.OptionCode, source.OptionName, source.Description, source.IsDefault, source.SortOrder, 1, SYSUTCDATETIME(), 0
	FROM Core.Tenant tenant
	CROSS JOIN (VALUES
		(N'ProposalGovernanceStatus', N'Draft', N'Draft', N'Proposal can be edited and submitted for review.', CAST(1 AS bit), 10),
		(N'ProposalGovernanceStatus', N'InternalReview', N'Internal Review', N'Proposal is awaiting assigned reviewer action.', CAST(0 AS bit), 20),
		(N'ProposalGovernanceStatus', N'ChangesRequired', N'Changes Required', N'Reviewer returned the proposal for revision.', CAST(0 AS bit), 30),
		(N'ProposalGovernanceStatus', N'Approved', N'Approved', N'Proposal version has an immutable approval snapshot.', CAST(0 AS bit), 40),
		(N'ProposalGovernanceStatus', N'ReadyToDeliver', N'Ready to Deliver', N'Approved proposal version may be delivered.', CAST(0 AS bit), 50),
		(N'ProposalRecipientType', N'Client', N'Client Recipient', N'Primary client proposal recipient.', CAST(1 AS bit), 10),
		(N'ProposalRecipientType', N'Signer', N'Signer', N'Recipient required to sign through an e-sign provider.', CAST(0 AS bit), 20),
		(N'ProposalRecipientType', N'Cc', N'Copy Recipient', N'Recipient copied on proposal delivery.', CAST(0 AS bit), 30),
		(N'ProposalCallbackStatus', N'Sent', N'Sent', N'Provider accepted the delivery request.', CAST(1 AS bit), 10),
		(N'ProposalCallbackStatus', N'Delivered', N'Delivered', N'Provider confirmed recipient delivery.', CAST(0 AS bit), 20),
		(N'ProposalCallbackStatus', N'Viewed', N'Viewed', N'Recipient viewed the proposal.', CAST(0 AS bit), 30),
		(N'ProposalCallbackStatus', N'Downloaded', N'Downloaded', N'Recipient downloaded proposal evidence.', CAST(0 AS bit), 40),
		(N'ProposalCallbackStatus', N'Signed', N'Signed', N'Recipient completed the signature envelope.', CAST(0 AS bit), 50),
		(N'ProposalCallbackStatus', N'Declined', N'Declined', N'Recipient declined the proposal or envelope.', CAST(0 AS bit), 60),
		(N'ProposalCallbackStatus', N'Expired', N'Expired', N'Delivery or envelope expired.', CAST(0 AS bit), 70),
		(N'ProposalCallbackStatus', N'Bounced', N'Bounced', N'Delivery provider reported a bounce.', CAST(0 AS bit), 80),
		(N'ProposalCallbackStatus', N'Cancelled', N'Cancelled', N'Delivery or envelope was cancelled.', CAST(0 AS bit), 90),
		(N'ProposalCallbackStatus', N'Failed', N'Failed', N'Provider reported an unrecoverable failure.', CAST(0 AS bit), 100)
	) source(OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
	WHERE tenant.IsDeleted = 0 AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption existing WHERE existing.TenantId = tenant.TenantId AND existing.OptionGroup = source.OptionGroup AND existing.OptionCode = source.OptionCode AND existing.IsDeleted = 0);
END;
GO
