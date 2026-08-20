SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Renewal') EXEC(N'CREATE SCHEMA Renewal');
GO

IF COL_LENGTH(N'Submissions.Proposal', N'VersionNumber') IS NULL ALTER TABLE Submissions.Proposal ADD VersionNumber INT NOT NULL CONSTRAINT DF_Proposal_VersionNumber_0251 DEFAULT 1;
IF COL_LENGTH(N'Submissions.Proposal', N'DeliveryMethod') IS NULL ALTER TABLE Submissions.Proposal ADD DeliveryMethod NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'Recipient') IS NULL ALTER TABLE Submissions.Proposal ADD Recipient NVARCHAR(320) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'SentDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD SentDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'SentByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD SentByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'PresentedDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD PresentedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'PresentedByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD PresentedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ClientDecision') IS NULL ALTER TABLE Submissions.Proposal ADD ClientDecision NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecisionNotes') IS NULL ALTER TABLE Submissions.Proposal ADD DecisionNotes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecisionDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD DecisionDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecidedByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD DecidedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DocumentId') IS NULL ALTER TABLE Submissions.Proposal ADD DocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'CustomIntroduction') IS NULL ALTER TABLE Submissions.Proposal ADD CustomIntroduction NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
GO

IF OBJECT_ID(N'Submissions.ProposalQuote', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalQuote
	(
		ProposalQuoteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalQuote PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		QuoteId UNIQUEIDENTIFIER NOT NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_ProposalQuote_SortOrder_0251 DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalQuote_Created_0251 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalQuote_Deleted_0251 DEFAULT 0
	);
END;
IF COL_LENGTH(N'Submissions.ProposalQuote', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.ProposalQuote ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.ProposalQuote', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.ProposalQuote ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.ProposalQuote', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.ProposalQuote ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalQuote') AND name = N'UX_ProposalQuote_Proposal_Quote')
	CREATE UNIQUE INDEX UX_ProposalQuote_Proposal_Quote ON Submissions.ProposalQuote(ProposalId, QuoteId) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'Submissions.ProposalLifecycleEvent', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalLifecycleEvent
	(
		ProposalLifecycleEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalLifecycleEvent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		EventCode NVARCHAR(50) NOT NULL,
		EventDetail NVARCHAR(1000) NULL,
		EventDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalLifecycleEvent_Date_0251 DEFAULT SYSUTCDATETIME(),
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalLifecycleEvent_Created_0251 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalLifecycleEvent_Deleted_0251 DEFAULT 0
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalLifecycleEvent') AND name = N'IX_ProposalLifecycleEvent_Proposal')
	CREATE INDEX IX_ProposalLifecycleEvent_Proposal ON Submissions.ProposalLifecycleEvent(TenantId, ProposalId, IsDeleted, EventDateUtc DESC);
GO

IF OBJECT_ID(N'Submissions.ProposalWorkflowOption', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalWorkflowOption
	(
		ProposalWorkflowOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalWorkflowOption PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		OptionGroupCode NVARCHAR(50) NOT NULL,
		OptionCode NVARCHAR(50) NOT NULL,
		DisplayName NVARCHAR(100) NOT NULL,
		Description NVARCHAR(500) NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_ProposalWorkflowOption_Default_0251 DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_ProposalWorkflowOption_Active_0251 DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_ProposalWorkflowOption_Sort_0251 DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalWorkflowOption_Created_0251 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalWorkflowOption_Deleted_0251 DEFAULT 0
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalWorkflowOption') AND name = N'UX_ProposalWorkflowOption_Tenant_Group_Code')
	CREATE UNIQUE INDEX UX_ProposalWorkflowOption_Tenant_Group_Code ON Submissions.ProposalWorkflowOption(TenantId, OptionGroupCode, OptionCode) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'Renewal.WorkflowOption', N'U') IS NULL
BEGIN
	CREATE TABLE Renewal.WorkflowOption
	(
		WorkflowOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RenewalWorkflowOption PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		OptionGroupCode NVARCHAR(50) NOT NULL,
		OptionCode NVARCHAR(50) NOT NULL,
		DisplayName NVARCHAR(100) NOT NULL,
		Description NVARCHAR(500) NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_RenewalWorkflowOption_Default_0251 DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_RenewalWorkflowOption_Active_0251 DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_RenewalWorkflowOption_Sort_0251 DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_RenewalWorkflowOption_Created_0251 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_RenewalWorkflowOption_Deleted_0251 DEFAULT 0
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Renewal.WorkflowOption') AND name = N'UX_RenewalWorkflowOption_Tenant_Group_Code')
	CREATE UNIQUE INDEX UX_RenewalWorkflowOption_Tenant_Group_Code ON Renewal.WorkflowOption(TenantId, OptionGroupCode, OptionCode) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'Renewal.AutomationSetting', N'U') IS NULL
BEGIN
	CREATE TABLE Renewal.AutomationSetting
	(
		AutomationSettingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RenewalAutomationSetting PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IsEnabled BIT NOT NULL CONSTRAINT DF_RenewalAutomationSetting_Enabled_0251 DEFAULT 1,
		InitiationLeadDays INT NOT NULL CONSTRAINT DF_RenewalAutomationSetting_LeadDays_0251 DEFAULT 120,
		DefaultPriorityCode NVARCHAR(20) NOT NULL CONSTRAINT DF_RenewalAutomationSetting_Priority_0251 DEFAULT N'Normal',
		DefaultStageCode NVARCHAR(40) NOT NULL CONSTRAINT DF_RenewalAutomationSetting_Stage_0251 DEFAULT N'Intake',
		CreateOpportunity BIT NOT NULL CONSTRAINT DF_RenewalAutomationSetting_Opportunity_0251 DEFAULT 1,
		CreateSubmission BIT NOT NULL CONSTRAINT DF_RenewalAutomationSetting_Submission_0251 DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_RenewalAutomationSetting_Created_0251 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_RenewalAutomationSetting_Deleted_0251 DEFAULT 0
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Renewal.AutomationSetting') AND name = N'UX_RenewalAutomationSetting_Tenant')
	CREATE UNIQUE INDEX UX_RenewalAutomationSetting_Tenant ON Renewal.AutomationSetting(TenantId) WHERE IsDeleted = 0;
GO

IF COL_LENGTH(N'Renewal.RetentionCase', N'SourcePolicyTermId') IS NULL ALTER TABLE Renewal.RetentionCase ADD SourcePolicyTermId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Renewal.RetentionCase', N'RenewalOpportunityId') IS NULL ALTER TABLE Renewal.RetentionCase ADD RenewalOpportunityId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Renewal.RetentionCase', N'RenewalSubmissionId') IS NULL ALTER TABLE Renewal.RetentionCase ADD RenewalSubmissionId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Renewal.RetentionCase', N'RenewalPolicyBindTransactionId') IS NULL ALTER TABLE Renewal.RetentionCase ADD RenewalPolicyBindTransactionId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Renewal.RetentionCase', N'ResultPolicyId') IS NULL ALTER TABLE Renewal.RetentionCase ADD ResultPolicyId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Renewal.RetentionCase', N'ResultPolicyTermId') IS NULL ALTER TABLE Renewal.RetentionCase ADD ResultPolicyTermId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Renewal.RetentionCase', N'InitiationSourceCode') IS NULL ALTER TABLE Renewal.RetentionCase ADD InitiationSourceCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Renewal.RetentionCase', N'InitiatedDateUtc') IS NULL ALTER TABLE Renewal.RetentionCase ADD InitiatedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Renewal.RetentionCase', N'CompletedDateUtc') IS NULL ALTER TABLE Renewal.RetentionCase ADD CompletedDateUtc DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Renewal.RetentionCase') AND name = N'UX_RetentionCase_SourceTerm')
	CREATE UNIQUE INDEX UX_RetentionCase_SourceTerm ON Renewal.RetentionCase(TenantId, SourcePolicyTermId) WHERE SourcePolicyTermId IS NOT NULL AND IsDeleted = 0;
GO

IF COL_LENGTH(N'Policy.PolicyTerm', N'RenewalRetentionCaseId') IS NULL ALTER TABLE Policy.PolicyTerm ADD RenewalRetentionCaseId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Policy.PolicyTerm', N'PriorPolicyTermId') IS NULL ALTER TABLE Policy.PolicyTerm ADD PriorPolicyTermId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.BoundPolicy', N'RenewalRetentionCaseId') IS NULL ALTER TABLE Submissions.BoundPolicy ADD RenewalRetentionCaseId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.BoundPolicy', N'PriorPolicyId') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PriorPolicyId UNIQUEIDENTIFIER NULL;
GO

DECLARE @SeedUserId UNIQUEIDENTIFIER = COALESCE((SELECT TOP 1 UserId FROM IAM.[User] WHERE IsDeleted = 0 ORDER BY CreatedDateUtc), '00000000-0000-0000-0000-000000000002');
DECLARE @Tenants TABLE (TenantId UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Tenants(TenantId)
SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0
UNION SELECT DISTINCT TenantId FROM Submissions.Proposal
UNION SELECT DISTINCT TenantId FROM Renewal.RetentionCase;

INSERT INTO Submissions.ProposalWorkflowOption
	(ProposalWorkflowOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, IsActive, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), t.TenantId, v.OptionGroupCode, v.OptionCode, v.DisplayName, v.Description, v.IsDefault, 1, v.SortOrder, SYSUTCDATETIME(), @SeedUserId, 0
FROM @Tenants t
CROSS JOIN (VALUES
	(N'DeliveryMethod', N'Email', N'Email', N'Deliver by email.', CAST(1 AS bit), 10),
	(N'DeliveryMethod', N'Portal', N'Customer Portal', N'Deliver through the customer portal.', CAST(0 AS bit), 20),
	(N'DeliveryMethod', N'ESignature', N'E-Signature', N'Deliver through the configured e-signature provider.', CAST(0 AS bit), 30),
	(N'DeliveryMethod', N'InPerson', N'In Person', N'Present directly to the customer.', CAST(0 AS bit), 40),
	(N'Decision', N'Accepted', N'Accepted', N'Customer accepted a proposal quote option.', CAST(0 AS bit), 10),
	(N'Decision', N'Declined', N'Declined', N'Customer declined the proposal.', CAST(0 AS bit), 20)
) v(OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM Submissions.ProposalWorkflowOption x WHERE x.TenantId=t.TenantId AND x.OptionGroupCode=v.OptionGroupCode AND x.OptionCode=v.OptionCode AND x.IsDeleted=0);

INSERT INTO Renewal.WorkflowOption
	(WorkflowOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, IsActive, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), t.TenantId, v.OptionGroupCode, v.OptionCode, v.DisplayName, v.Description, v.IsDefault, 1, v.SortOrder, SYSUTCDATETIME(), @SeedUserId, 0
FROM @Tenants t
CROSS JOIN (VALUES
	(N'Stage', N'Intake', N'Intake', N'Renewal initiated from an expiring policy term.', CAST(1 AS bit), 10),
	(N'Stage', N'RetentionDesk', N'Retention Desk', N'Retention review and outreach.', CAST(0 AS bit), 20),
	(N'Stage', N'OfferStrategy', N'Offer Strategy', N'Renewal options are being prepared.', CAST(0 AS bit), 30),
	(N'Stage', N'ProposalReady', N'Proposal Ready', N'Renewal proposal is ready.', CAST(0 AS bit), 40),
	(N'Stage', N'Remarket', N'Remarket', N'Renewal is being marketed.', CAST(0 AS bit), 50),
	(N'Stage', N'Binding', N'Binding', N'Renewal bind request is active.', CAST(0 AS bit), 60),
	(N'Stage', N'Saved', N'Saved', N'Renewal was successfully bound.', CAST(0 AS bit), 70),
	(N'Stage', N'Lost', N'Lost', N'Renewal was lost or non-renewed.', CAST(0 AS bit), 80),
	(N'OutreachStatus', N'NotStarted', N'Not Started', NULL, CAST(1 AS bit), 10),
	(N'OutreachStatus', N'NeedsOutreach', N'Needs Outreach', NULL, CAST(0 AS bit), 20),
	(N'OutreachStatus', N'ClientContacted', N'Client Contacted', NULL, CAST(0 AS bit), 30),
	(N'OutreachStatus', N'ProducerFollowUp', N'Producer Follow-Up', NULL, CAST(0 AS bit), 40),
	(N'OutreachStatus', N'ProposalSent', N'Proposal Sent', NULL, CAST(0 AS bit), 50),
	(N'OutreachStatus', N'Accepted', N'Accepted', NULL, CAST(0 AS bit), 60),
	(N'OutreachStatus', N'Declined', N'Declined', NULL, CAST(0 AS bit), 70),
	(N'Sentiment', N'Positive', N'Positive', NULL, CAST(0 AS bit), 10),
	(N'Sentiment', N'Neutral', N'Neutral', NULL, CAST(1 AS bit), 20),
	(N'Sentiment', N'Concerned', N'Concerned', NULL, CAST(0 AS bit), 30),
	(N'Sentiment', N'AtRisk', N'At Risk', NULL, CAST(0 AS bit), 40),
	(N'ActivityType', N'Call', N'Call', NULL, CAST(1 AS bit), 10),
	(N'ActivityType', N'Email', N'Email', NULL, CAST(0 AS bit), 20),
	(N'ActivityType', N'Meeting', N'Meeting', NULL, CAST(0 AS bit), 30),
	(N'ActivityType', N'Remarket', N'Remarket', NULL, CAST(0 AS bit), 40),
	(N'ActivityType', N'Offer', N'Offer', NULL, CAST(0 AS bit), 50),
	(N'ActivityType', N'Bind', N'Bind', NULL, CAST(0 AS bit), 60),
	(N'ActivityType', N'Note', N'Note', NULL, CAST(0 AS bit), 70),
	(N'Priority', N'Low', N'Low', NULL, CAST(0 AS bit), 10),
	(N'Priority', N'Normal', N'Normal', NULL, CAST(1 AS bit), 20),
	(N'Priority', N'High', N'High', NULL, CAST(0 AS bit), 30),
	(N'Priority', N'Urgent', N'Urgent', NULL, CAST(0 AS bit), 40)
) v(OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM Renewal.WorkflowOption x WHERE x.TenantId=t.TenantId AND x.OptionGroupCode=v.OptionGroupCode AND x.OptionCode=v.OptionCode AND x.IsDeleted=0);

INSERT INTO Renewal.AutomationSetting
	(AutomationSettingId, TenantId, IsEnabled, InitiationLeadDays, DefaultPriorityCode, DefaultStageCode, CreateOpportunity, CreateSubmission, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), t.TenantId, 1, 120, N'Normal', N'Intake', 1, 1, SYSUTCDATETIME(), @SeedUserId, 0
FROM @Tenants t
WHERE NOT EXISTS (SELECT 1 FROM Renewal.AutomationSetting x WHERE x.TenantId=t.TenantId AND x.IsDeleted=0);
GO