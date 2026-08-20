SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS
(
	SELECT 1
	FROM DMS.IntakeSessionDocument child
	LEFT JOIN DMS.IntakeSession parent ON parent.TenantId = child.TenantId AND parent.IntakeSessionId = child.IntakeSessionId
	WHERE parent.IntakeSessionId IS NULL
)
	THROW 51000, N'Document Intake contains session-document rows outside their tenant session scope.', 1;

IF EXISTS
(
	SELECT 1
	FROM DMS.IntakeWorkItem child
	LEFT JOIN DMS.IntakeSession parent ON parent.TenantId = child.TenantId AND parent.IntakeSessionId = child.IntakeSessionId
	WHERE parent.IntakeSessionId IS NULL
)
	THROW 51000, N'Document Intake contains work items outside their tenant session scope.', 1;

IF EXISTS
(
	SELECT 1
	FROM DMS.IntakeWorkAttempt child
	LEFT JOIN DMS.IntakeWorkItem parent ON parent.TenantId = child.TenantId AND parent.IntakeWorkItemId = child.IntakeWorkItemId
	WHERE parent.IntakeWorkItemId IS NULL
)
	THROW 51000, N'Document Intake contains work attempts outside their tenant work-item scope.', 1;

IF EXISTS
(
	SELECT 1
	FROM DMS.AiExecution child
	LEFT JOIN DMS.IntakeSession sessionRow ON sessionRow.TenantId = child.TenantId AND sessionRow.IntakeSessionId = child.IntakeSessionId
	LEFT JOIN DMS.IntakeWorkItem workItem ON workItem.TenantId = child.TenantId AND workItem.IntakeSessionId = child.IntakeSessionId AND workItem.IntakeWorkItemId = child.IntakeWorkItemId
	WHERE sessionRow.IntakeSessionId IS NULL OR workItem.IntakeWorkItemId IS NULL
)
	THROW 51000, N'Document Intake contains AI executions outside their tenant processing scope.', 1;

IF EXISTS
(
	SELECT 1 FROM DMS.IntakeDraftField child
	LEFT JOIN DMS.IntakeSession parent ON parent.TenantId = child.TenantId AND parent.IntakeSessionId = child.IntakeSessionId
	WHERE parent.IntakeSessionId IS NULL
	UNION ALL
	SELECT 1 FROM DMS.IntakeIssue child
	LEFT JOIN DMS.IntakeSession parent ON parent.TenantId = child.TenantId AND parent.IntakeSessionId = child.IntakeSessionId
	WHERE parent.IntakeSessionId IS NULL
	UNION ALL
	SELECT 1 FROM DMS.IntakeReviewHistory child
	LEFT JOIN DMS.IntakeDraftField parent ON parent.TenantId = child.TenantId AND parent.IntakeSessionId = child.IntakeSessionId AND parent.IntakeDraftFieldId = child.IntakeDraftFieldId
	WHERE parent.IntakeDraftFieldId IS NULL
	UNION ALL
	SELECT 1 FROM DMS.IntakePromotion child
	LEFT JOIN DMS.IntakeSession parent ON parent.TenantId = child.TenantId AND parent.IntakeSessionId = child.IntakeSessionId
	WHERE parent.IntakeSessionId IS NULL
)
	THROW 51000, N'Document Intake contains workflow rows outside their tenant session scope.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeSession') AND name = N'UX_DMS_IntakeSession_Tenant_Id')
	CREATE UNIQUE INDEX UX_DMS_IntakeSession_Tenant_Id ON DMS.IntakeSession(TenantId, IntakeSessionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeWorkItem') AND name = N'UX_DMS_IntakeWorkItem_Tenant_Id')
	CREATE UNIQUE INDEX UX_DMS_IntakeWorkItem_Tenant_Id ON DMS.IntakeWorkItem(TenantId, IntakeWorkItemId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeWorkItem') AND name = N'UX_DMS_IntakeWorkItem_Tenant_Session_Id')
	CREATE UNIQUE INDEX UX_DMS_IntakeWorkItem_Tenant_Session_Id ON DMS.IntakeWorkItem(TenantId, IntakeSessionId, IntakeWorkItemId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeDraftField') AND name = N'UX_DMS_IntakeDraftField_Tenant_Id')
	CREATE UNIQUE INDEX UX_DMS_IntakeDraftField_Tenant_Id ON DMS.IntakeDraftField(TenantId, IntakeDraftFieldId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakeDraftField') AND name = N'UX_DMS_IntakeDraftField_Tenant_Session_Id')
	CREATE UNIQUE INDEX UX_DMS_IntakeDraftField_Tenant_Session_Id ON DMS.IntakeDraftField(TenantId, IntakeSessionId, IntakeDraftFieldId);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_IntakeSessionDocument_Session')
	ALTER TABLE DMS.IntakeSessionDocument DROP CONSTRAINT FK_DMS_IntakeSessionDocument_Session;
ALTER TABLE DMS.IntakeSessionDocument WITH CHECK ADD CONSTRAINT FK_DMS_IntakeSessionDocument_Session
	FOREIGN KEY (TenantId, IntakeSessionId) REFERENCES DMS.IntakeSession(TenantId, IntakeSessionId);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_IntakeWorkItem_Session')
	ALTER TABLE DMS.IntakeWorkItem DROP CONSTRAINT FK_DMS_IntakeWorkItem_Session;
ALTER TABLE DMS.IntakeWorkItem WITH CHECK ADD CONSTRAINT FK_DMS_IntakeWorkItem_Session
	FOREIGN KEY (TenantId, IntakeSessionId) REFERENCES DMS.IntakeSession(TenantId, IntakeSessionId);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_IntakeWorkAttempt_WorkItem')
	ALTER TABLE DMS.IntakeWorkAttempt DROP CONSTRAINT FK_DMS_IntakeWorkAttempt_WorkItem;
ALTER TABLE DMS.IntakeWorkAttempt WITH CHECK ADD CONSTRAINT FK_DMS_IntakeWorkAttempt_WorkItem
	FOREIGN KEY (TenantId, IntakeWorkItemId) REFERENCES DMS.IntakeWorkItem(TenantId, IntakeWorkItemId);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_AiExecution_Session')
	ALTER TABLE DMS.AiExecution DROP CONSTRAINT FK_DMS_AiExecution_Session;
ALTER TABLE DMS.AiExecution WITH CHECK ADD CONSTRAINT FK_DMS_AiExecution_Session
	FOREIGN KEY (TenantId, IntakeSessionId) REFERENCES DMS.IntakeSession(TenantId, IntakeSessionId);
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_AiExecution_WorkItem')
	ALTER TABLE DMS.AiExecution DROP CONSTRAINT FK_DMS_AiExecution_WorkItem;
ALTER TABLE DMS.AiExecution WITH CHECK ADD CONSTRAINT FK_DMS_AiExecution_WorkItem
	FOREIGN KEY (TenantId, IntakeSessionId, IntakeWorkItemId) REFERENCES DMS.IntakeWorkItem(TenantId, IntakeSessionId, IntakeWorkItemId);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_IntakeDraftField_Session')
	ALTER TABLE DMS.IntakeDraftField DROP CONSTRAINT FK_DMS_IntakeDraftField_Session;
ALTER TABLE DMS.IntakeDraftField WITH CHECK ADD CONSTRAINT FK_DMS_IntakeDraftField_Session
	FOREIGN KEY (TenantId, IntakeSessionId) REFERENCES DMS.IntakeSession(TenantId, IntakeSessionId);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_IntakeIssue_Session')
	ALTER TABLE DMS.IntakeIssue DROP CONSTRAINT FK_DMS_IntakeIssue_Session;
ALTER TABLE DMS.IntakeIssue WITH CHECK ADD CONSTRAINT FK_DMS_IntakeIssue_Session
	FOREIGN KEY (TenantId, IntakeSessionId) REFERENCES DMS.IntakeSession(TenantId, IntakeSessionId);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_IntakeReviewHistory_Session')
	ALTER TABLE DMS.IntakeReviewHistory DROP CONSTRAINT FK_DMS_IntakeReviewHistory_Session;
ALTER TABLE DMS.IntakeReviewHistory WITH CHECK ADD CONSTRAINT FK_DMS_IntakeReviewHistory_Session
	FOREIGN KEY (TenantId, IntakeSessionId) REFERENCES DMS.IntakeSession(TenantId, IntakeSessionId);
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_IntakeReviewHistory_Field')
	ALTER TABLE DMS.IntakeReviewHistory DROP CONSTRAINT FK_DMS_IntakeReviewHistory_Field;
ALTER TABLE DMS.IntakeReviewHistory WITH CHECK ADD CONSTRAINT FK_DMS_IntakeReviewHistory_Field
	FOREIGN KEY (TenantId, IntakeSessionId, IntakeDraftFieldId) REFERENCES DMS.IntakeDraftField(TenantId, IntakeSessionId, IntakeDraftFieldId);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DMS_IntakePromotion_Session')
	ALTER TABLE DMS.IntakePromotion DROP CONSTRAINT FK_DMS_IntakePromotion_Session;
ALTER TABLE DMS.IntakePromotion WITH CHECK ADD CONSTRAINT FK_DMS_IntakePromotion_Session
	FOREIGN KEY (TenantId, IntakeSessionId) REFERENCES DMS.IntakeSession(TenantId, IntakeSessionId);

IF EXISTS
(
	SELECT TenantId, IntakeSessionId
	FROM DMS.IntakePromotion
	GROUP BY TenantId, IntakeSessionId
	HAVING COUNT(*) > 1
)
	THROW 51000, N'Document Intake contains duplicate promotions for one tenant session.', 1;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakePromotion') AND name = N'UX_DMS_IntakePromotion_SessionModule')
	DROP INDEX UX_DMS_IntakePromotion_SessionModule ON DMS.IntakePromotion;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'DMS.IntakePromotion') AND name = N'UX_DMS_IntakePromotion_Session')
	CREATE UNIQUE INDEX UX_DMS_IntakePromotion_Session ON DMS.IntakePromotion(TenantId, IntakeSessionId);

DECLARE @ExtractionSchema NVARCHAR(MAX) = N'{"type":"object","required":["fields"],"additionalProperties":false,"properties":{"fields":{"type":"array","items":{"type":"object","required":["entityTypeCode","entityKey","path","value","valueTypeCode","confidence"],"additionalProperties":false,"properties":{"entityTypeCode":{"type":"string","minLength":1},"entityKey":{"type":"string","minLength":1},"path":{"type":"string","minLength":1},"value":{"type":["string","number","integer","boolean","null"]},"valueTypeCode":{"type":"string","minLength":1},"confidence":{"type":"number","minimum":0,"maximum":1},"sourcePage":{"type":["integer","null"],"minimum":1},"boundingBoxJson":{"type":["string","null"]}}}},"warnings":{"type":"array","items":{"type":"object","required":["code","severityCode","message"],"additionalProperties":false,"properties":{"code":{"type":"string"},"severityCode":{"type":"string"},"fieldPath":{"type":["string","null"]},"message":{"type":"string"}}}}}}';
DECLARE @Prompts TABLE (PromptCode NVARCHAR(100), CategoryCode NVARCHAR(50));
INSERT INTO @Prompts(PromptCode, CategoryCode) VALUES
(N'SUBMISSION.EXTRACTION',N'SUBMISSION_EXTRACTION'),(N'POLICY.EXTRACTION',N'POLICY_EXTRACTION'),
(N'LEAD.EXTRACTION',N'LEAD_EXTRACTION'),(N'RENEWAL.EXTRACTION',N'RENEWAL_EXTRACTION'),
(N'CLAIM.EXTRACTION',N'CLAIM_EXTRACTION'),(N'BIND_REQUEST.EXTRACTION',N'BIND_REQUEST_EXTRACTION'),
(N'ENDORSEMENT.EXTRACTION',N'ENDORSEMENT_EXTRACTION'),(N'ACCOUNT.EXTRACTION',N'ACCOUNT_EXTRACTION'),
(N'CERTIFICATE.EXTRACTION',N'CERTIFICATE_EXTRACTION'),(N'ACCOUNTING.EXTRACTION',N'ACCOUNTING_EXTRACTION'),
(N'CARRIER_INBOX.EXTRACTION',N'CARRIER_INBOX_EXTRACTION'),(N'PRODUCER_WORKSPACE.EXTRACTION',N'PRODUCER_WORKSPACE_EXTRACTION'),
(N'CRM.EXTRACTION',N'CRM_EXTRACTION'),(N'TASK.EXTRACTION',N'TASK_EXTRACTION'),
(N'COMPLIANCE.EXTRACTION',N'COMPLIANCE_EXTRACTION');

INSERT DMS.AiPromptDefinition
	(TenantId, PromptCode, PromptCategoryCode, VersionLabel, SystemPrompt, OutputSchemaJson, StatusCode, ApprovedDateUtc, EffectiveFromUtc)
SELECT NULL, source.PromptCode, source.CategoryCode, N'2.0',
	   CONCAT(N'Extract ', REPLACE(LOWER(source.PromptCode), N'.extraction', N''), N' facts from OCR evidence. Return only fields supported by the evidence using stable module-qualified field paths. Include entityTypeCode, entityKey, valueTypeCode, confidence, and source evidence for each field. Never create or update an AMS record.'),
	   @ExtractionSchema, N'APPROVED', SYSUTCDATETIME(), SYSUTCDATETIME()
FROM @Prompts source
WHERE NOT EXISTS
(
	SELECT 1 FROM DMS.AiPromptDefinition target
	WHERE target.TenantId IS NULL AND target.PromptCode = source.PromptCode AND target.VersionLabel = N'2.0'
);

COMMIT TRANSACTION;
