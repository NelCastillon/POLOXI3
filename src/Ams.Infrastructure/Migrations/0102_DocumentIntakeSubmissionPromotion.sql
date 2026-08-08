SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'DMS.IntakePromotionConfiguration', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakePromotionConfiguration
	(
		IntakePromotionConfigurationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakePromotionConfiguration PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ModuleCode NVARCHAR(50) NOT NULL,
		RequireReadyStatus BIT NOT NULL CONSTRAINT DF_DMS_IntakePromotionConfiguration_Ready DEFAULT 1,
		RequireCanonicalLob BIT NOT NULL CONSTRAINT DF_DMS_IntakePromotionConfiguration_Lob DEFAULT 1,
		LinkSourceDocuments BIT NOT NULL CONSTRAINT DF_DMS_IntakePromotionConfiguration_Documents DEFAULT 1,
		CreateFollowUpTask BIT NOT NULL CONSTRAINT DF_DMS_IntakePromotionConfiguration_Task DEFAULT 1,
		FollowUpTaskTitle NVARCHAR(200) NULL,
		FollowUpTaskDescription NVARCHAR(1000) NULL,
		FollowUpDueDays INT NULL,
		FollowUpTaskPriorityCode NVARCHAR(50) NOT NULL,
		OpportunityLinePriorityCode NVARCHAR(50) NOT NULL,
		OpportunityLineStatusCode NVARCHAR(50) NOT NULL,
		OpportunityCloseDays INT NOT NULL,
		OpportunityWinProbability DECIMAL(5,2) NOT NULL,
		SubmissionTermMonths INT NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_DMS_IntakePromotionConfiguration_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakePromotionConfiguration_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_DMS_IntakePromotionConfiguration_Deleted DEFAULT 0,
		CONSTRAINT CK_DMS_IntakePromotionConfiguration_DueDays CHECK (FollowUpDueDays IS NULL OR FollowUpDueDays BETWEEN 0 AND 3650),
		CONSTRAINT CK_DMS_IntakePromotionConfiguration_CloseDays CHECK (OpportunityCloseDays BETWEEN 0 AND 3650),
		CONSTRAINT CK_DMS_IntakePromotionConfiguration_WinProbability CHECK (OpportunityWinProbability BETWEEN 0 AND 100),
		CONSTRAINT CK_DMS_IntakePromotionConfiguration_TermMonths CHECK (SubmissionTermMonths BETWEEN 1 AND 120)
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakePromotionConfiguration') AND name = N'UX_DMS_IntakePromotionConfiguration_Tenant_Module')
	CREATE UNIQUE INDEX UX_DMS_IntakePromotionConfiguration_Tenant_Module ON DMS.IntakePromotionConfiguration(TenantId, ModuleCode) WHERE IsDeleted = 0;

IF OBJECT_ID(N'DMS.IntakePromotedDocument', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakePromotedDocument
	(
		IntakePromotedDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakePromotedDocument PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		IntakePromotionId UNIQUEIDENTIFIER NOT NULL,
		DocumentId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		OriginalEntityName NVARCHAR(100) NULL,
		OriginalEntityId UNIQUEIDENTIFIER NULL,
		DocumentRoleCode NVARCHAR(50) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakePromotedDocument_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_DMS_IntakePromotedDocument_Deleted DEFAULT 0,
		CONSTRAINT FK_DMS_IntakePromotedDocument_Session FOREIGN KEY (IntakeSessionId) REFERENCES DMS.IntakeSession(IntakeSessionId),
		CONSTRAINT FK_DMS_IntakePromotedDocument_Promotion FOREIGN KEY (IntakePromotionId) REFERENCES DMS.IntakePromotion(IntakePromotionId),
		CONSTRAINT FK_DMS_IntakePromotedDocument_Document FOREIGN KEY (DocumentId) REFERENCES DMS.Document(DocumentId),
		CONSTRAINT FK_DMS_IntakePromotedDocument_Submission FOREIGN KEY (SubmissionId) REFERENCES Submissions.Submission(SubmissionId)
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakePromotedDocument') AND name = N'UX_DMS_IntakePromotedDocument_Link')
	CREATE UNIQUE INDEX UX_DMS_IntakePromotedDocument_Link ON DMS.IntakePromotedDocument(TenantId, IntakeSessionId, DocumentId, SubmissionId) WHERE IsDeleted = 0;

IF COL_LENGTH(N'DMS.IntakePromotion', N'SubmissionIntakeId') IS NULL ALTER TABLE DMS.IntakePromotion ADD SubmissionIntakeId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.IntakePromotion', N'AccountId') IS NULL ALTER TABLE DMS.IntakePromotion ADD AccountId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.IntakePromotion', N'OpportunityId') IS NULL ALTER TABLE DMS.IntakePromotion ADD OpportunityId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.IntakePromotion', N'LobId') IS NULL ALTER TABLE DMS.IntakePromotion ADD LobId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.IntakePromotion', N'LastErrorMessage') IS NULL ALTER TABLE DMS.IntakePromotion ADD LastErrorMessage NVARCHAR(4000) NULL;
IF COL_LENGTH(N'DMS.IntakePromotion', N'ModifiedDateUtc') IS NULL ALTER TABLE DMS.IntakePromotion ADD ModifiedDateUtc DATETIME2 NULL;

INSERT INTO Submissions.SubmissionReferenceOption
	(SubmissionReferenceOptionId, TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
SELECT NEWID(), tenant.TenantId, optionValue.OptionGroup, optionValue.OptionCode, optionValue.OptionName, optionValue.Description, optionValue.IsDefault, 1, optionValue.SortOrder, SYSUTCDATETIME(), 0
FROM (SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0 UNION SELECT DISTINCT TenantId FROM DMS.IntakeSession) tenant
CROSS JOIN
(
	VALUES
		(N'OpportunityLinePriority', N'Standard', N'Standard', N'Standard opportunity coverage-line priority.', CAST(1 AS bit), 10),
		(N'OpportunityLinePriority', N'High', N'High', N'High-priority opportunity coverage line.', CAST(0 AS bit), 20),
		(N'OpportunityLineStatus', N'Draft', N'Draft', N'Coverage line is being prepared.', CAST(1 AS bit), 10),
		(N'OpportunityLineStatus', N'Active', N'Active', N'Coverage line is active and ready for submission preparation.', CAST(0 AS bit), 20)
) optionValue(OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
WHERE NOT EXISTS
(
	SELECT 1 FROM Submissions.SubmissionReferenceOption existing
	WHERE existing.TenantId = tenant.TenantId
	  AND existing.OptionGroup = optionValue.OptionGroup
	  AND existing.OptionCode = optionValue.OptionCode
	  AND existing.IsDeleted = 0
);

INSERT INTO DMS.IntakePromotionConfiguration
	(IntakePromotionConfigurationId, TenantId, ModuleCode, RequireReadyStatus, RequireCanonicalLob, LinkSourceDocuments, CreateFollowUpTask, FollowUpTaskTitle, FollowUpTaskDescription, FollowUpDueDays, FollowUpTaskPriorityCode, OpportunityLinePriorityCode, OpportunityLineStatusCode, OpportunityCloseDays, OpportunityWinProbability, SubmissionTermMonths, IsActive, CreatedDateUtc, IsDeleted)
SELECT NEWID(), tenant.TenantId, N'SUBMISSION', 1, 1, 1, 1,
	   N'Review newly created submission', N'Review extracted data, source documents, readiness requirements, and market eligibility.', 1,
		  taskPriority.OptionCode, priorityOption.OptionCode, statusOption.OptionCode, 30, 20, 12, 1, SYSUTCDATETIME(), 0
FROM (SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0 UNION SELECT DISTINCT TenantId FROM DMS.IntakeSession) tenant
CROSS APPLY
(
	SELECT TOP 1 OptionCode
	FROM Submissions.SubmissionReferenceOption
	WHERE TenantId = tenant.TenantId AND OptionGroup = N'OpportunityLinePriority' AND IsActive = 1 AND IsDeleted = 0
	ORDER BY IsDefault DESC, SortOrder, OptionCode
) priorityOption
CROSS APPLY
(
	SELECT TOP 1 OptionCode
	FROM Submissions.SubmissionReferenceOption
	WHERE TenantId = tenant.TenantId AND OptionGroup = N'OpportunityLineStatus' AND IsActive = 1 AND IsDeleted = 0
	ORDER BY IsDefault DESC, SortOrder, OptionCode
) statusOption
CROSS APPLY
(
	SELECT TOP 1 OptionCode
	FROM Submissions.SubmissionReferenceOption
	WHERE TenantId = tenant.TenantId AND OptionGroup = N'SubmissionPriority' AND IsActive = 1 AND IsDeleted = 0
	ORDER BY IsDefault DESC, SortOrder, OptionCode
) taskPriority
WHERE NOT EXISTS
(
	SELECT 1 FROM DMS.IntakePromotionConfiguration existing
	WHERE existing.TenantId = tenant.TenantId AND existing.ModuleCode = N'SUBMISSION' AND existing.IsDeleted = 0
);

COMMIT TRANSACTION;
