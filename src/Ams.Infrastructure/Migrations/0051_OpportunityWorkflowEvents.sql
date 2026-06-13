-- ============================================================
-- MIGRATION 0051: OPPORTUNITY WORKFLOW EVENTS
-- Captures opportunity detail workflow history and related-entity sync.
-- ============================================================

IF OBJECT_ID('CRM.OpportunityWorkflowEvent', 'U') IS NULL
BEGIN
	CREATE TABLE CRM.OpportunityWorkflowEvent
	(
		WorkflowEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OpportunityWorkflowEvent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		OpportunityId UNIQUEIDENTIFIER NOT NULL,
		EventType NVARCHAR(50) NOT NULL,
		EventTitle NVARCHAR(200) NOT NULL,
		EventDetail NVARCHAR(2000) NULL,
		RelatedEntityName NVARCHAR(100) NULL,
		RelatedEntityId UNIQUEIDENTIFIER NULL,
		EventDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_OpportunityWorkflowEvent_EventDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_OpportunityWorkflowEvent_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_OpportunityWorkflowEvent_IsDeleted DEFAULT 0,
		CONSTRAINT FK_OpportunityWorkflowEvent_Opportunity FOREIGN KEY (OpportunityId) REFERENCES CRM.Opportunity(OpportunityId)
	);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OpportunityWorkflowEvent_Opportunity' AND object_id = OBJECT_ID('CRM.OpportunityWorkflowEvent'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_OpportunityWorkflowEvent_Opportunity
		ON CRM.OpportunityWorkflowEvent (OpportunityId, EventDateUtc DESC, IsDeleted);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OpportunityWorkflowEvent_Tenant' AND object_id = OBJECT_ID('CRM.OpportunityWorkflowEvent'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_OpportunityWorkflowEvent_Tenant
		ON CRM.OpportunityWorkflowEvent (TenantId, EventDateUtc DESC, IsDeleted);
END
GO

INSERT INTO CRM.OpportunityWorkflowEvent
(
	WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail,
	RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted
)
SELECT
	NEWID(), o.TenantId, o.OpportunityId, N'Created', N'Opportunity created',
	CONCAT(N'Opportunity ', o.OpportunityNumber, N' was created in CRM.'),
	N'Opportunity', o.OpportunityId,
	COALESCE(o.CreatedDateUtc, SYSUTCDATETIME()),
	COALESCE(o.CreatedDateUtc, SYSUTCDATETIME()),
	o.CreatedByUserId,
	0
FROM CRM.Opportunity o
WHERE ISNULL(o.IsDeleted, 0) = 0
  AND NOT EXISTS
  (
	  SELECT 1
	  FROM CRM.OpportunityWorkflowEvent e
	  WHERE e.OpportunityId = o.OpportunityId
		AND e.EventType = N'Created'
		AND ISNULL(e.IsDeleted, 0) = 0
  );
GO
