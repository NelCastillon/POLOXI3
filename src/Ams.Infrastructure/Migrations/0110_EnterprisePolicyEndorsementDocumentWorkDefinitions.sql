SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Policy.PolicyEndorsementDocumentWorkDefinition',N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementDocumentWorkDefinition
	(
		DocumentWorkDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementDocumentWorkDefinition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		DocumentTypeCode NVARCHAR(100) NOT NULL,
		TriggerCode NVARCHAR(40) NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_PolicyEndorsementDocumentWorkDefinition_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_PolicyEndorsementDocumentWorkDefinition_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyEndorsementDocumentWorkDefinition_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyEndorsementDocumentWorkDefinition_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_PolicyEndorsementDocumentWorkDefinition_Trigger CHECK(TriggerCode IN(N'Workflow',N'AccountingCompleted'))
	);
	CREATE UNIQUE INDEX UX_PolicyEndorsementDocumentWorkDefinition_Code ON Policy.PolicyEndorsementDocumentWorkDefinition(TenantId,DocumentTypeCode,TriggerCode) WHERE IsDeleted=0;
	CREATE INDEX IX_PolicyEndorsementDocumentWorkDefinition_Resolve ON Policy.PolicyEndorsementDocumentWorkDefinition(TenantId,TriggerCode,IsActive,IsDeleted,SortOrder);
END;

;WITH tenants AS
(
	SELECT DISTINCT TenantId FROM Policy.EndorsementType WHERE IsDeleted=0
), definitions AS
(
	SELECT * FROM (VALUES
		(N'IssuedEndorsement',N'Workflow',10),
		(N'UpdatedDeclaration',N'AccountingCompleted',10),
		(N'EndorsementSchedule',N'AccountingCompleted',20),
		(N'InvoiceOrCreditMemo',N'AccountingCompleted',30),
		(N'CoverageSummary',N'AccountingCompleted',40),
		(N'CarrierLetter',N'AccountingCompleted',50),
		(N'ClientLetter',N'AccountingCompleted',60)
	) value(DocumentTypeCode,TriggerCode,SortOrder)
)
INSERT Policy.PolicyEndorsementDocumentWorkDefinition
(DocumentWorkDefinitionId,TenantId,DocumentTypeCode,TriggerCode,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),tenant.TenantId,definition.DocumentTypeCode,definition.TriggerCode,1,definition.SortOrder,SYSUTCDATETIME(),0
FROM tenants tenant
CROSS JOIN definitions definition
WHERE NOT EXISTS
(
	SELECT 1 FROM Policy.PolicyEndorsementDocumentWorkDefinition existing
	WHERE existing.TenantId=tenant.TenantId AND existing.DocumentTypeCode=definition.DocumentTypeCode
	  AND existing.TriggerCode=definition.TriggerCode AND existing.IsDeleted=0
);
