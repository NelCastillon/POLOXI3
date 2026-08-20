IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Submissions') EXEC(N'CREATE SCHEMA Submissions');
GO

IF OBJECT_ID(N'Submissions.SubmissionIntakeTemplate', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.SubmissionIntakeTemplate
	(
		IntakeTemplateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionIntakeTemplate PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		LineOfBusiness NVARCHAR(100) NOT NULL,
		QuestionCode NVARCHAR(100) NOT NULL,
		QuestionText NVARCHAR(500) NOT NULL,
		HelpText NVARCHAR(1000) NULL,
		IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_IsRequired DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_SortOrder DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_IsDeleted DEFAULT 0
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionIntakeTemplate') AND name = N'UX_SubmissionIntakeTemplate_Tenant_Lob_Code')
	CREATE UNIQUE INDEX UX_SubmissionIntakeTemplate_Tenant_Lob_Code ON Submissions.SubmissionIntakeTemplate(TenantId, LineOfBusiness, QuestionCode) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'Submissions.SubmissionDocumentRequirement', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.SubmissionDocumentRequirement
	(
		DocumentRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionDocumentRequirement PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		LineOfBusiness NVARCHAR(100) NOT NULL,
		CategoryCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsRequired DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_SortOrder DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsDeleted DEFAULT 0
	);
END;
GO

IF COL_LENGTH(N'Submissions.SubmissionDocumentRequirement', N'IsActive') IS NULL ALTER TABLE Submissions.SubmissionDocumentRequirement ADD IsActive BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsActive_0054 DEFAULT 1;
IF COL_LENGTH(N'Submissions.SubmissionDocumentRequirement', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.SubmissionDocumentRequirement ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionDocumentRequirement', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionDocumentRequirement ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionDocumentRequirement', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionDocumentRequirement ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionDocumentRequirement') AND name = N'UX_SubmissionDocumentRequirement_Tenant_Lob_Code')
	CREATE UNIQUE INDEX UX_SubmissionDocumentRequirement_Tenant_Lob_Code ON Submissions.SubmissionDocumentRequirement(TenantId, LineOfBusiness, CategoryCode) WHERE IsDeleted = 0;
GO

IF COL_LENGTH(N'Submissions.Proposal', N'DeliveryMethod') IS NULL ALTER TABLE Submissions.Proposal ADD DeliveryMethod NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'Recipient') IS NULL ALTER TABLE Submissions.Proposal ADD Recipient NVARCHAR(320) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'SentDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD SentDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'SentByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD SentByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ClientDecision') IS NULL ALTER TABLE Submissions.Proposal ADD ClientDecision NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecisionNotes') IS NULL ALTER TABLE Submissions.Proposal ADD DecisionNotes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecisionDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD DecisionDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecidedByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD DecidedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DocumentId') IS NULL ALTER TABLE Submissions.Proposal ADD DocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'CustomIntroduction') IS NULL ALTER TABLE Submissions.Proposal ADD CustomIntroduction NVARCHAR(2000) NULL;
GO

IF OBJECT_ID(N'Submissions.ProposalQuote', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalQuote
	(
		ProposalQuoteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalQuote PRIMARY KEY DEFAULT NEWID(),
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		QuoteId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_ProposalQuote_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalQuote_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalQuote_IsDeleted DEFAULT 0
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalQuote') AND name = N'UX_ProposalQuote_Proposal_Quote')
	CREATE UNIQUE INDEX UX_ProposalQuote_Proposal_Quote ON Submissions.ProposalQuote(ProposalId, QuoteId) WHERE IsDeleted = 0;
GO

IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'RelatedEntityName') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD RelatedEntityName NVARCHAR(100) NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'RelatedEntityId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD RelatedEntityId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'ActionSource') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD ActionSource NVARCHAR(50) NULL;
GO
