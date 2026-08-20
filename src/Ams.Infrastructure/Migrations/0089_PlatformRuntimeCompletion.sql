SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Platform.ServiceCatalog',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Platform.ServiceCatalog',N'MaturityCode') IS NULL ALTER TABLE Platform.ServiceCatalog ADD MaturityCode NVARCHAR(30) NOT NULL CONSTRAINT DF_Platform_ServiceCatalog_Maturity_0281 DEFAULT N'CATALOGED';
	IF COL_LENGTH(N'Platform.ServiceCatalog',N'ImplementationStatusCode') IS NULL ALTER TABLE Platform.ServiceCatalog ADD ImplementationStatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_Platform_ServiceCatalog_Implementation_0281 DEFAULT N'PARTIAL';
	IF COL_LENGTH(N'Platform.ServiceCatalog',N'ImplementationNotes') IS NULL ALTER TABLE Platform.ServiceCatalog ADD ImplementationNotes NVARCHAR(2000) NULL;
END;

IF OBJECT_ID(N'Platform.ModuleServiceDependency',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Platform.ModuleServiceDependency',N'AdoptionStatusCode') IS NULL ALTER TABLE Platform.ModuleServiceDependency ADD AdoptionStatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_Platform_ModuleServiceDependency_Adoption_0281 DEFAULT N'PLANNED';
	IF COL_LENGTH(N'Platform.ModuleServiceDependency',N'ConsumerReference') IS NULL ALTER TABLE Platform.ModuleServiceDependency ADD ConsumerReference NVARCHAR(500) NULL;
	IF COL_LENGTH(N'Platform.ModuleServiceDependency',N'LastVerifiedDateUtc') IS NULL ALTER TABLE Platform.ModuleServiceDependency ADD LastVerifiedDateUtc DATETIME2 NULL;
END;

GO

IF OBJECT_ID(N'Platform.MigrationGap',N'U') IS NULL
BEGIN
	CREATE TABLE Platform.MigrationGap
	(
		MigrationGapId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_MigrationGap PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		GapCode NVARCHAR(150) NOT NULL,
		PlatformServiceId UNIQUEIDENTIFIER NOT NULL,
		BusinessModuleId UNIQUEIDENTIFIER NULL,
		SourceReference NVARCHAR(500) NOT NULL,
		TargetContractReference NVARCHAR(500) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		PriorityCode NVARCHAR(30) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		RemediationJson NVARCHAR(MAX) NOT NULL,
		DetectedDateUtc DATETIME2 NOT NULL,
		CompletedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Platform_MigrationGap_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Platform_MigrationGap_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Platform_MigrationGap_Service FOREIGN KEY(PlatformServiceId) REFERENCES Platform.ServiceCatalog(PlatformServiceId),
		CONSTRAINT FK_Platform_MigrationGap_Module FOREIGN KEY(BusinessModuleId) REFERENCES Platform.BusinessModuleCatalog(BusinessModuleId),
		CONSTRAINT CK_Platform_MigrationGap_Priority CHECK(PriorityCode IN(N'CRITICAL',N'HIGH',N'MEDIUM',N'LOW')),
		CONSTRAINT CK_Platform_MigrationGap_Status CHECK(StatusCode IN(N'OPEN',N'IN_PROGRESS',N'COMPLETED',N'ACCEPTED')),
		CONSTRAINT CK_Platform_MigrationGap_Json CHECK(ISJSON(RemediationJson)=1)
	);
	CREATE UNIQUE INDEX UX_Platform_MigrationGap_Global ON Platform.MigrationGap(GapCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Platform_MigrationGap_Tenant ON Platform.MigrationGap(TenantId,GapCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
	CREATE INDEX IX_Platform_MigrationGap_Status ON Platform.MigrationGap(TenantId,StatusCode,PriorityCode,PlatformServiceId) INCLUDE(BusinessModuleId,SourceReference,TargetContractReference);
END;

UPDATE Platform.ServiceCatalog SET ContractReference=N'IUserService; IRoleService; IPermissionService',AdministrationRoute=N'/admin/users',MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Identity and authorization are separated across user, role, permission, authentication policy, and API authorization contracts.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'IDENTITY' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET ContractReference=N'IRoleService; IPermissionService; ASP.NET authorization policies',AdministrationRoute=N'/admin/roles',MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Authorization is implemented by role and permission services plus host authorization policies; no monolithic AuthorizationService exists.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'AUTHORIZATION' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Append-only enterprise audit contracts, API filters, persistence, and administration surfaces are active.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'AUDIT' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET AdministrationRoute=NULL,MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Workflow contracts and APIs are active; administration uses workflow designer/configuration routes rather than the former placeholder route.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'WORKFLOW' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET AdministrationRoute=NULL,MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'PARTIAL',ImplementationNotes=N'Notification records, templates, API, and retry behavior are centralized. Some SMTP delivery callers remain tracked migration gaps.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'NOTIFICATION' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Permission-scoped enterprise search is exposed through Intelligence search contracts and Azure Search indexing adapters.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'SEARCH' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'PARTIAL',ImplementationNotes=N'Document storage, intake, OCR, semantic normalization, search indexing, configuration, sharing, and retention are centralized. OCR provider configuration migration remains tracked.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'DOCUMENT' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Intelligence engines, governance, reasoning, discovery, recommendations, evaluations, findings, and business signals have DB-backed runtime paths.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'INTELLIGENCE' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET ContractReference=N'IRulesPlatformService',AdministrationRoute=N'/intelligence/governance',MaturityCode=N'EXECUTABLE',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Constrained DB-backed deterministic evaluation and RuleExecution evidence are exposed through the shared runtime.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'RULES' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET ContractReference=N'IValidationPlatformService',AdministrationRoute=N'/intelligence/governance',MaturityCode=N'EXECUTABLE',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Scoped DB-backed validations and ValidationExecution/ValidationResult evidence are exposed through the shared runtime.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'VALIDATION' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET AdministrationRoute=NULL,MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'PARTIAL',ImplementationNotes=N'Core configuration settings, tenant overrides, feature flags, and typed services are centralized. Hardcoded business option migrations remain tracked.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'CONFIGURATION' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET AdministrationRoute=NULL,MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Integration catalog, connector configuration, API, and carrier integration status are centralized.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'INTEGRATION' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Report catalog, execution, export, and scheduling surfaces are centralized.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'REPORTING' AND IsDeleted=0;
UPDATE Platform.ServiceCatalog SET MaturityCode=N'OPERATIONAL',ImplementationStatusCode=N'IMPLEMENTED',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceKindCode=N'INFRASTRUCTURE' AND IsDeleted=0;

UPDATE Platform.ModuleServiceDependency SET AdoptionStatusCode=N'PLANNED',ConsumerReference=NULL,LastVerifiedDateUtc=NULL,IsRequired=0,ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND IsDeleted=0;

DECLARE @Verified TABLE(ModuleCode NVARCHAR(100),ServiceCode NVARCHAR(100),ConsumerReference NVARCHAR(500));
INSERT @Verified VALUES
(N'CRM',N'IDENTITY',N'IUserService'),(N'CRM',N'AUTHORIZATION',N'CRM authorization policies'),(N'CRM',N'AUDIT',N'EntityAuditActionFilter'),(N'CRM',N'CONFIGURATION',N'CRM configuration services'),(N'CRM',N'RULES',N'AI.RecommendationRule adapter'),(N'CRM',N'INTELLIGENCE',N'IIntelligenceService recommendations and signals'),
(N'LEAD',N'DOCUMENT',N'IDocumentIntakeService'),(N'LEAD',N'NOTIFICATION',N'INotificationService'),(N'LEAD',N'VALIDATION',N'Validation platform target adapter'),
(N'OPPORTUNITY',N'INTELLIGENCE',N'IIntelligenceService recommendations'),(N'OPPORTUNITY',N'RULES',N'Rules.RuleDefinition recommendation adapter'),
(N'ACCOUNT',N'SEARCH',N'IIntelligenceService.SearchAsync'),(N'ACCOUNT',N'INTELLIGENCE',N'Customer intelligence and relationship engine'),(N'ACCOUNT',N'DOCUMENT',N'IDocumentService'),
(N'SUBMISSION',N'WORKFLOW',N'IWorkflowService and submission workflow services'),(N'SUBMISSION',N'DOCUMENT',N'IDocumentIntakeService'),(N'SUBMISSION',N'RULES',N'Rules platform recommendation adapter'),(N'SUBMISSION',N'VALIDATION',N'Bind validation adapter'),(N'SUBMISSION',N'INTELLIGENCE',N'Risk, compliance, recommendation, reasoning'),(N'SUBMISSION',N'INTEGRATION',N'IIntegrationService and carrier connectors'),(N'SUBMISSION',N'AUDIT',N'Enterprise audit filter and workflow audit'),
(N'QUOTE',N'WORKFLOW',N'Quote request workflow'),(N'QUOTE',N'INTEGRATION',N'Carrier/rater connectors'),(N'QUOTE',N'DOCUMENT',N'IDocumentService'),
(N'PROPOSAL',N'DOCUMENT',N'IDocumentService'),(N'PROPOSAL',N'WORKFLOW',N'Proposal workflow'),(N'PROPOSAL',N'NOTIFICATION',N'Proposal delivery migration gap'),
(N'BIND_REQUEST',N'VALIDATION',N'Bind validation adapter'),(N'BIND_REQUEST',N'RULES',N'Bind requirement/approval rules'),(N'BIND_REQUEST',N'WORKFLOW',N'Bind workflow'),(N'BIND_REQUEST',N'INTELLIGENCE',N'Compliance findings and reasoning'),
(N'POLICY',N'DOCUMENT',N'IDocumentService and DMS configuration'),(N'POLICY',N'WORKFLOW',N'Policy servicing workflow'),(N'POLICY',N'AUDIT',N'Enterprise audit'),
(N'ENDORSEMENT',N'WORKFLOW',N'Endorsement workflow workers'),(N'ENDORSEMENT',N'DOCUMENT',N'IDocumentService'),(N'ENDORSEMENT',N'INTEGRATION',N'Carrier workflow adapter'),
(N'RENEWAL',N'WORKFLOW',N'Renewal workflow'),(N'RENEWAL',N'INTELLIGENCE',N'Renewal intelligence signals'),(N'RENEWAL',N'DOCUMENT',N'IDocumentService'),
(N'CLAIMS',N'DOCUMENT',N'IDocumentService and claim document links'),(N'CLAIMS',N'INTELLIGENCE',N'Claims intelligence findings/signals'),(N'CLAIMS',N'SEARCH',N'Enterprise search projection'),(N'CLAIMS',N'VALIDATION',N'Validation platform target adapter'),
(N'CERTIFICATES',N'WORKFLOW',N'ICertificateWorkflowService'),(N'CERTIFICATES',N'DOCUMENT',N'IDocumentService'),(N'CERTIFICATES',N'NOTIFICATION',N'Notification platform'),
(N'ACCOUNTING',N'WORKFLOW',N'Accounting workflow services'),(N'ACCOUNTING',N'REPORTING',N'IReportService'),(N'ACCOUNTING',N'AUDIT',N'Enterprise audit'),(N'ACCOUNTING',N'VALIDATION',N'Validation platform target adapter'),
(N'DOCUMENTS',N'DOCUMENT',N'IDocumentService and IDocumentIntakeService'),(N'DOCUMENTS',N'INTELLIGENCE',N'Document intelligence and semantic mapping'),(N'DOCUMENTS',N'SEARCH',N'IDocumentSearchIndexer'),(N'DOCUMENTS',N'CONFIGURATION',N'DMS.DocumentKind and DMS.DocumentGroup');
UPDATE dependency SET AdoptionStatusCode=N'VERIFIED',ConsumerReference=verified.ConsumerReference,LastVerifiedDateUtc=SYSUTCDATETIME(),IsRequired=1,ModifiedDateUtc=SYSUTCDATETIME()
FROM Platform.ModuleServiceDependency dependency
JOIN Platform.BusinessModuleCatalog module ON module.BusinessModuleId=dependency.BusinessModuleId AND module.TenantId IS NULL AND module.IsDeleted=0
JOIN Platform.ServiceCatalog service ON service.PlatformServiceId=dependency.PlatformServiceId AND service.TenantId IS NULL AND service.IsDeleted=0
JOIN @Verified verified ON verified.ModuleCode=module.ModuleCode AND verified.ServiceCode=service.ServiceCode
WHERE dependency.TenantId IS NULL AND dependency.IsDeleted=0;

DECLARE @Gaps TABLE(GapCode NVARCHAR(150),ServiceCode NVARCHAR(100),ModuleCode NVARCHAR(100),SourceReference NVARCHAR(500),TargetContractReference NVARCHAR(500),Description NVARCHAR(2000),PriorityCode NVARCHAR(30),StatusCode NVARCHAR(30),RemediationJson NVARCHAR(MAX));
INSERT @Gaps VALUES
(N'DOCUMENT_AI_ROUTER_BYPASS',N'INTELLIGENCE',N'DOCUMENTS',N'AzureOpenAiDocumentInterpretationProvider',N'IAiProviderRouter',N'Document interpretation uses central database-backed provider routing, safety controls, and AI execution evidence.',N'CRITICAL',N'COMPLETED',N'{"strategy":"completed","consumer":"DocumentIntakeProcessor","evidence":"AI.Execution"}'),
(N'DOCUMENT_OCR_CONFIGURATION',N'DOCUMENT',N'DOCUMENTS',N'AzureDocumentIntelligenceOcrProvider',N'Configuration Platform provider route',N'Document OCR reads endpoint, model, API version, and credentials from host options instead of tenant-over-platform database configuration.',N'HIGH',N'OPEN',N'{"strategy":"add DB-backed OCR provider route while preserving IDocumentOcrProvider adapter"}'),
(N'PROPOSAL_SMTP_BYPASS',N'NOTIFICATION',N'PROPOSAL',N'ProposalDeliveryWorkerService.SendEmailAsync',N'INotificationService delivery dispatch',N'Proposal delivery sends SMTP directly instead of creating and dispatching a Notification Platform record.',N'HIGH',N'OPEN',N'{"strategy":"add notification delivery command and migrate proposal worker"}'),
(N'CONTACT_SMTP_BYPASS',N'NOTIFICATION',N'LEAD',N'SmtpContactIntakeNotificationService',N'INotificationService delivery dispatch',N'Contact intake sends SMTP directly instead of creating and dispatching a Notification Platform record.',N'MEDIUM',N'OPEN',N'{"strategy":"add notification delivery command and migrate contact intake"}'),
(N'BUSINESS_OPTIONS_CONFIGURATION',N'CONFIGURATION',NULL,N'Blazor business pages with static operational option arrays',N'IConfigurationService and normalized lookup tables',N'Operational statuses, reasons, channels, report schedules, and selectable values remain hardcoded on multiple business pages.',N'HIGH',N'OPEN',N'{"strategy":"inventory each option family, map to existing tables, create missing normalized settings, then migrate page by page"}');
MERGE Platform.MigrationGap target USING
(
	SELECT gap.GapCode,service.PlatformServiceId,module.BusinessModuleId,gap.SourceReference,gap.TargetContractReference,gap.Description,gap.PriorityCode,gap.StatusCode,gap.RemediationJson
	FROM @Gaps gap JOIN Platform.ServiceCatalog service ON service.TenantId IS NULL AND service.ServiceCode=gap.ServiceCode AND service.IsDeleted=0
	LEFT JOIN Platform.BusinessModuleCatalog module ON module.TenantId IS NULL AND module.ModuleCode=gap.ModuleCode AND module.IsDeleted=0
) source ON target.TenantId IS NULL AND target.GapCode=source.GapCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET PlatformServiceId=source.PlatformServiceId,BusinessModuleId=source.BusinessModuleId,SourceReference=source.SourceReference,TargetContractReference=source.TargetContractReference,Description=source.Description,PriorityCode=source.PriorityCode,StatusCode=source.StatusCode,RemediationJson=source.RemediationJson,CompletedDateUtc=CASE WHEN source.StatusCode=N'COMPLETED' THEN COALESCE(target.CompletedDateUtc,SYSUTCDATETIME()) ELSE NULL END,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TenantId,GapCode,PlatformServiceId,BusinessModuleId,SourceReference,TargetContractReference,Description,PriorityCode,StatusCode,RemediationJson,DetectedDateUtc,CompletedDateUtc,CreatedDateUtc,IsDeleted) VALUES(NULL,source.GapCode,source.PlatformServiceId,source.BusinessModuleId,source.SourceReference,source.TargetContractReference,source.Description,source.PriorityCode,source.StatusCode,source.RemediationJson,SYSUTCDATETIME(),CASE WHEN source.StatusCode=N'COMPLETED' THEN SYSUTCDATETIME() ELSE NULL END,SYSUTCDATETIME(),0);
