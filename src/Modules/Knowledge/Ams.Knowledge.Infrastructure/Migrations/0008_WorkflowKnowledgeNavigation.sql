SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'knowledge.WorkflowGuideStep', N'ModuleSequenceNumber') IS NULL
	ALTER TABLE knowledge.WorkflowGuideStep ADD ModuleSequenceNumber INT NULL;
IF COL_LENGTH(N'knowledge.WorkflowGuideStep', N'ModuleDisplayName') IS NULL
	ALTER TABLE knowledge.WorkflowGuideStep ADD ModuleDisplayName NVARCHAR(200) NULL;
IF COL_LENGTH(N'knowledge.WorkflowGuideStep', N'NavigationRoute') IS NULL
	ALTER TABLE knowledge.WorkflowGuideStep ADD NavigationRoute NVARCHAR(500) NULL;

EXEC sys.sp_executesql N'
UPDATE knowledge.WorkflowGuideStep
SET ModuleSequenceNumber = CASE ModuleCode
		WHEN ''CRM_LEAD'' THEN 10 WHEN ''CRM_OPPORTUNITY'' THEN 20 WHEN ''SUBMISSION'' THEN 30
		WHEN ''SUBMISSION_MARKET'' THEN 40 WHEN ''QUOTE_REQUEST'' THEN 50 WHEN ''QUOTE'' THEN 60
		WHEN ''PROPOSAL'' THEN 70 WHEN ''CLIENT_ACCEPTANCE'' THEN 80 WHEN ''BIND_REQUEST'' THEN 90
		WHEN ''POLICY'' THEN 100 WHEN ''ENDORSEMENT'' THEN 110 WHEN ''RENEWAL'' THEN 120
		ELSE SequenceNumber END,
	ModuleDisplayName = CASE ModuleCode
		WHEN ''CRM_LEAD'' THEN N''Leads'' WHEN ''CRM_OPPORTUNITY'' THEN N''Opportunities'' WHEN ''SUBMISSION'' THEN N''Submissions''
		WHEN ''SUBMISSION_MARKET'' THEN N''Submission Markets'' WHEN ''QUOTE_REQUEST'' THEN N''Quote Requests'' WHEN ''QUOTE'' THEN N''Quotes''
		WHEN ''PROPOSAL'' THEN N''Proposals'' WHEN ''CLIENT_ACCEPTANCE'' THEN N''Client Acceptance'' WHEN ''BIND_REQUEST'' THEN N''Bind Requests''
		WHEN ''POLICY'' THEN N''Policies'' WHEN ''ENDORSEMENT'' THEN N''Endorsements'' WHEN ''RENEWAL'' THEN N''Renewals''
		ELSE REPLACE(ModuleCode, ''_'', '' '') END,
	NavigationRoute = CASE ModuleCode
		WHEN ''CRM_LEAD'' THEN N''/crm/leads'' WHEN ''CRM_OPPORTUNITY'' THEN N''/crm/opportunities''
		WHEN ''SUBMISSION'' THEN N''/submissions'' WHEN ''SUBMISSION_MARKET'' THEN N''/submissions''
		WHEN ''QUOTE_REQUEST'' THEN N''/submissions/quotes'' WHEN ''QUOTE'' THEN N''/submissions/quotes''
		WHEN ''PROPOSAL'' THEN N''/submissions/quotes'' WHEN ''CLIENT_ACCEPTANCE'' THEN N''/submissions/quotes''
		WHEN ''BIND_REQUEST'' THEN N''/submissions/bind-requests'' WHEN ''POLICY'' THEN N''/policies''
		WHEN ''ENDORSEMENT'' THEN N''/policies/endorsements'' WHEN ''RENEWAL'' THEN N''/renewals''
		ELSE PageRoute END,
	ModifiedDateUtc = SYSUTCDATETIME()
WHERE IsDeleted = 0;

ALTER TABLE knowledge.WorkflowGuideStep ALTER COLUMN ModuleSequenceNumber INT NOT NULL;
ALTER TABLE knowledge.WorkflowGuideStep ALTER COLUMN ModuleDisplayName NVARCHAR(200) NOT NULL;
ALTER TABLE knowledge.WorkflowGuideStep ALTER COLUMN NavigationRoute NVARCHAR(500) NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''knowledge.WorkflowGuideStep'') AND name = N''IX_KnowledgeWorkflowGuideStep_ModuleOrder'')
	CREATE INDEX IX_KnowledgeWorkflowGuideStep_ModuleOrder
		ON knowledge.WorkflowGuideStep(WorkflowCode, ModuleSequenceNumber, SequenceNumber)
		INCLUDE(ModuleCode, ModuleDisplayName, NavigationRoute)
		WHERE IsDeleted = 0 AND IsActive = 1;';
