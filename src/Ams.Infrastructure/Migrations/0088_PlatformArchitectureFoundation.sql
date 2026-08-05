SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name=N'Platform') EXEC(N'CREATE SCHEMA Platform');
IF NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name=N'Rules') EXEC(N'CREATE SCHEMA Rules');
IF NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name=N'Validation') EXEC(N'CREATE SCHEMA Validation');

IF OBJECT_ID(N'Platform.ServiceCatalog',N'U') IS NULL
BEGIN
	CREATE TABLE Platform.ServiceCatalog
	(
		PlatformServiceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_ServiceCatalog PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		ServiceCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		ServiceKindCode NVARCHAR(50) NOT NULL,
		OwningSchemaCode NVARCHAR(50) NULL,
		ContractReference NVARCHAR(500) NULL,
		AdministrationRoute NVARCHAR(500) NULL,
		IsInfrastructureOnly BIT NOT NULL CONSTRAINT DF_Platform_ServiceCatalog_Infrastructure DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Platform_ServiceCatalog_Active DEFAULT 1,
		SortOrder INT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Platform_ServiceCatalog_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Platform_ServiceCatalog_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Platform_ServiceCatalog_Kind CHECK(ServiceKindCode IN(N'PLATFORM',N'INFRASTRUCTURE'))
	);
	CREATE UNIQUE INDEX UX_Platform_ServiceCatalog_Global ON Platform.ServiceCatalog(ServiceCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Platform_ServiceCatalog_Tenant ON Platform.ServiceCatalog(TenantId,ServiceCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;

IF OBJECT_ID(N'AI.SafetyControl',N'U') IS NOT NULL
BEGIN
	DECLARE @RouterSafety TABLE(ControlCode NVARCHAR(120),DisplayName NVARCHAR(200),Description NVARCHAR(2000),ControlTypeCode NVARCHAR(50),EnforcementStageCode NVARCHAR(50),ConfigurationJson NVARCHAR(MAX),ViolationActionCode NVARCHAR(50),RequiresHumanReview BIT,SortOrder INT);
	INSERT @RouterSafety VALUES
	(N'MAXIMUM_INPUT_LENGTH',N'Maximum AI input length',N'Block provider requests whose combined system and user prompts exceed the Configuration Platform limit.',N'INPUT_VALIDATION',N'PRE_EXECUTION',N'{"enabled":true,"settingKey":"Intelligence.Safety.MaximumInputCharacters"}',N'BLOCK',0,9),
	(N'MAXIMUM_OUTPUT_LENGTH',N'Maximum AI output length',N'Reject provider responses whose content exceeds the Configuration Platform limit.',N'OUTPUT_VALIDATION',N'POST_EXECUTION',N'{"enabled":true,"settingKey":"Intelligence.Safety.MaximumOutputCharacters"}',N'REJECT',1,10);
	MERGE AI.SafetyControl target USING @RouterSafety source ON target.TenantId IS NULL AND target.ControlCode=source.ControlCode AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,ControlTypeCode=source.ControlTypeCode,EnforcementStageCode=source.EnforcementStageCode,ConfigurationJson=source.ConfigurationJson,ViolationActionCode=source.ViolationActionCode,RequiresHumanReview=source.RequiresHumanReview,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(TenantId,ControlCode,DisplayName,Description,ControlTypeCode,EnforcementStageCode,ConfigurationJson,ViolationActionCode,RequiresHumanReview,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(NULL,source.ControlCode,source.DisplayName,source.Description,source.ControlTypeCode,source.EnforcementStageCode,source.ConfigurationJson,source.ViolationActionCode,source.RequiresHumanReview,source.SortOrder,1,SYSUTCDATETIME(),0);
END;

IF OBJECT_ID(N'Platform.BusinessModuleCatalog',N'U') IS NULL
BEGIN
	CREATE TABLE Platform.BusinessModuleCatalog
	(
		BusinessModuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_BusinessModuleCatalog PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		ModuleCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		OwningSchemaCode NVARCHAR(50) NULL,
		NavigationRoute NVARCHAR(500) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Platform_BusinessModuleCatalog_Active DEFAULT 1,
		SortOrder INT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Platform_BusinessModuleCatalog_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Platform_BusinessModuleCatalog_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_Platform_BusinessModuleCatalog_Global ON Platform.BusinessModuleCatalog(ModuleCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Platform_BusinessModuleCatalog_Tenant ON Platform.BusinessModuleCatalog(TenantId,ModuleCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;

IF OBJECT_ID(N'Platform.ModuleServiceDependency',N'U') IS NULL
BEGIN
	CREATE TABLE Platform.ModuleServiceDependency
	(
		ModuleServiceDependencyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_ModuleServiceDependency PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		BusinessModuleId UNIQUEIDENTIFIER NOT NULL,
		PlatformServiceId UNIQUEIDENTIFIER NOT NULL,
		UsageCode NVARCHAR(100) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		IsRequired BIT NOT NULL CONSTRAINT DF_Platform_ModuleServiceDependency_Required DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_Platform_ModuleServiceDependency_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Platform_ModuleServiceDependency_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Platform_ModuleServiceDependency_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Platform_ModuleServiceDependency_Module FOREIGN KEY(BusinessModuleId) REFERENCES Platform.BusinessModuleCatalog(BusinessModuleId),
		CONSTRAINT FK_Platform_ModuleServiceDependency_Service FOREIGN KEY(PlatformServiceId) REFERENCES Platform.ServiceCatalog(PlatformServiceId)
	);
	CREATE UNIQUE INDEX UX_Platform_ModuleServiceDependency_Global ON Platform.ModuleServiceDependency(BusinessModuleId,PlatformServiceId,UsageCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Platform_ModuleServiceDependency_Tenant ON Platform.ModuleServiceDependency(TenantId,BusinessModuleId,PlatformServiceId,UsageCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;

IF OBJECT_ID(N'Rules.RuleDefinition',N'U') IS NULL
BEGIN
	CREATE TABLE Rules.RuleDefinition
	(
		RuleDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Rules_RuleDefinition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		RuleCode NVARCHAR(120) NOT NULL,
		DisplayName NVARCHAR(240) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		RuleCategoryCode NVARCHAR(50) NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		SourceModuleCode NVARCHAR(100) NULL,
		ConditionJson NVARCHAR(MAX) NOT NULL,
		OutcomeJson NVARCHAR(MAX) NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		StopsProcessing BIT NOT NULL CONSTRAINT DF_Rules_RuleDefinition_Stops DEFAULT 0,
		EffectiveFromUtc DATETIME2 NOT NULL,
		EffectiveToUtc DATETIME2 NULL,
		VersionNumber INT NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Rules_RuleDefinition_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Rules_RuleDefinition_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Rules_RuleDefinition_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Rules_RuleDefinition_ConditionJson CHECK(ISJSON(ConditionJson)=1),
		CONSTRAINT CK_Rules_RuleDefinition_OutcomeJson CHECK(ISJSON(OutcomeJson)=1),
		CONSTRAINT CK_Rules_RuleDefinition_Dates CHECK(EffectiveToUtc IS NULL OR EffectiveToUtc>EffectiveFromUtc),
		CONSTRAINT CK_Rules_RuleDefinition_Version CHECK(VersionNumber>0)
	);
	CREATE UNIQUE INDEX UX_Rules_RuleDefinition_Global ON Rules.RuleDefinition(RuleCode,VersionNumber) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Rules_RuleDefinition_Tenant ON Rules.RuleDefinition(TenantId,RuleCode,VersionNumber) WHERE TenantId IS NOT NULL AND IsDeleted=0;
	CREATE INDEX IX_Rules_RuleDefinition_Entity ON Rules.RuleDefinition(TenantId,EntityTypeCode,IsActive,EffectiveFromUtc,EffectiveToUtc) INCLUDE(RuleCode,RuleCategoryCode,SeverityCode);
END;

IF OBJECT_ID(N'Rules.RuleExecution',N'U') IS NULL
BEGIN
	CREATE TABLE Rules.RuleExecution
	(
		RuleExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Rules_RuleExecution PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		RuleDefinitionId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		CorrelationId NVARCHAR(100) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		IsMatch BIT NULL,
		InputSnapshotJson NVARCHAR(MAX) NOT NULL,
		ResultJson NVARCHAR(MAX) NULL,
		ErrorMessage NVARCHAR(4000) NULL,
		EvaluatedDateUtc DATETIME2 NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Rules_RuleExecution_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Rules_RuleExecution_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Rules_RuleExecution_Definition FOREIGN KEY(RuleDefinitionId) REFERENCES Rules.RuleDefinition(RuleDefinitionId),
		CONSTRAINT CK_Rules_RuleExecution_Status CHECK(StatusCode IN(N'COMPLETED',N'FAILED')),
		CONSTRAINT CK_Rules_RuleExecution_InputJson CHECK(ISJSON(InputSnapshotJson)=1),
		CONSTRAINT CK_Rules_RuleExecution_ResultJson CHECK(ResultJson IS NULL OR ISJSON(ResultJson)=1)
	);
	CREATE UNIQUE INDEX UX_Rules_RuleExecution_Correlation ON Rules.RuleExecution(TenantId,CorrelationId,RuleDefinitionId,EntityId) WHERE IsDeleted=0;
	CREATE INDEX IX_Rules_RuleExecution_Entity ON Rules.RuleExecution(TenantId,EntityTypeCode,EntityId,EvaluatedDateUtc DESC) INCLUDE(RuleDefinitionId,StatusCode,IsMatch);
END;

IF OBJECT_ID(N'Validation.ValidationDefinition',N'U') IS NULL
BEGIN
	CREATE TABLE Validation.ValidationDefinition
	(
		ValidationDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Validation_ValidationDefinition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		ValidationCode NVARCHAR(120) NOT NULL,
		DisplayName NVARCHAR(240) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		ValidatorTypeCode NVARCHAR(50) NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		SourceModuleCode NVARCHAR(100) NULL,
		JurisdictionCode NVARCHAR(20) NULL,
		CarrierId UNIQUEIDENTIFIER NULL,
		LineOfBusinessCode NVARCHAR(100) NULL,
		ConditionJson NVARCHAR(MAX) NOT NULL,
		FailureJson NVARCHAR(MAX) NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		IsBlocking BIT NOT NULL CONSTRAINT DF_Validation_ValidationDefinition_Blocking DEFAULT 0,
		CanBeWaived BIT NOT NULL CONSTRAINT DF_Validation_ValidationDefinition_Waivable DEFAULT 0,
		WaiverPermissionCode NVARCHAR(150) NULL,
		EffectiveFromUtc DATETIME2 NOT NULL,
		EffectiveToUtc DATETIME2 NULL,
		VersionNumber INT NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Validation_ValidationDefinition_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Validation_ValidationDefinition_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Validation_ValidationDefinition_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Validation_ValidationDefinition_Type CHECK(ValidatorTypeCode IN(N'REQUIRED_FIELD',N'CROSS_FIELD',N'BUSINESS',N'CARRIER',N'STATE',N'DOCUMENT',N'AI_ADAPTER')),
		CONSTRAINT CK_Validation_ValidationDefinition_ConditionJson CHECK(ISJSON(ConditionJson)=1),
		CONSTRAINT CK_Validation_ValidationDefinition_FailureJson CHECK(ISJSON(FailureJson)=1),
		CONSTRAINT CK_Validation_ValidationDefinition_Dates CHECK(EffectiveToUtc IS NULL OR EffectiveToUtc>EffectiveFromUtc),
		CONSTRAINT CK_Validation_ValidationDefinition_Version CHECK(VersionNumber>0)
	);
	CREATE UNIQUE INDEX UX_Validation_ValidationDefinition_Global ON Validation.ValidationDefinition(ValidationCode,VersionNumber) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Validation_ValidationDefinition_Tenant ON Validation.ValidationDefinition(TenantId,ValidationCode,VersionNumber) WHERE TenantId IS NOT NULL AND IsDeleted=0;
	CREATE INDEX IX_Validation_ValidationDefinition_Scope ON Validation.ValidationDefinition(TenantId,EntityTypeCode,ValidatorTypeCode,JurisdictionCode,CarrierId,LineOfBusinessCode,IsActive) INCLUDE(ValidationCode,SeverityCode,IsBlocking);
END;

IF OBJECT_ID(N'Validation.ValidationExecution',N'U') IS NULL
BEGIN
	CREATE TABLE Validation.ValidationExecution
	(
		ValidationExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Validation_ValidationExecution PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		CorrelationId NVARCHAR(100) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		RequestedByUserId UNIQUEIDENTIFIER NULL,
		StartedDateUtc DATETIME2 NOT NULL,
		CompletedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Validation_ValidationExecution_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Validation_ValidationExecution_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Validation_ValidationExecution_Status CHECK(StatusCode IN(N'PROCESSING',N'COMPLETED',N'FAILED'))
	);
	CREATE UNIQUE INDEX UX_Validation_ValidationExecution_Correlation ON Validation.ValidationExecution(TenantId,CorrelationId) WHERE IsDeleted=0;
END;

IF OBJECT_ID(N'Validation.ValidationResult',N'U') IS NULL
BEGIN
	CREATE TABLE Validation.ValidationResult
	(
		ValidationResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Validation_ValidationResult PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ValidationExecutionId UNIQUEIDENTIFIER NOT NULL,
		ValidationDefinitionId UNIQUEIDENTIFIER NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		IsBlocking BIT NOT NULL,
		Message NVARCHAR(2000) NOT NULL,
		EvidenceJson NVARCHAR(MAX) NOT NULL,
		EvaluatedDateUtc DATETIME2 NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Validation_ValidationResult_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Validation_ValidationResult_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Validation_ValidationResult_Execution FOREIGN KEY(ValidationExecutionId) REFERENCES Validation.ValidationExecution(ValidationExecutionId),
		CONSTRAINT FK_Validation_ValidationResult_Definition FOREIGN KEY(ValidationDefinitionId) REFERENCES Validation.ValidationDefinition(ValidationDefinitionId),
		CONSTRAINT CK_Validation_ValidationResult_Status CHECK(StatusCode IN(N'PASSED',N'FAILED',N'NOT_APPLICABLE',N'ERROR')),
		CONSTRAINT CK_Validation_ValidationResult_EvidenceJson CHECK(ISJSON(EvidenceJson)=1)
	);
	CREATE UNIQUE INDEX UX_Validation_ValidationResult_Definition ON Validation.ValidationResult(ValidationExecutionId,ValidationDefinitionId) WHERE IsDeleted=0;
END;

DECLARE @Services TABLE(ServiceCode NVARCHAR(100),DisplayName NVARCHAR(200),Description NVARCHAR(2000),ServiceKindCode NVARCHAR(50),OwningSchemaCode NVARCHAR(50),ContractReference NVARCHAR(500),AdministrationRoute NVARCHAR(500),IsInfrastructureOnly BIT,SortOrder INT);
INSERT @Services VALUES
(N'IDENTITY',N'Identity Platform',N'Central user, tenant, authentication, and identity lifecycle services.',N'PLATFORM',N'IAM',N'Ams.Application.UserService',N'/admin/users',0,10),
(N'AUTHORIZATION',N'Authorization Platform',N'Central role, permission, policy, and tenant access enforcement.',N'PLATFORM',N'IAM',N'Ams.Application.AuthorizationService',N'/admin/roles',0,20),
(N'AUDIT',N'Audit Platform',N'Append-only enterprise and security audit evidence with correlation and actor context.',N'PLATFORM',N'Audit',N'IEnterpriseAuditService',N'/admin/audit',0,30),
(N'WORKFLOW',N'Workflow Platform',N'Reusable definitions, instances, approvals, SLA, escalation, and rework orchestration.',N'PLATFORM',N'Workflow',N'IWorkflowService',N'/admin/workflow',0,40),
(N'NOTIFICATION',N'Notification Platform',N'Central notification policies, templates, delivery, preferences, and history.',N'PLATFORM',N'Notification',N'INotificationService',N'/admin/notifications',0,50),
(N'SEARCH',N'Search Platform',N'Permission-aware enterprise search across business modules and knowledge sources.',N'PLATFORM',N'AI',N'IIntelligenceService.SearchAsync',N'/intelligence/search',0,60),
(N'DOCUMENT',N'Document Platform',N'Canonical document storage, configuration, intake, OCR, retention, sharing, and audit.',N'PLATFORM',N'DMS',N'IDocumentService',N'/tenant/document-config',0,70),
(N'INTELLIGENCE',N'Intelligence Platform',N'Knowledge, recommendations, risk, compliance, discovery, AI operations, and explainability.',N'PLATFORM',N'AI',N'IIntelligenceService',N'/intelligence/platform',0,80),
(N'RULES',N'Rules Platform',N'Versioned deterministic rule definitions and explainable execution evidence shared by all modules.',N'PLATFORM',N'Rules',N'IRulesPlatformService',N'/admin/platform/rules',0,90),
(N'VALIDATION',N'Validation Platform',N'Required-field, cross-field, business, carrier, state, document, and AI-adapter validations.',N'PLATFORM',N'Validation',N'IValidationPlatformService',N'/admin/platform/validation',0,100),
(N'CONFIGURATION',N'Configuration Platform',N'Platform, tenant, regional, feature, carrier, workflow, notification, rules, and AI settings.',N'PLATFORM',N'Core',N'IConfigurationService',N'/admin/configuration',0,110),
(N'INTEGRATION',N'Integration Platform',N'Central connector configuration, dispatch, retry, correlation, and external-system evidence.',N'PLATFORM',N'Integration',N'IIntegrationService',N'/admin/integrations',0,120),
(N'REPORTING',N'Reporting Platform',N'Central operational and management reporting over authoritative module data.',N'PLATFORM',N'Reporting',N'IReportService',N'/reports',0,130),
(N'SQL',N'SQL Infrastructure',N'Authoritative relational persistence and transactional processing.',N'INFRASTRUCTURE',NULL,NULL,NULL,1,200),
(N'BLOB',N'Blob Infrastructure',N'Durable binary object storage used through the Document Platform.',N'INFRASTRUCTURE',NULL,NULL,NULL,1,210),
(N'AZURE_AI',N'Azure AI Infrastructure',N'Configured AI provider infrastructure accessed through governed platform adapters.',N'INFRASTRUCTURE',NULL,N'IAiProviderRouter',NULL,1,220),
(N'AZURE_SEARCH',N'Azure Search Infrastructure',N'External search indexing infrastructure accessed through the Search Platform.',N'INFRASTRUCTURE',NULL,N'AzureDocumentSearchIndexer',NULL,1,230),
(N'WORKERS',N'Worker Infrastructure',N'BackgroundService hosts for durable platform and workflow processing.',N'INFRASTRUCTURE',NULL,N'Ams.Worker',NULL,1,240);
MERGE Platform.ServiceCatalog target USING @Services source ON target.TenantId IS NULL AND target.ServiceCode=source.ServiceCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,ServiceKindCode=source.ServiceKindCode,OwningSchemaCode=source.OwningSchemaCode,ContractReference=source.ContractReference,AdministrationRoute=source.AdministrationRoute,IsInfrastructureOnly=source.IsInfrastructureOnly,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TenantId,ServiceCode,DisplayName,Description,ServiceKindCode,OwningSchemaCode,ContractReference,AdministrationRoute,IsInfrastructureOnly,IsActive,SortOrder,CreatedDateUtc,IsDeleted) VALUES(NULL,source.ServiceCode,source.DisplayName,source.Description,source.ServiceKindCode,source.OwningSchemaCode,source.ContractReference,source.AdministrationRoute,source.IsInfrastructureOnly,1,source.SortOrder,SYSUTCDATETIME(),0);

DECLARE @Modules TABLE(ModuleCode NVARCHAR(100),DisplayName NVARCHAR(200),Description NVARCHAR(2000),OwningSchemaCode NVARCHAR(50),NavigationRoute NVARCHAR(500),SortOrder INT);
INSERT @Modules VALUES
(N'CRM',N'CRM',N'Lead, opportunity, account, and customer relationship workflows.',N'CRM',N'/crm',10),
(N'LEAD',N'Lead',N'Lead qualification and conversion workflows.',N'CRM',N'/crm/leads',20),
(N'OPPORTUNITY',N'Opportunity',N'Revenue opportunity and pipeline workflows.',N'CRM',N'/crm/opportunities',30),
(N'ACCOUNT',N'Account',N'Customer and prospect master records and Account 360.',N'Client',N'/accounts',40),
(N'SUBMISSION',N'Submission',N'Underwriting package and placement preparation workflows.',N'Submissions',N'/submissions',50),
(N'QUOTE',N'Quote',N'Market pricing and coverage offer workflows.',N'Submissions',N'/quotes',60),
(N'PROPOSAL',N'Proposal',N'Customer presentation and quote comparison workflows.',N'Submissions',N'/proposals',70),
(N'BIND_REQUEST',N'Bind Request',N'Customer-authorized carrier binding workflow.',N'Submissions',N'/bind-requests',80),
(N'POLICY',N'Policy',N'Bound policy servicing and lifecycle management.',N'Submissions',N'/policies',90),
(N'ENDORSEMENT',N'Endorsement',N'Policy change request and carrier confirmation workflows.',N'Endorsements',N'/endorsements',100),
(N'RENEWAL',N'Renewal',N'Renewal preparation, retention, marketing, and disposition workflows.',N'Renewal',N'/renewals',110),
(N'CLAIMS',N'Claims',N'Claim intake, servicing, documentation, and review workflows.',N'Claims',N'/claims',120),
(N'CERTIFICATES',N'Certificates',N'Certificate issuance, holder, and renewal workflows.',N'Certificates',N'/certificates',130),
(N'ACCOUNTING',N'Accounting',N'Billing, receivables, payables, reconciliation, and commission workflows.',N'Accounting',N'/accounting',140),
(N'DOCUMENTS',N'Documents',N'Business-facing document access that delegates storage and intelligence to the Document Platform.',N'DMS',N'/documents',150);
MERGE Platform.BusinessModuleCatalog target USING @Modules source ON target.TenantId IS NULL AND target.ModuleCode=source.ModuleCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,OwningSchemaCode=source.OwningSchemaCode,NavigationRoute=source.NavigationRoute,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TenantId,ModuleCode,DisplayName,Description,OwningSchemaCode,NavigationRoute,IsActive,SortOrder,CreatedDateUtc,IsDeleted) VALUES(NULL,source.ModuleCode,source.DisplayName,source.Description,source.OwningSchemaCode,source.NavigationRoute,1,source.SortOrder,SYSUTCDATETIME(),0);

DECLARE @Dependencies TABLE(ModuleCode NVARCHAR(100),ServiceCode NVARCHAR(100),UsageCode NVARCHAR(100),Description NVARCHAR(2000),IsRequired BIT);
INSERT @Dependencies
SELECT module.ModuleCode,service.ServiceCode,N'CONSUMES',CONCAT(module.DisplayName,N' consumes the shared ',service.DisplayName,N'; the capability must not be reimplemented inside the business module.'),1
FROM @Modules module CROSS JOIN @Services service
WHERE service.ServiceKindCode=N'PLATFORM' AND service.ServiceCode IN(N'IDENTITY',N'AUTHORIZATION',N'AUDIT',N'WORKFLOW',N'NOTIFICATION',N'SEARCH',N'DOCUMENT',N'INTELLIGENCE',N'RULES',N'VALIDATION',N'CONFIGURATION',N'INTEGRATION',N'REPORTING');
MERGE Platform.ModuleServiceDependency target USING
(
	SELECT module.BusinessModuleId,service.PlatformServiceId,dependency.UsageCode,dependency.Description,dependency.IsRequired
	FROM @Dependencies dependency
	JOIN Platform.BusinessModuleCatalog module ON module.TenantId IS NULL AND module.ModuleCode=dependency.ModuleCode AND module.IsDeleted=0
	JOIN Platform.ServiceCatalog service ON service.TenantId IS NULL AND service.ServiceCode=dependency.ServiceCode AND service.IsDeleted=0
) source ON target.TenantId IS NULL AND target.BusinessModuleId=source.BusinessModuleId AND target.PlatformServiceId=source.PlatformServiceId AND target.UsageCode=source.UsageCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET Description=source.Description,IsRequired=source.IsRequired,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TenantId,BusinessModuleId,PlatformServiceId,UsageCode,Description,IsRequired,IsActive,CreatedDateUtc,IsDeleted) VALUES(NULL,source.BusinessModuleId,source.PlatformServiceId,source.UsageCode,source.Description,source.IsRequired,1,SYSUTCDATETIME(),0);

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(2000),DataTypeCode NVARCHAR(50),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Platform.Rules.AllowedConditionOperators',N'["EQUALS","NOT_EQUALS","GREATER_THAN","GREATER_THAN_OR_EQUAL","LESS_THAN","LESS_THAN_OR_EQUAL","IS_EMPTY","IS_NOT_EMPTY","CONTAINS","IN"]',N'JSON',N'Allowlisted operators for the shared deterministic Rules Platform.'),
	(N'Platform.Validation.AllowedConditionOperators',N'["EQUALS","NOT_EQUALS","GREATER_THAN","GREATER_THAN_OR_EQUAL","LESS_THAN","LESS_THAN_OR_EQUAL","IS_EMPTY","IS_NOT_EMPTY","CONTAINS","IN"]',N'JSON',N'Allowlisted operators for the shared Validation Platform.'),
	(N'Platform.Architecture.EnforceDeclaredDependencies',N'true',N'Boolean',N'Requires business modules to consume declared shared platform-service contracts.');
	MERGE Core.ConfigurationSetting target USING @Config source ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Platform',SettingValue=source.SettingValue,DefaultValue=source.SettingValue,DataTypeCode=source.DataTypeCode,Description=source.Description,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc) VALUES(NEWID(),NULL,N'Platform',N'Platform',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,0,SYSUTCDATETIME());
END;

IF OBJECT_ID(N'AI.RecommendationRule',N'U') IS NOT NULL
BEGIN
	MERGE Rules.RuleDefinition target USING
	(
		SELECT recommendationRule.TenantId,CONCAT(N'RECOMMENDATION.',recommendationRule.RuleCode) RuleCode,recommendationRule.Name DisplayName,COALESCE(recommendationRule.Description,recommendationRule.Name) Description,N'RECOMMENDATION' RuleCategoryCode,recommendationRule.EntityTypeCode,N'AI' SourceModuleCode,recommendationRule.ConditionJson,recommendationRule.ActionJson OutcomeJson,CASE WHEN recommendationRule.Priority>=80 THEN N'HIGH' WHEN recommendationRule.Priority>=50 THEN N'MEDIUM' ELSE N'LOW' END SeverityCode,CAST(0 AS bit) StopsProcessing,recommendationRule.EffectiveFromUtc,recommendationRule.EffectiveToUtc,1 VersionNumber,recommendationRule.IsActive
		FROM AI.RecommendationRule recommendationRule WHERE recommendationRule.IsDeleted=0
	) source ON ((target.TenantId=source.TenantId) OR (target.TenantId IS NULL AND source.TenantId IS NULL)) AND target.RuleCode=source.RuleCode AND target.VersionNumber=source.VersionNumber AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,RuleCategoryCode=source.RuleCategoryCode,EntityTypeCode=source.EntityTypeCode,SourceModuleCode=source.SourceModuleCode,ConditionJson=source.ConditionJson,OutcomeJson=source.OutcomeJson,SeverityCode=source.SeverityCode,StopsProcessing=source.StopsProcessing,EffectiveFromUtc=source.EffectiveFromUtc,EffectiveToUtc=source.EffectiveToUtc,IsActive=source.IsActive,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(TenantId,RuleCode,DisplayName,Description,RuleCategoryCode,EntityTypeCode,SourceModuleCode,ConditionJson,OutcomeJson,SeverityCode,StopsProcessing,EffectiveFromUtc,EffectiveToUtc,VersionNumber,IsActive,CreatedDateUtc,IsDeleted) VALUES(source.TenantId,source.RuleCode,source.DisplayName,source.Description,source.RuleCategoryCode,source.EntityTypeCode,source.SourceModuleCode,source.ConditionJson,source.OutcomeJson,source.SeverityCode,source.StopsProcessing,source.EffectiveFromUtc,source.EffectiveToUtc,source.VersionNumber,source.IsActive,SYSUTCDATETIME(),0);
END;

IF OBJECT_ID(N'AI.ComplianceRequirement',N'U') IS NOT NULL
BEGIN
	MERGE Validation.ValidationDefinition target USING
	(
		SELECT requirement.TenantId,CONCAT(N'COMPLIANCE.',requirement.RequirementCode) ValidationCode,requirement.DisplayName,requirement.Description,CASE requirement.RequirementTypeCode WHEN N'DOCUMENT' THEN N'DOCUMENT' WHEN N'REQUIRED_FIELD' THEN N'REQUIRED_FIELD' WHEN N'STATE' THEN N'STATE' WHEN N'CARRIER' THEN N'CARRIER' ELSE N'BUSINESS' END ValidatorTypeCode,requirement.EntityTypeCode,N'AI' SourceModuleCode,requirement.JurisdictionCode,requirement.CarrierId,requirement.LineOfBusinessCode,requirement.EvaluationJson ConditionJson,CONCAT(N'{"message":"',STRING_ESCAPE(requirement.Description,N'json'),N'","requirementCode":"',STRING_ESCAPE(requirement.RequirementCode,N'json'),N'"}') FailureJson,requirement.SeverityCode,requirement.BlocksTransaction IsBlocking,requirement.CanBeWaived,requirement.WaiverPermissionCode,requirement.EffectiveFromUtc,requirement.EffectiveToUtc,requirement.VersionNumber,requirement.IsActive
		FROM AI.ComplianceRequirement requirement WHERE requirement.IsDeleted=0
	) source ON target.TenantId=source.TenantId AND target.ValidationCode=source.ValidationCode AND target.VersionNumber=source.VersionNumber AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,ValidatorTypeCode=source.ValidatorTypeCode,EntityTypeCode=source.EntityTypeCode,SourceModuleCode=source.SourceModuleCode,JurisdictionCode=source.JurisdictionCode,CarrierId=source.CarrierId,LineOfBusinessCode=source.LineOfBusinessCode,ConditionJson=source.ConditionJson,FailureJson=source.FailureJson,SeverityCode=source.SeverityCode,IsBlocking=source.IsBlocking,CanBeWaived=source.CanBeWaived,WaiverPermissionCode=source.WaiverPermissionCode,EffectiveFromUtc=source.EffectiveFromUtc,EffectiveToUtc=source.EffectiveToUtc,IsActive=source.IsActive,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(TenantId,ValidationCode,DisplayName,Description,ValidatorTypeCode,EntityTypeCode,SourceModuleCode,JurisdictionCode,CarrierId,LineOfBusinessCode,ConditionJson,FailureJson,SeverityCode,IsBlocking,CanBeWaived,WaiverPermissionCode,EffectiveFromUtc,EffectiveToUtc,VersionNumber,IsActive,CreatedDateUtc,IsDeleted) VALUES(source.TenantId,source.ValidationCode,source.DisplayName,source.Description,source.ValidatorTypeCode,source.EntityTypeCode,source.SourceModuleCode,source.JurisdictionCode,source.CarrierId,source.LineOfBusinessCode,source.ConditionJson,source.FailureJson,source.SeverityCode,source.IsBlocking,source.CanBeWaived,source.WaiverPermissionCode,source.EffectiveFromUtc,source.EffectiveToUtc,source.VersionNumber,source.IsActive,SYSUTCDATETIME(),0);
END;
