-- ============================================================
-- MIGRATION 0050: DOCUMENTS WORKFLOW SCHEMA - COMPLETE
-- Creates comprehensive document workflow management tables
-- ============================================================

-- ============================================================
-- DMS SCHEMA VALIDATION
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DMS')
BEGIN
	EXEC('CREATE SCHEMA DMS');
END
GO

-- ============================================================
-- DOCUMENT WORKFLOW TEMPLATE TABLE
-- Defines reusable workflow templates for document processes
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DMS') AND name = 'DocumentWorkflowTemplate')
BEGIN
	CREATE TABLE DMS.DocumentWorkflowTemplate (
		WorkflowTemplateId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId            UNIQUEIDENTIFIER NOT NULL,

		-- Template Information
		TemplateName        NVARCHAR(255)    NOT NULL,
		TemplateCode        NVARCHAR(100)    NOT NULL,
		Description         NVARCHAR(MAX)    NULL,
		WorkflowType        NVARCHAR(100)    NOT NULL, -- 'Approval', 'Review', 'Classification', 'Retention'

		-- Configuration
		IsSequential        BIT              NOT NULL DEFAULT 1, -- Sequential vs Parallel steps
		RequiresAllApprovals BIT             NOT NULL DEFAULT 1,
		AutoArchiveOnComplete BIT            NOT NULL DEFAULT 0,
		NotifyOnStart       BIT              NOT NULL DEFAULT 1,
		NotifyOnComplete    BIT              NOT NULL DEFAULT 1,

		-- Trigger Configuration
		TriggerOnUpload     BIT              NOT NULL DEFAULT 0,
		TriggerOnCategory   NVARCHAR(100)    NULL, -- DocumentCategory code
		TriggerOnDocType    NVARCHAR(100)    NULL, -- DocumentType code

		-- Status
		IsActive            BIT              NOT NULL DEFAULT 1,
		SortOrder           INT              NOT NULL DEFAULT 0,

		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0
	);

	CREATE NONCLUSTERED INDEX IX_DocumentWorkflowTemplate_TenantId ON DMS.DocumentWorkflowTemplate(TenantId, IsDeleted, IsActive);
	CREATE UNIQUE NONCLUSTERED INDEX IX_DocumentWorkflowTemplate_Code ON DMS.DocumentWorkflowTemplate(TenantId, TemplateCode) WHERE IsDeleted = 0;
END
GO

-- ============================================================
-- DOCUMENT WORKFLOW STEP TEMPLATE TABLE
-- Defines steps within a workflow template
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DMS') AND name = 'DocumentWorkflowStepTemplate')
BEGIN
	CREATE TABLE DMS.DocumentWorkflowStepTemplate (
		StepTemplateId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		WorkflowTemplateId  UNIQUEIDENTIFIER NOT NULL,

		-- Step Information
		StepName            NVARCHAR(255)    NOT NULL,
		StepType            NVARCHAR(100)    NOT NULL, -- 'Approval', 'Review', 'Classify', 'Notify', 'Archive'
		StepOrder           INT              NOT NULL,
		Description         NVARCHAR(MAX)    NULL,

		-- Assignment
		AssignedToRoleCode  NVARCHAR(100)    NULL, -- Role-based assignment
		AssignedToUserId    UNIQUEIDENTIFIER NULL, -- Specific user assignment
		AssignToBranchAdmin BIT              NOT NULL DEFAULT 0,
		AssignToDocOwner    BIT              NOT NULL DEFAULT 0,

		-- Configuration
		IsRequired          BIT              NOT NULL DEFAULT 1,
		DueDays             INT              NULL, -- Days to complete step
		EscalateDays        INT              NULL, -- Days before escalation
		EscalateToRoleCode  NVARCHAR(100)    NULL,

		-- Conditional Logic
		RequiresPreviousApproval BIT         NOT NULL DEFAULT 0,
		SkipIfCondition     NVARCHAR(500)    NULL, -- JSON condition logic

		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT FK_DocumentWorkflowStepTemplate_WorkflowTemplate FOREIGN KEY (WorkflowTemplateId)
			REFERENCES DMS.DocumentWorkflowTemplate(WorkflowTemplateId)
	);

	CREATE NONCLUSTERED INDEX IX_DocumentWorkflowStepTemplate_TenantId ON DMS.DocumentWorkflowStepTemplate(TenantId, IsDeleted);
	CREATE NONCLUSTERED INDEX IX_DocumentWorkflowStepTemplate_WorkflowTemplateId ON DMS.DocumentWorkflowStepTemplate(WorkflowTemplateId, StepOrder);
END
GO

-- ============================================================
-- DOCUMENT WORKFLOW INSTANCE TABLE
-- Tracks active workflow instances for documents
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DMS') AND name = 'DocumentWorkflowInstance')
BEGIN
	CREATE TABLE DMS.DocumentWorkflowInstance (
		WorkflowInstanceId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		DocumentId          UNIQUEIDENTIFIER NOT NULL,
		WorkflowTemplateId  UNIQUEIDENTIFIER NOT NULL,

		-- Instance Information
		InstanceName        NVARCHAR(255)    NOT NULL,
		WorkflowStatus      NVARCHAR(100)    NOT NULL DEFAULT 'Pending', -- 'Pending', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Escalated'
		CurrentStepOrder    INT              NULL,

		-- Timeline
		StartedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CompletedDateUtc    DATETIME2        NULL,
		DueDateUtc          DATETIME2        NULL,

		-- Context
		InitiatedByUserId   UNIQUEIDENTIFIER NOT NULL,
		InitiatedByName     NVARCHAR(200)    NULL,
		Comments            NVARCHAR(MAX)    NULL,
		Priority            NVARCHAR(50)     NOT NULL DEFAULT 'Normal', -- 'Low', 'Normal', 'High', 'Critical'

		-- Outcome
		FinalOutcome        NVARCHAR(100)    NULL, -- 'Approved', 'Rejected', 'Cancelled'
		FinalComments       NVARCHAR(MAX)    NULL,
		CompletedByUserId   UNIQUEIDENTIFIER NULL,
		CompletedByName     NVARCHAR(200)    NULL,

		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT FK_DocumentWorkflowInstance_WorkflowTemplate FOREIGN KEY (WorkflowTemplateId)
			REFERENCES DMS.DocumentWorkflowTemplate(WorkflowTemplateId)
	);

	CREATE NONCLUSTERED INDEX IX_DocumentWorkflowInstance_TenantId ON DMS.DocumentWorkflowInstance(TenantId, IsDeleted);
	CREATE NONCLUSTERED INDEX IX_DocumentWorkflowInstance_DocumentId ON DMS.DocumentWorkflowInstance(DocumentId, WorkflowStatus);
	CREATE NONCLUSTERED INDEX IX_DocumentWorkflowInstance_Status ON DMS.DocumentWorkflowInstance(WorkflowStatus, DueDateUtc);
	CREATE NONCLUSTERED INDEX IX_DocumentWorkflowInstance_InitiatedBy ON DMS.DocumentWorkflowInstance(InitiatedByUserId, WorkflowStatus);
END
GO

-- ============================================================
-- DOCUMENT APPROVAL TABLE
-- Tracks approval requests within workflows
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DMS') AND name = 'DocumentApproval')
BEGIN
	CREATE TABLE DMS.DocumentApproval (
		ApprovalId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		WorkflowInstanceId  UNIQUEIDENTIFIER NOT NULL,
		DocumentId          UNIQUEIDENTIFIER NOT NULL,
		StepTemplateId      UNIQUEIDENTIFIER NULL,

		-- Approval Information
		ApprovalName        NVARCHAR(255)    NOT NULL,
		ApprovalType        NVARCHAR(100)    NOT NULL DEFAULT 'Standard', -- 'Standard', 'Compliance', 'Legal', 'Financial'
		StepOrder           INT              NOT NULL,

		-- Assignment
		AssignedToUserId    UNIQUEIDENTIFIER NOT NULL,
		AssignedToName      NVARCHAR(200)    NULL,
		AssignedToRoleCode  NVARCHAR(100)    NULL,
		AssignedDateUtc     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),

		-- Status
		ApprovalStatus      NVARCHAR(100)    NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected', 'Deferred', 'Escalated'

		-- Response
		ResponseDateUtc     DATETIME2        NULL,
		ResponseByUserId    UNIQUEIDENTIFIER NULL,
		ResponseByName      NVARCHAR(200)    NULL,
		Comments            NVARCHAR(MAX)    NULL,

		-- Timeline
		DueDateUtc          DATETIME2        NULL,
		EscalatedDateUtc    DATETIME2        NULL,
		EscalatedToUserId   UNIQUEIDENTIFIER NULL,

		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT FK_DocumentApproval_WorkflowInstance FOREIGN KEY (WorkflowInstanceId)
			REFERENCES DMS.DocumentWorkflowInstance(WorkflowInstanceId)
	);

	CREATE NONCLUSTERED INDEX IX_DocumentApproval_TenantId ON DMS.DocumentApproval(TenantId, IsDeleted);
	CREATE NONCLUSTERED INDEX IX_DocumentApproval_WorkflowInstanceId ON DMS.DocumentApproval(WorkflowInstanceId, StepOrder);
	CREATE NONCLUSTERED INDEX IX_DocumentApproval_AssignedTo ON DMS.DocumentApproval(AssignedToUserId, ApprovalStatus);
	CREATE NONCLUSTERED INDEX IX_DocumentApproval_Status ON DMS.DocumentApproval(ApprovalStatus, DueDateUtc);
	CREATE NONCLUSTERED INDEX IX_DocumentApproval_DocumentId ON DMS.DocumentApproval(DocumentId, ApprovalStatus);
END
GO

-- ============================================================
-- DOCUMENT REVIEW TABLE
-- Tracks review requests within workflows
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DMS') AND name = 'DocumentReview')
BEGIN
	CREATE TABLE DMS.DocumentReview (
		ReviewId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		WorkflowInstanceId  UNIQUEIDENTIFIER NULL,
		DocumentId          UNIQUEIDENTIFIER NOT NULL,

		-- Review Information
		ReviewName          NVARCHAR(255)    NOT NULL,
		ReviewType          NVARCHAR(100)    NOT NULL DEFAULT 'Standard', -- 'Standard', 'Technical', 'Legal', 'Compliance'
		ReviewPurpose       NVARCHAR(MAX)    NULL,

		-- Assignment
		AssignedToUserId    UNIQUEIDENTIFIER NOT NULL,
		AssignedToName      NVARCHAR(200)    NULL,
		AssignedDateUtc     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),

		-- Status
		ReviewStatus        NVARCHAR(100)    NOT NULL DEFAULT 'Pending', -- 'Pending', 'InReview', 'Completed', 'Returned', 'Cancelled'

		-- Response
		CompletedDateUtc    DATETIME2        NULL,
		CompletedByUserId   UNIQUEIDENTIFIER NULL,
		CompletedByName     NVARCHAR(200)    NULL,
		ReviewNotes         NVARCHAR(MAX)    NULL,
		Rating              INT              NULL, -- 1-5 rating

		-- Findings
		IssuesFound         INT              NOT NULL DEFAULT 0,
		RecommendChanges    BIT              NOT NULL DEFAULT 0,
		ChangesDescription  NVARCHAR(MAX)    NULL,

		-- Timeline
		DueDateUtc          DATETIME2        NULL,

		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0,

		CONSTRAINT FK_DocumentReview_WorkflowInstance FOREIGN KEY (WorkflowInstanceId)
			REFERENCES DMS.DocumentWorkflowInstance(WorkflowInstanceId)
	);

	CREATE NONCLUSTERED INDEX IX_DocumentReview_TenantId ON DMS.DocumentReview(TenantId, IsDeleted);
	CREATE NONCLUSTERED INDEX IX_DocumentReview_WorkflowInstanceId ON DMS.DocumentReview(WorkflowInstanceId);
	CREATE NONCLUSTERED INDEX IX_DocumentReview_AssignedTo ON DMS.DocumentReview(AssignedToUserId, ReviewStatus);
	CREATE NONCLUSTERED INDEX IX_DocumentReview_Status ON DMS.DocumentReview(ReviewStatus, DueDateUtc);
	CREATE NONCLUSTERED INDEX IX_DocumentReview_DocumentId ON DMS.DocumentReview(DocumentId, ReviewStatus);
END
GO

-- ============================================================
-- DOCUMENT RETENTION POLICY TABLE
-- Defines retention rules for document categories
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DMS') AND name = 'DocumentRetentionPolicy')
BEGIN
	CREATE TABLE DMS.DocumentRetentionPolicy (
		RetentionPolicyId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId            UNIQUEIDENTIFIER NOT NULL,

		-- Policy Information
		PolicyName          NVARCHAR(255)    NOT NULL,
		PolicyCode          NVARCHAR(100)    NOT NULL,
		Description         NVARCHAR(MAX)    NULL,

		-- Scope
		ApplicableCategory  NVARCHAR(100)    NULL, -- DocumentCategory code
		ApplicableDocType   NVARCHAR(100)    NULL, -- DocumentType code
		ApplicableEntityType NVARCHAR(100)   NULL, -- 'Account', 'Policy', 'Claim'

		-- Retention Rules
		RetentionPeriodYears INT             NOT NULL,
		RetentionStartTrigger NVARCHAR(100)  NOT NULL DEFAULT 'Creation', -- 'Creation', 'PolicyExpiry', 'ClaimClosure', 'LastModified'

		-- Actions
		ActionOnExpiry      NVARCHAR(100)    NOT NULL DEFAULT 'Archive', -- 'Archive', 'Delete', 'Review', 'Flag'
		RequireApprovalToDelete BIT          NOT NULL DEFAULT 1,
		NotifyBeforeDays    INT              NULL, -- Days before retention expires to notify
		NotifyRoleCode      NVARCHAR(100)    NULL,

		-- Compliance
		RegulatoryBasis     NVARCHAR(MAX)    NULL, -- Legal/regulatory justification
		ComplianceNotes     NVARCHAR(MAX)    NULL,

		-- Status
		IsActive            BIT              NOT NULL DEFAULT 1,
		EffectiveDate       DATE             NOT NULL,
		ExpiryDate          DATE             NULL,

		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0
	);

	CREATE NONCLUSTERED INDEX IX_DocumentRetentionPolicy_TenantId ON DMS.DocumentRetentionPolicy(TenantId, IsDeleted, IsActive);
	CREATE UNIQUE NONCLUSTERED INDEX IX_DocumentRetentionPolicy_Code ON DMS.DocumentRetentionPolicy(TenantId, PolicyCode) WHERE IsDeleted = 0;
	CREATE NONCLUSTERED INDEX IX_DocumentRetentionPolicy_Category ON DMS.DocumentRetentionPolicy(ApplicableCategory, IsActive);
END
GO

-- ============================================================
-- DOCUMENT AUDIT TRAIL TABLE
-- Comprehensive audit log for all document workflow actions
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DMS') AND name = 'DocumentAuditTrail')
BEGIN
	CREATE TABLE DMS.DocumentAuditTrail (
		AuditId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		DocumentId          UNIQUEIDENTIFIER NOT NULL,
		WorkflowInstanceId  UNIQUEIDENTIFIER NULL,

		-- Event Information
		EventType           NVARCHAR(100)    NOT NULL, -- 'Upload', 'Download', 'View', 'Edit', 'Delete', 'Approve', 'Reject', 'Review', 'Share', 'Archive'
		EventCategory       NVARCHAR(100)    NOT NULL DEFAULT 'Document', -- 'Document', 'Workflow', 'Approval', 'Review', 'Retention'
		EventDescription    NVARCHAR(MAX)    NULL,

		-- Context
		PerformedByUserId   UNIQUEIDENTIFIER NULL,
		PerformedByName     NVARCHAR(200)    NULL,
		PerformedByRoleCode NVARCHAR(100)    NULL,
		EventDateUtc        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),

		-- Details
		OldValue            NVARCHAR(MAX)    NULL, -- JSON of previous state
		NewValue            NVARCHAR(MAX)    NULL, -- JSON of new state
		ChangesSummary      NVARCHAR(MAX)    NULL,

		-- System Context
		IpAddress           NVARCHAR(50)     NULL,
		UserAgent           NVARCHAR(500)    NULL,
		SessionId           NVARCHAR(100)    NULL,

		-- Compliance
		RetentionYears      INT              NOT NULL DEFAULT 7,
		IsArchived          BIT              NOT NULL DEFAULT 0,

		-- No IsDeleted - audit records are immutable
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE()
	);

	CREATE NONCLUSTERED INDEX IX_DocumentAuditTrail_TenantId ON DMS.DocumentAuditTrail(TenantId, EventDateUtc DESC);
	CREATE NONCLUSTERED INDEX IX_DocumentAuditTrail_DocumentId ON DMS.DocumentAuditTrail(DocumentId, EventDateUtc DESC);
	CREATE NONCLUSTERED INDEX IX_DocumentAuditTrail_WorkflowInstanceId ON DMS.DocumentAuditTrail(WorkflowInstanceId, EventDateUtc DESC);
	CREATE NONCLUSTERED INDEX IX_DocumentAuditTrail_PerformedBy ON DMS.DocumentAuditTrail(PerformedByUserId, EventDateUtc DESC);
	CREATE NONCLUSTERED INDEX IX_DocumentAuditTrail_EventType ON DMS.DocumentAuditTrail(EventType, EventDateUtc DESC);
END
GO

-- ============================================================
-- DOCUMENT CLASSIFICATION QUEUE TABLE
-- Tracks documents pending classification/OCR review
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DMS') AND name = 'DocumentClassificationQueue')
BEGIN
	CREATE TABLE DMS.DocumentClassificationQueue (
		ClassificationQueueId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		DocumentId          UNIQUEIDENTIFIER NOT NULL,

		-- Classification Status
		QueueStatus         NVARCHAR(100)    NOT NULL DEFAULT 'Pending', -- 'Pending', 'InReview', 'Classified', 'Failed', 'Skipped'
		ClassificationMethod NVARCHAR(100)   NOT NULL DEFAULT 'OCR', -- 'OCR', 'Manual', 'RuleBased', 'AI'

		-- OCR Results
		OcrConfidence       DECIMAL(5,2)     NULL, -- 0-100%
		SuggestedCategory   NVARCHAR(100)    NULL,
		SuggestedDocType    NVARCHAR(100)    NULL,
		ExtractedText       NVARCHAR(MAX)    NULL,
		ExtractedMetadata   NVARCHAR(MAX)    NULL, -- JSON metadata

		-- Assignment
		AssignedToUserId    UNIQUEIDENTIFIER NULL,
		AssignedToName      NVARCHAR(200)    NULL,
		AssignedDateUtc     DATETIME2        NULL,

		-- Resolution
		ClassifiedByUserId  UNIQUEIDENTIFIER NULL,
		ClassifiedByName    NVARCHAR(200)    NULL,
		ClassifiedDateUtc   DATETIME2        NULL,
		FinalCategory       NVARCHAR(100)    NULL,
		FinalDocType        NVARCHAR(100)    NULL,
		ClassificationNotes NVARCHAR(MAX)    NULL,

		-- Priority
		Priority            NVARCHAR(50)     NOT NULL DEFAULT 'Normal', -- 'Low', 'Normal', 'High'
		DueDateUtc          DATETIME2        NULL,

		-- Audit
		CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL DEFAULT 0
	);

	CREATE NONCLUSTERED INDEX IX_DocumentClassificationQueue_TenantId ON DMS.DocumentClassificationQueue(TenantId, IsDeleted);
	CREATE NONCLUSTERED INDEX IX_DocumentClassificationQueue_DocumentId ON DMS.DocumentClassificationQueue(DocumentId);
	CREATE NONCLUSTERED INDEX IX_DocumentClassificationQueue_Status ON DMS.DocumentClassificationQueue(QueueStatus, Priority, DueDateUtc);
	CREATE NONCLUSTERED INDEX IX_DocumentClassificationQueue_AssignedTo ON DMS.DocumentClassificationQueue(AssignedToUserId, QueueStatus);
END
GO

PRINT 'Migration 0050: Documents Workflow Schema - COMPLETE';
GO
