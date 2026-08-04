SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'DMS') EXEC(N'CREATE SCHEMA DMS');

IF OBJECT_ID(N'DMS.IntakeSession', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeSession
	(
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeSession PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SessionNumber NVARCHAR(50) NOT NULL,
		IdempotencyKey NVARCHAR(200) NOT NULL,
		ModuleCode NVARCHAR(50) NOT NULL,
		EntryPointCode NVARCHAR(50) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_DMS_IntakeSession_Status DEFAULT N'DRAFT',
		PriorityCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_IntakeSession_Priority DEFAULT N'NORMAL',
		TargetEntityId UNIQUEIDENTIFIER NULL,
		AssignedToUserId UNIQUEIDENTIFIER NULL,
		OverallConfidence DECIMAL(5,4) NULL,
		WarningCount INT NOT NULL CONSTRAINT DF_DMS_IntakeSession_Warnings DEFAULT 0,
		ErrorCount INT NOT NULL CONSTRAINT DF_DMS_IntakeSession_Errors DEFAULT 0,
		PromotedEntityId UNIQUEIDENTIFIER NULL,
		PromotedDateUtc DATETIME2 NULL,
		CancelledDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeSession_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_DMS_IntakeSession_Confidence CHECK (OverallConfidence IS NULL OR (OverallConfidence >= 0 AND OverallConfidence <= 1)),
		CONSTRAINT CK_DMS_IntakeSession_Status CHECK (StatusCode IN (N'DRAFT',N'QUEUED',N'PROCESSING',N'REVIEW_REQUIRED',N'READY',N'COMPLETED',N'FAILED',N'CANCELLED'))
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeSession') AND name = N'UX_DMS_IntakeSession_Tenant_Idempotency')
	CREATE UNIQUE INDEX UX_DMS_IntakeSession_Tenant_Idempotency ON DMS.IntakeSession(TenantId, IdempotencyKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeSession') AND name = N'UX_DMS_IntakeSession_Tenant_Number')
	CREATE UNIQUE INDEX UX_DMS_IntakeSession_Tenant_Number ON DMS.IntakeSession(TenantId, SessionNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeSession') AND name = N'IX_DMS_IntakeSession_Queue')
	CREATE INDEX IX_DMS_IntakeSession_Queue ON DMS.IntakeSession(TenantId, StatusCode, PriorityCode, CreatedDateUtc DESC);

IF OBJECT_ID(N'DMS.IntakeSessionDocument', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeSessionDocument
	(
		IntakeSessionDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeSessionDocument PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		DocumentId UNIQUEIDENTIFIER NOT NULL,
		DocumentRoleCode NVARCHAR(50) NOT NULL CONSTRAINT DF_DMS_IntakeSessionDocument_Role DEFAULT N'SOURCE',
		ContentHashSha256 CHAR(64) NULL,
		SequenceNumber INT NOT NULL CONSTRAINT DF_DMS_IntakeSessionDocument_Sequence DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeSessionDocument_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		CONSTRAINT FK_DMS_IntakeSessionDocument_Session FOREIGN KEY (IntakeSessionId) REFERENCES DMS.IntakeSession(IntakeSessionId),
		CONSTRAINT FK_DMS_IntakeSessionDocument_Document FOREIGN KEY (DocumentId) REFERENCES DMS.Document(DocumentId)
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeSessionDocument') AND name = N'UX_DMS_IntakeSessionDocument_Link')
	CREATE UNIQUE INDEX UX_DMS_IntakeSessionDocument_Link ON DMS.IntakeSessionDocument(TenantId, IntakeSessionId, DocumentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeSessionDocument') AND name = N'IX_DMS_IntakeSessionDocument_Hash')
	CREATE INDEX IX_DMS_IntakeSessionDocument_Hash ON DMS.IntakeSessionDocument(TenantId, ContentHashSha256) WHERE ContentHashSha256 IS NOT NULL;

IF OBJECT_ID(N'DMS.IntakeWorkItem', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeWorkItem
	(
		IntakeWorkItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeWorkItem PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		DocumentId UNIQUEIDENTIFIER NULL,
		WorkTypeCode NVARCHAR(50) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_DMS_IntakeWorkItem_Status DEFAULT N'PENDING',
		IdempotencyKey NVARCHAR(240) NOT NULL,
		SequenceNumber INT NOT NULL CONSTRAINT DF_DMS_IntakeWorkItem_Sequence DEFAULT 1,
		AttemptCount INT NOT NULL CONSTRAINT DF_DMS_IntakeWorkItem_Attempts DEFAULT 0,
		MaxAttempts INT NOT NULL CONSTRAINT DF_DMS_IntakeWorkItem_MaxAttempts DEFAULT 6,
		AvailableDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeWorkItem_Available DEFAULT SYSUTCDATETIME(),
		LeaseOwner NVARCHAR(200) NULL,
		LeaseExpiresDateUtc DATETIME2 NULL,
		StartedDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		LastErrorCode NVARCHAR(100) NULL,
		LastErrorMessage NVARCHAR(4000) NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeWorkItem_Created DEFAULT SYSUTCDATETIME(),
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_DMS_IntakeWorkItem_Session FOREIGN KEY (IntakeSessionId) REFERENCES DMS.IntakeSession(IntakeSessionId),
		CONSTRAINT CK_DMS_IntakeWorkItem_Status CHECK (StatusCode IN (N'PENDING',N'PROCESSING',N'COMPLETED',N'RETRY_SCHEDULED',N'FAILED',N'DEAD_LETTERED',N'CANCELLED')),
		CONSTRAINT CK_DMS_IntakeWorkItem_Attempts CHECK (AttemptCount >= 0 AND MaxAttempts BETWEEN 1 AND 20)
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeWorkItem') AND name = N'UX_DMS_IntakeWorkItem_Idempotency')
	CREATE UNIQUE INDEX UX_DMS_IntakeWorkItem_Idempotency ON DMS.IntakeWorkItem(TenantId, IdempotencyKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeWorkItem') AND name = N'IX_DMS_IntakeWorkItem_Lease')
	CREATE INDEX IX_DMS_IntakeWorkItem_Lease ON DMS.IntakeWorkItem(StatusCode, AvailableDateUtc, LeaseExpiresDateUtc, SequenceNumber) INCLUDE (TenantId, IntakeSessionId, WorkTypeCode, AttemptCount, MaxAttempts);

IF OBJECT_ID(N'DMS.IntakeWorkAttempt', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeWorkAttempt
	(
		IntakeWorkAttemptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeWorkAttempt PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeWorkItemId UNIQUEIDENTIFIER NOT NULL,
		AttemptNumber INT NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		LeaseOwner NVARCHAR(200) NOT NULL,
		StartedDateUtc DATETIME2 NOT NULL,
		CompletedDateUtc DATETIME2 NULL,
		DurationMilliseconds BIGINT NULL,
		ErrorCode NVARCHAR(100) NULL,
		ErrorMessage NVARCHAR(4000) NULL,
		CONSTRAINT FK_DMS_IntakeWorkAttempt_WorkItem FOREIGN KEY (IntakeWorkItemId) REFERENCES DMS.IntakeWorkItem(IntakeWorkItemId)
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeWorkAttempt') AND name = N'UX_DMS_IntakeWorkAttempt_Number')
	CREATE UNIQUE INDEX UX_DMS_IntakeWorkAttempt_Number ON DMS.IntakeWorkAttempt(IntakeWorkItemId, AttemptNumber);

IF OBJECT_ID(N'DMS.AiPromptDefinition', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.AiPromptDefinition
	(
		AiPromptDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_AiPromptDefinition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		PromptCode NVARCHAR(100) NOT NULL,
		PromptCategoryCode NVARCHAR(50) NOT NULL,
		VersionLabel NVARCHAR(30) NOT NULL,
		SystemPrompt NVARCHAR(MAX) NOT NULL,
		OutputSchemaJson NVARCHAR(MAX) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		ApprovedByUserId UNIQUEIDENTIFIER NULL,
		ApprovedDateUtc DATETIME2 NULL,
		EffectiveFromUtc DATETIME2 NOT NULL,
		EffectiveToUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_AiPromptDefinition_Created DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc DATETIME2 NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_DMS_AiPromptDefinition_Status CHECK (StatusCode IN (N'DRAFT',N'APPROVED',N'RETIRED'))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.AiPromptDefinition') AND name = N'UX_DMS_AiPromptDefinition_Version')
	CREATE UNIQUE INDEX UX_DMS_AiPromptDefinition_Version ON DMS.AiPromptDefinition(PromptCode, VersionLabel, TenantId);

IF OBJECT_ID(N'DMS.AiExecution', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.AiExecution
	(
		AiExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_AiExecution PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		IntakeWorkItemId UNIQUEIDENTIFIER NOT NULL,
		DocumentId UNIQUEIDENTIFIER NULL,
		ExecutionTypeCode NVARCHAR(50) NOT NULL,
		ProviderCode NVARCHAR(50) NOT NULL,
		ModelName NVARCHAR(150) NOT NULL,
		PromptCode NVARCHAR(100) NULL,
		PromptVersion NVARCHAR(30) NULL,
		InputReference NVARCHAR(1000) NOT NULL,
		OutputReference NVARCHAR(1000) NOT NULL,
		InputHashSha256 CHAR(64) NULL,
		OutputHashSha256 CHAR(64) NULL,
		Confidence DECIMAL(5,4) NULL,
		DurationMilliseconds BIGINT NOT NULL,
		InputTokenCount INT NULL,
		OutputTokenCount INT NULL,
		ContainsPii BIT NOT NULL CONSTRAINT DF_DMS_AiExecution_Pii DEFAULT 1,
		StatusCode NVARCHAR(30) NOT NULL,
		ErrorCode NVARCHAR(100) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_AiExecution_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT FK_DMS_AiExecution_Session FOREIGN KEY (IntakeSessionId) REFERENCES DMS.IntakeSession(IntakeSessionId),
		CONSTRAINT FK_DMS_AiExecution_WorkItem FOREIGN KEY (IntakeWorkItemId) REFERENCES DMS.IntakeWorkItem(IntakeWorkItemId),
		CONSTRAINT CK_DMS_AiExecution_Confidence CHECK (Confidence IS NULL OR (Confidence >= 0 AND Confidence <= 1))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.AiExecution') AND name = N'IX_DMS_AiExecution_Session')
	CREATE INDEX IX_DMS_AiExecution_Session ON DMS.AiExecution(TenantId, IntakeSessionId, CreatedDateUtc DESC);

IF OBJECT_ID(N'DMS.IntakeDraftField', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeDraftField
	(
		IntakeDraftFieldId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeDraftField PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		EntityKey NVARCHAR(100) NOT NULL,
		FieldPath NVARCHAR(500) NOT NULL,
		ExtractedValue NVARCHAR(MAX) NULL,
		NormalizedValue NVARCHAR(MAX) NULL,
		ReviewedValue NVARCHAR(MAX) NULL,
		ValueTypeCode NVARCHAR(30) NOT NULL,
		Confidence DECIMAL(5,4) NULL,
		SourceDocumentId UNIQUEIDENTIFIER NULL,
		SourcePageNumber INT NULL,
		SourceBoundingBoxJson NVARCHAR(2000) NULL,
		KnowledgeConceptId UNIQUEIDENTIFIER NULL,
		MappingStatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_IntakeDraftField_Mapping DEFAULT N'UNMAPPED',
		ReviewStatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_IntakeDraftField_Review DEFAULT N'PENDING',
		CorrectedByUserId UNIQUEIDENTIFIER NULL,
		CorrectedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeDraftField_Created DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc DATETIME2 NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_DMS_IntakeDraftField_Session FOREIGN KEY (IntakeSessionId) REFERENCES DMS.IntakeSession(IntakeSessionId),
		CONSTRAINT CK_DMS_IntakeDraftField_Confidence CHECK (Confidence IS NULL OR (Confidence >= 0 AND Confidence <= 1))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeDraftField') AND name = N'UX_DMS_IntakeDraftField_Path')
	CREATE UNIQUE INDEX UX_DMS_IntakeDraftField_Path ON DMS.IntakeDraftField(TenantId, IntakeSessionId, EntityTypeCode, EntityKey, FieldPath);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeDraftField') AND name = N'IX_DMS_IntakeDraftField_Review')
	CREATE INDEX IX_DMS_IntakeDraftField_Review ON DMS.IntakeDraftField(TenantId, IntakeSessionId, ReviewStatusCode, MappingStatusCode);

IF OBJECT_ID(N'DMS.IntakeIssue', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeIssue
	(
		IntakeIssueId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeIssue PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		IssueCode NVARCHAR(100) NOT NULL,
		IssueTypeCode NVARCHAR(50) NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		FieldPath NVARCHAR(500) NULL,
		Message NVARCHAR(2000) NOT NULL,
		ExistingValue NVARCHAR(MAX) NULL,
		ExtractedValue NVARCHAR(MAX) NULL,
		StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_DMS_IntakeIssue_Status DEFAULT N'OPEN',
		ResolvedByUserId UNIQUEIDENTIFIER NULL,
		ResolvedDateUtc DATETIME2 NULL,
		ResolutionNotes NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeIssue_Created DEFAULT SYSUTCDATETIME(),
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_DMS_IntakeIssue_Session FOREIGN KEY (IntakeSessionId) REFERENCES DMS.IntakeSession(IntakeSessionId),
		CONSTRAINT CK_DMS_IntakeIssue_Severity CHECK (SeverityCode IN (N'INFO',N'WARNING',N'ERROR'))
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeIssue') AND name = N'IX_DMS_IntakeIssue_Queue')
	CREATE INDEX IX_DMS_IntakeIssue_Queue ON DMS.IntakeIssue(TenantId, StatusCode, SeverityCode, CreatedDateUtc DESC);

IF OBJECT_ID(N'DMS.IntakeReviewHistory', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakeReviewHistory
	(
		IntakeReviewHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakeReviewHistory PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		IntakeDraftFieldId UNIQUEIDENTIFIER NULL,
		ActionCode NVARCHAR(50) NOT NULL,
		PreviousValue NVARCHAR(MAX) NULL,
		NewValue NVARCHAR(MAX) NULL,
		Reason NVARCHAR(2000) NOT NULL,
		ReviewedByUserId UNIQUEIDENTIFIER NOT NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakeReviewHistory_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT FK_DMS_IntakeReviewHistory_Session FOREIGN KEY (IntakeSessionId) REFERENCES DMS.IntakeSession(IntakeSessionId),
		CONSTRAINT FK_DMS_IntakeReviewHistory_Field FOREIGN KEY (IntakeDraftFieldId) REFERENCES DMS.IntakeDraftField(IntakeDraftFieldId)
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeReviewHistory') AND name = N'IX_DMS_IntakeReviewHistory_Session')
	CREATE INDEX IX_DMS_IntakeReviewHistory_Session ON DMS.IntakeReviewHistory(TenantId, IntakeSessionId, CreatedDateUtc DESC);

IF OBJECT_ID(N'DMS.IntakePromotion', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.IntakePromotion
	(
		IntakePromotionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DMS_IntakePromotion PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntakeSessionId UNIQUEIDENTIFIER NOT NULL,
		ModuleCode NVARCHAR(50) NOT NULL,
		IdempotencyKey NVARCHAR(240) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		TargetEntityId UNIQUEIDENTIFIER NULL,
		RequestJson NVARCHAR(MAX) NOT NULL,
		ResultJson NVARCHAR(MAX) NULL,
		PromotedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DMS_IntakePromotion_Created DEFAULT SYSUTCDATETIME(),
		CompletedDateUtc DATETIME2 NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_DMS_IntakePromotion_Session FOREIGN KEY (IntakeSessionId) REFERENCES DMS.IntakeSession(IntakeSessionId)
	);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakePromotion') AND name = N'UX_DMS_IntakePromotion_Idempotency')
	CREATE UNIQUE INDEX UX_DMS_IntakePromotion_Idempotency ON DMS.IntakePromotion(TenantId, IdempotencyKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakePromotion') AND name = N'UX_DMS_IntakePromotion_SessionModule')
	CREATE UNIQUE INDEX UX_DMS_IntakePromotion_SessionModule ON DMS.IntakePromotion(TenantId, IntakeSessionId, ModuleCode);

DECLARE @Prompts TABLE (PromptCode NVARCHAR(100), CategoryCode NVARCHAR(50), SystemPrompt NVARCHAR(MAX), OutputSchemaJson NVARCHAR(MAX));
INSERT INTO @Prompts VALUES
(N'DOCUMENT.CLASSIFICATION',N'DOCUMENT_CLASSIFICATION',N'Classify the insurance document. Return only the required structured JSON. Do not infer unsupported facts and do not propose database changes.',N'{"type":"object","required":["documentTypeCode","confidence"],"properties":{"documentTypeCode":{"type":"string"},"confidence":{"type":"number","minimum":0,"maximum":1},"warnings":{"type":"array","items":{"type":"string"}}}}'),
(N'SUBMISSION.EXTRACTION',N'SUBMISSION_EXTRACTION',N'Extract submission intake facts from OCR evidence. Return only structured JSON with source page and confidence for every field. Never create or update an AMS record.',N'{"type":"object","required":["fields"],"properties":{"fields":{"type":"array","items":{"type":"object","required":["path","value","confidence"],"properties":{"path":{"type":"string"},"value":{},"confidence":{"type":"number","minimum":0,"maximum":1},"sourcePage":{"type":["integer","null"]}}}},"warnings":{"type":"array","items":{"type":"string"}}}}'),
(N'POLICY.EXTRACTION',N'POLICY_EXTRACTION',N'Extract policy facts and differences from OCR evidence. Return only structured JSON. Never update policy records.',N'{"type":"object","required":["fields","differences"],"properties":{"fields":{"type":"array"},"differences":{"type":"array"}}}'),
(N'CLAIM.EXTRACTION',N'CLAIM_EXTRACTION',N'Extract claim evidence facts from OCR evidence. Return only structured JSON. Never create or update a claim.',N'{"type":"object","required":["fields"],"properties":{"fields":{"type":"array"},"warnings":{"type":"array"}}}'),
(N'ENDORSEMENT.INTENT',N'ENDORSEMENT_INTENT',N'Identify requested endorsement changes. Return only structured JSON and never change a policy.',N'{"type":"object","required":["changes"],"properties":{"changes":{"type":"array"},"warnings":{"type":"array"}}}'),
(N'RENEWAL.SUMMARY',N'RENEWAL_SUMMARY',N'Summarize renewal evidence and differences. Return only structured JSON and never update renewal records.',N'{"type":"object","required":["summary","differences"],"properties":{"summary":{"type":"string"},"differences":{"type":"array"}}}'),
(N'QUOTE.COMPARISON',N'QUOTE_COMPARISON',N'Compare quote evidence using deterministic extracted values. Return only structured JSON and never select or bind coverage.',N'{"type":"object","required":["differences"],"properties":{"differences":{"type":"array"},"warnings":{"type":"array"}}}');

MERGE DMS.AiPromptDefinition AS target
USING @Prompts AS source
ON target.TenantId IS NULL AND target.PromptCode = source.PromptCode AND target.VersionLabel = N'1.0'
WHEN MATCHED THEN UPDATE SET PromptCategoryCode=source.CategoryCode,SystemPrompt=source.SystemPrompt,OutputSchemaJson=source.OutputSchemaJson,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TenantId,PromptCode,PromptCategoryCode,VersionLabel,SystemPrompt,OutputSchemaJson,StatusCode,EffectiveFromUtc) VALUES(NULL,source.PromptCode,source.CategoryCode,N'1.0',source.SystemPrompt,source.OutputSchemaJson,N'APPROVED',SYSUTCDATETIME());

IF OBJECT_ID(N'Master.PermissionAction', N'U') IS NOT NULL AND OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
BEGIN
	DECLARE @Permission TABLE (PermissionCode NVARCHAR(150), PermissionName NVARCHAR(200), ResourceCode NVARCHAR(100), ActionCode NVARCHAR(50), Description NVARCHAR(500));
	INSERT INTO @Permission VALUES
	(N'DMS.INTAKE.READ',N'View document intake sessions',N'DMS.Intake',N'READ',N'View AI document intake sessions, drafts, issues, and history.'),
	(N'DMS.INTAKE.UPLOAD',N'Upload documents into intake',N'DMS.Intake',N'UPLOAD',N'Create intake sessions and attach evidence documents.'),
	(N'DMS.INTAKE.REVIEW',N'Review document intake drafts',N'DMS.IntakeReview',N'WRITE',N'Review and correct extracted draft values.'),
	(N'DMS.INTAKE.REPROCESS',N'Reprocess document intake sessions',N'DMS.IntakeProcessing',N'MANAGE',N'Requeue failed or completed processing stages.'),
	(N'DMS.INTAKE.PROMOTE',N'Promote reviewed intake drafts',N'DMS.IntakePromotion',N'MANAGE',N'Promote approved drafts through owning application services.'),
	(N'DMS.INTAKE.ADMIN',N'Administer document intake',N'DMS.IntakeAdministration',N'MANAGE',N'Administer prompt versions and intake configuration.');

	UPDATE existing
	SET PermissionName=source.PermissionName,ResourceCode=source.ResourceCode,ActionCode=source.ActionCode,ModuleCode=N'DMS',Description=source.Description,
		PermissionActionId=COALESCE(actionRow.PermissionActionId,existing.PermissionActionId),IsBuiltIn=1,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	FROM IAM.Permission existing
	JOIN @Permission source ON source.PermissionCode=existing.PermissionCode
	OUTER APPLY (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode)=source.ActionCode OR UPPER(ActionName)=source.ActionCode ORDER BY PermissionActionId) actionRow;

	INSERT INTO IAM.Permission(PermissionId,TenantId,PermissionCode,PermissionActionId,PermissionName,ResourceCode,ActionCode,ModuleCode,Description,IsBuiltIn,IsActive,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),seedTenant.TenantId,source.PermissionCode,COALESCE(actionRow.PermissionActionId,readAction.PermissionActionId,1),source.PermissionName,source.ResourceCode,source.ActionCode,N'DMS',source.Description,1,1,SYSUTCDATETIME(),0
	FROM @Permission source
	CROSS APPLY
	(
		SELECT TOP 1 tenant.TenantId
		FROM Core.Tenant tenant
		ORDER BY CASE WHEN tenant.TenantId=N'00000000-0000-0000-0000-000000000001' THEN 0 ELSE 1 END,tenant.TenantId
	) seedTenant
	OUTER APPLY (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode)=source.ActionCode OR UPPER(ActionName)=source.ActionCode ORDER BY PermissionActionId) actionRow
	OUTER APPLY (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode)=N'READ' OR UPPER(ActionName)=N'READ' ORDER BY PermissionActionId) readAction
	WHERE NOT EXISTS (SELECT 1 FROM IAM.Permission existing WHERE existing.PermissionCode=source.PermissionCode);
END;

COMMIT TRANSACTION;
