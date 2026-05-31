-- =============================================================================
-- Migration 0053: Account360 Enhancement Schema
-- Creates tables for AccountActivity, AccountCommunication, AccountRelationship,
-- Submission, and MarketingCampaignEnrollment
-- =============================================================================

-- ══════════════════════════════════════════════════════════════════════════════
-- ACCOUNT ACTIVITY
-- ══════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountActivity' AND schema_id = SCHEMA_ID('Client'))
BEGIN
	CREATE TABLE Client.AccountActivity (
		ActivityId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		AccountId           UNIQUEIDENTIFIER NOT NULL,
		ActivityType        NVARCHAR(50)     NOT NULL, -- Call, Email, Meeting, Note, Task
		[Subject]           NVARCHAR(200)    NOT NULL,
		Notes               NVARCHAR(MAX)    NULL,
		OccurredAtUtc       DATETIME2        NOT NULL,
		Outcome             NVARCHAR(100)    NULL,
		DurationMinutes     INT              NULL,
		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT PK_AccountActivity PRIMARY KEY (ActivityId),
		CONSTRAINT FK_AccountActivity_Account FOREIGN KEY (AccountId) REFERENCES Client.Account(AccountId),
		CONSTRAINT CK_AccountActivity_Type CHECK (ActivityType IN ('Call', 'Email', 'Meeting', 'Note', 'Task', 'Other'))
	);

	CREATE NONCLUSTERED INDEX IX_AccountActivity_Account ON Client.AccountActivity (AccountId, OccurredAtUtc DESC) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_AccountActivity_Type ON Client.AccountActivity (ActivityType, OccurredAtUtc DESC) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_AccountActivity_Tenant ON Client.AccountActivity (TenantId) WHERE IsDeleted = 0;

	PRINT 'Created Client.AccountActivity table';
END
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- ACCOUNT COMMUNICATION
-- ══════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountCommunication' AND schema_id = SCHEMA_ID('Client'))
BEGIN
	CREATE TABLE Client.AccountCommunication (
		CommunicationId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		AccountId           UNIQUEIDENTIFIER NOT NULL,
		ContactId           UNIQUEIDENTIFIER NULL,
		Channel             NVARCHAR(50)     NOT NULL, -- Email, Phone, SMS, Portal, Chat
		Direction           NVARCHAR(20)     NOT NULL DEFAULT 'Outbound', -- Inbound, Outbound
		[Subject]           NVARCHAR(200)    NOT NULL,
		MessagePreview      NVARCHAR(500)    NULL,
		FullMessageBody     NVARCHAR(MAX)    NULL,
		SentAtUtc           DATETIME2        NOT NULL,
		WasOpened           BIT              NULL,
		OpenedAtUtc         DATETIME2        NULL,
		WasClicked          BIT              NULL,
		ClickedAtUtc        DATETIME2        NULL,
		ExternalMessageId   NVARCHAR(100)    NULL,
		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT PK_AccountCommunication PRIMARY KEY (CommunicationId),
		CONSTRAINT FK_AccountCommunication_Account FOREIGN KEY (AccountId) REFERENCES Client.Account(AccountId),
		CONSTRAINT FK_AccountCommunication_Contact FOREIGN KEY (ContactId) REFERENCES Client.Contact(ContactId),
		CONSTRAINT CK_AccountCommunication_Channel CHECK (Channel IN ('Email', 'Phone', 'SMS', 'Portal', 'Chat', 'Other')),
		CONSTRAINT CK_AccountCommunication_Direction CHECK (Direction IN ('Inbound', 'Outbound'))
	);

	CREATE NONCLUSTERED INDEX IX_AccountCommunication_Account ON Client.AccountCommunication (AccountId, SentAtUtc DESC) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_AccountCommunication_Channel ON Client.AccountCommunication (Channel, SentAtUtc DESC) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_AccountCommunication_Tenant ON Client.AccountCommunication (TenantId) WHERE IsDeleted = 0;

	PRINT 'Created Client.AccountCommunication table';
END
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- ACCOUNT RELATIONSHIP
-- ══════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountRelationship' AND schema_id = SCHEMA_ID('Client'))
BEGIN
	CREATE TABLE Client.AccountRelationship (
		RelationshipId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		AccountId           UNIQUEIDENTIFIER NOT NULL,
		RelatedAccountId    UNIQUEIDENTIFIER NOT NULL,
		RelationshipType    NVARCHAR(50)     NOT NULL, -- Parent, Subsidiary, Partner, Affiliated, Referred By
		[Description]       NVARCHAR(500)    NULL,
		IsActive            BIT              NOT NULL DEFAULT 1,
		StartedAtUtc        DATETIME2        NULL,
		EndedAtUtc          DATETIME2        NULL,
		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT PK_AccountRelationship PRIMARY KEY (RelationshipId),
		CONSTRAINT FK_AccountRelationship_Account FOREIGN KEY (AccountId) REFERENCES Client.Account(AccountId),
		CONSTRAINT FK_AccountRelationship_RelatedAccount FOREIGN KEY (RelatedAccountId) REFERENCES Client.Account(AccountId),
		CONSTRAINT CK_AccountRelationship_Type CHECK (RelationshipType IN ('Parent', 'Subsidiary', 'Partner', 'Affiliated', 'Referred By', 'Other')),
		CONSTRAINT CK_AccountRelationship_DifferentAccounts CHECK (AccountId <> RelatedAccountId)
	);

	CREATE NONCLUSTERED INDEX IX_AccountRelationship_Account ON Client.AccountRelationship (AccountId, IsActive DESC) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_AccountRelationship_RelatedAccount ON Client.AccountRelationship (RelatedAccountId, IsActive DESC) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_AccountRelationship_Type ON Client.AccountRelationship (RelationshipType) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_AccountRelationship_Tenant ON Client.AccountRelationship (TenantId) WHERE IsDeleted = 0;

	PRINT 'Created Client.AccountRelationship table';
END
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- SUBMISSION
-- ══════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Submission' AND schema_id = SCHEMA_ID('CRM'))
BEGIN
	CREATE TABLE CRM.Submission (
		SubmissionId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		AccountId           UNIQUEIDENTIFIER NOT NULL,
		SubmissionNumber    NVARCHAR(50)     NOT NULL,
		LineOfBusiness      NVARCHAR(100)    NOT NULL,
		CarrierId           UNIQUEIDENTIFIER NULL,
		CarrierName         NVARCHAR(150)    NULL,
		StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Submitted', -- Submitted, Quoted, Bound, Declined, Expired
		SubmittedAtUtc      DATETIME2        NOT NULL,
		DueDateUtc          DATETIME2        NULL,
		QuotedAtUtc         DATETIME2        NULL,
		QuotedPremium       DECIMAL(18,2)    NULL,
		BoundAtUtc          DATETIME2        NULL,
		Notes               NVARCHAR(MAX)    NULL,
		DeclineReason       NVARCHAR(500)    NULL,
		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT PK_Submission PRIMARY KEY (SubmissionId),
		CONSTRAINT FK_Submission_Account FOREIGN KEY (AccountId) REFERENCES Client.Account(AccountId),
		CONSTRAINT FK_Submission_Carrier FOREIGN KEY (CarrierId) REFERENCES Agency.Carrier(CarrierId),
		CONSTRAINT CK_Submission_Status CHECK (StatusCode IN ('Submitted', 'Quoted', 'Bound', 'Declined', 'Expired')),
		CONSTRAINT UQ_Submission_Number UNIQUE (TenantId, SubmissionNumber)
	);

	CREATE NONCLUSTERED INDEX IX_Submission_Account ON CRM.Submission (AccountId, SubmittedAtUtc DESC) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_Submission_Status ON CRM.Submission (StatusCode, DueDateUtc) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_Submission_Carrier ON CRM.Submission (CarrierId) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_Submission_Tenant ON CRM.Submission (TenantId) WHERE IsDeleted = 0;

	PRINT 'Created CRM.Submission table';
END
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- MARKETING CAMPAIGN ENROLLMENT
-- ══════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Marketing')
BEGIN
	EXEC('CREATE SCHEMA Marketing');
	PRINT 'Created Marketing schema';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CampaignEnrollment' AND schema_id = SCHEMA_ID('Marketing'))
BEGIN
	CREATE TABLE Marketing.CampaignEnrollment (
		EnrollmentId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		AccountId           UNIQUEIDENTIFIER NOT NULL,
		CampaignId          UNIQUEIDENTIFIER NOT NULL,
		CampaignName        NVARCHAR(200)    NOT NULL,
		StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Active', -- Active, Completed, Paused, OptedOut
		EnrolledAtUtc       DATETIME2        NOT NULL,
		CompletedAtUtc      DATETIME2        NULL,
		EmailsSent          INT              NOT NULL DEFAULT 0,
		EmailsOpened        INT              NOT NULL DEFAULT 0,
		EmailsClicked       INT              NOT NULL DEFAULT 0,
		LastContactUtc      DATETIME2        NULL,
		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT PK_CampaignEnrollment PRIMARY KEY (EnrollmentId),
		CONSTRAINT FK_CampaignEnrollment_Account FOREIGN KEY (AccountId) REFERENCES Client.Account(AccountId),
		CONSTRAINT CK_CampaignEnrollment_Status CHECK (StatusCode IN ('Active', 'Completed', 'Paused', 'OptedOut')),
		CONSTRAINT CK_CampaignEnrollment_Stats CHECK (EmailsOpened <= EmailsSent AND EmailsClicked <= EmailsOpened)
	);

	CREATE NONCLUSTERED INDEX IX_CampaignEnrollment_Account ON Marketing.CampaignEnrollment (AccountId, StatusCode) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_CampaignEnrollment_Campaign ON Marketing.CampaignEnrollment (CampaignId, StatusCode) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_CampaignEnrollment_Status ON Marketing.CampaignEnrollment (StatusCode, EnrolledAtUtc DESC) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_CampaignEnrollment_Tenant ON Marketing.CampaignEnrollment (TenantId) WHERE IsDeleted = 0;

	PRINT 'Created Marketing.CampaignEnrollment table';
END
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- UPDATE ACCOUNT TABLE WITH NEW FIELDS
-- ══════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'Street')
BEGIN
	ALTER TABLE Client.Account ADD Street NVARCHAR(200) NULL;
	PRINT 'Added Street to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'City')
BEGIN
	ALTER TABLE Client.Account ADD City NVARCHAR(100) NULL;
	PRINT 'Added City to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'State')
BEGIN
	ALTER TABLE Client.Account ADD [State] NVARCHAR(50) NULL;
	PRINT 'Added State to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'Zip')
BEGIN
	ALTER TABLE Client.Account ADD Zip NVARCHAR(20) NULL;
	PRINT 'Added Zip to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'Country')
BEGIN
	ALTER TABLE Client.Account ADD Country NVARCHAR(50) NULL DEFAULT 'USA';
	PRINT 'Added Country to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'Employees')
BEGIN
	ALTER TABLE Client.Account ADD Employees INT NULL;
	PRINT 'Added Employees to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'TaxId')
BEGIN
	ALTER TABLE Client.Account ADD TaxId NVARCHAR(50) NULL;
	PRINT 'Added TaxId to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'NaicsCode')
BEGIN
	ALTER TABLE Client.Account ADD NaicsCode NVARCHAR(20) NULL;
	PRINT 'Added NaicsCode to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'SegmentCode')
BEGIN
	ALTER TABLE Client.Account ADD SegmentCode NVARCHAR(50) NULL;
	PRINT 'Added SegmentCode to Client.Account';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'ServicingTeamId')
BEGIN
	ALTER TABLE Client.Account ADD ServicingTeamId UNIQUEIDENTIFIER NULL;
	PRINT 'Added ServicingTeamId to Client.Account';
END

PRINT 'Migration 0053 completed: Account360 Enhancement Schema';
GO
