SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name=N'AI') EXEC(N'CREATE SCHEMA AI');

IF OBJECT_ID(N'AI.SearchIntentPattern',N'U') IS NULL
BEGIN
	CREATE TABLE AI.SearchIntentPattern
	(
		SearchIntentPatternId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_SearchIntentPattern PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		PatternCode NVARCHAR(100) NOT NULL,
		EntityTypeCode NVARCHAR(100) NULL,
		ModuleCode NVARCHAR(100) NULL,
		ExtractionStrategyCode NVARCHAR(50) NOT NULL,
		Priority INT NOT NULL,
		IsEntityList BIT NOT NULL,
		IsActive BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_SearchIntentPattern_Created DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc DATETIME2 NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_SearchIntentPattern_Deleted DEFAULT 0
	);
END;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'AI.SearchIntentPattern') AND name=N'UX_AI_SearchIntentPattern_Code')
	CREATE UNIQUE INDEX UX_AI_SearchIntentPattern_Code ON AI.SearchIntentPattern(TenantId,PatternCode) WHERE IsDeleted=0;

IF OBJECT_ID(N'AI.SearchIntentPatternPhrase',N'U') IS NULL
BEGIN
	CREATE TABLE AI.SearchIntentPatternPhrase
	(
		SearchIntentPatternPhraseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_SearchIntentPatternPhrase PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		SearchIntentPatternId UNIQUEIDENTIFIER NOT NULL,
		PhraseKindCode NVARCHAR(30) NOT NULL,
		PhraseText NVARCHAR(300) NOT NULL,
		SortOrder INT NOT NULL,
		IsActive BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_SearchIntentPatternPhrase_Created DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc DATETIME2 NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_SearchIntentPatternPhrase_Deleted DEFAULT 0,
		CONSTRAINT FK_AI_SearchIntentPatternPhrase_Pattern FOREIGN KEY(SearchIntentPatternId) REFERENCES AI.SearchIntentPattern(SearchIntentPatternId)
	);
END;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'AI.SearchIntentPatternPhrase') AND name=N'UX_AI_SearchIntentPatternPhrase_Text')
	CREATE UNIQUE INDEX UX_AI_SearchIntentPatternPhrase_Text ON AI.SearchIntentPatternPhrase(SearchIntentPatternId,PhraseKindCode,PhraseText) WHERE IsDeleted=0;

IF OBJECT_ID(N'AI.SearchIntentInterpretationLog',N'U') IS NULL
BEGIN
	CREATE TABLE AI.SearchIntentInterpretationLog
	(
		SearchIntentInterpretationLogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_SearchIntentInterpretationLog PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		UserId UNIQUEIDENTIFIER NOT NULL,
		QueryText NVARCHAR(1000) NOT NULL,
		EntityTypeCode NVARCHAR(100) NULL,
		ModuleCode NVARCHAR(100) NULL,
		SearchText NVARCHAR(1000) NULL,
		SourceEngineCode NVARCHAR(50) NOT NULL,
		Confidence DECIMAL(9,6) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_SearchIntentInterpretationLog_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_SearchIntentInterpretationLog_Deleted DEFAULT 0
	);
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'AI.SearchIntentInterpretationLog') AND name=N'IX_AI_SearchIntentInterpretationLog_TenantDate')
	CREATE INDEX IX_AI_SearchIntentInterpretationLog_TenantDate ON AI.SearchIntentInterpretationLog(TenantId,CreatedDateUtc DESC) INCLUDE(SourceEngineCode,StatusCode,EntityTypeCode,ModuleCode,Confidence) WHERE IsDeleted=0;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(2000),DataTypeCode NVARCHAR(50),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Intelligence.Search.EnableLlmIntentFallback',N'true',N'Boolean',N'Enables LLM fallback for search intent interpretation when DB-backed patterns do not match.'),
	(N'Intelligence.Search.LlmIntentMinimumConfidence',N'0.70',N'Decimal',N'Minimum confidence required to accept an LLM search intent interpretation.'),
	(N'Intelligence.Search.LlmIntentTimeoutSeconds',N'8',N'Integer',N'Maximum seconds allowed for LLM search intent fallback.');
	MERGE Core.ConfigurationSetting target USING @Config source
	ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',SettingValue=source.SettingValue,DefaultValue=source.SettingValue,DataTypeCode=source.DataTypeCode,Description=source.Description,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc)
	VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,0,SYSUTCDATETIME());
END;

IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL AND OBJECT_ID(N'AI.ModelDeployment',N'U') IS NOT NULL AND OBJECT_ID(N'Core.Tenant',N'U') IS NOT NULL
BEGIN
	;WITH TenantChatRoute AS
	(
		SELECT tenant.TenantId,
			(SELECT TOP(1) model.ModelDeploymentId FROM AI.ModelDeployment model JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT' AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL) ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,model.IsFallback,model.Priority,model.CreatedDateUtc) PrimaryModelDeploymentId,
			(SELECT TOP(1) model.ModelDeploymentId FROM AI.ModelDeployment model JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT' AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL) ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,CASE WHEN model.IsFallback=1 THEN 0 ELSE 1 END,model.Priority,model.CreatedDateUtc) FallbackModelDeploymentId
		FROM Core.Tenant tenant
		WHERE tenant.IsDeleted=0
	), SourcePolicy AS
	(
		SELECT TenantId,N'INTELLIGENCE_SEARCH_INTENT' FeatureCode,N'Intelligence' ModuleCode,PrimaryModelDeploymentId,CASE WHEN FallbackModelDeploymentId=PrimaryModelDeploymentId THEN NULL ELSE FallbackModelDeploymentId END FallbackModelDeploymentId,CONVERT(decimal(4,3),0.100) Temperature,4000 MaximumInputTokens,500 MaximumOutputTokens,8 TimeoutSeconds,CONVERT(decimal(5,4),0.7000) MinimumConfidence
		FROM TenantChatRoute WHERE PrimaryModelDeploymentId IS NOT NULL
	)
	MERGE AI.FeaturePolicy target USING SourcePolicy source
	ON target.TenantId=source.TenantId AND target.FeatureCode=source.FeatureCode AND target.IsDeleted=0
	WHEN MATCHED AND target.PrimaryModelDeploymentId IS NULL THEN UPDATE SET ModuleCode=source.ModuleCode,PrimaryModelDeploymentId=source.PrimaryModelDeploymentId,FallbackModelDeploymentId=COALESCE(target.FallbackModelDeploymentId,source.FallbackModelDeploymentId),Temperature=source.Temperature,MaximumInputTokens=source.MaximumInputTokens,MaximumOutputTokens=source.MaximumOutputTokens,TimeoutSeconds=source.TimeoutSeconds,MinimumConfidence=source.MinimumConfidence,IsEnabled=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(FeaturePolicyId,TenantId,FeatureCode,ModuleCode,PrimaryModelDeploymentId,FallbackModelDeploymentId,Temperature,MaximumInputTokens,MaximumOutputTokens,TimeoutSeconds,DailyCostLimit,MonthlyCostLimit,MinimumConfidence,RequiresHumanReview,IsEnabled,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),source.TenantId,source.FeatureCode,source.ModuleCode,source.PrimaryModelDeploymentId,source.FallbackModelDeploymentId,source.Temperature,source.MaximumInputTokens,source.MaximumOutputTokens,source.TimeoutSeconds,NULL,NULL,source.MinimumConfidence,0,1,SYSUTCDATETIME(),0);
END;

IF OBJECT_ID(N'Agency.Staff',N'U') IS NOT NULL AND OBJECT_ID(N'Search.EntityProjection',N'U') IS NOT NULL AND OBJECT_ID(N'AI.SearchDocument',N'U') IS NOT NULL
BEGIN
	DECLARE @ProducerSource TABLE(TenantId UNIQUEIDENTIFIER,EntityTypeCode NVARCHAR(80),EntityId UNIQUEIDENTIFIER,DisplayName NVARCHAR(500),SecondaryText NVARCHAR(1000),NavigationRoute NVARCHAR(500),SourceSchemaName NVARCHAR(128),SourceTableName NVARCHAR(128),SourceModifiedDateUtc DATETIME2,SearchText NVARCHAR(MAX),NormalizedFieldsJson NVARCHAR(MAX),ExactIdentifiersJson NVARCHAR(MAX),PermissionCode NVARCHAR(150));
	INSERT @ProducerSource
	SELECT staff.TenantId,N'Producer',staff.StaffId,CONCAT(staff.FirstName,N' ',staff.LastName),CONCAT_WS(N' · ',staff.Role,staff.Department,staff.Team,staff.Email),CONCAT(N'/tenant/agency/producers?staffId=',staff.StaffId),N'Agency',N'Staff',COALESCE(staff.ModifiedDateUtc,staff.CreatedDateUtc),CONCAT_WS(N' ',staff.FirstName,staff.LastName,staff.Email,staff.Phone,staff.Title,staff.Role,staff.Department,staff.Team,staff.LicenseNumber,staff.LicenseStates,staff.EmploymentStatus),
		(SELECT CONCAT(staff.FirstName,N' ',staff.LastName) DisplayName,CONCAT_WS(N' ',staff.FirstName,staff.LastName,staff.Email,staff.Phone,staff.Title,staff.Role,staff.Department,staff.Team,staff.LicenseNumber,staff.LicenseStates,staff.EmploymentStatus) SearchText,CONCAT(staff.FirstName,N' ',staff.LastName) FullName,staff.Email,staff.Phone,staff.Role,staff.Department,staff.Team,staff.LicenseNumber NpnLicense,staff.LicenseStates FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT staff.Email,staff.Phone,staff.LicenseNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
	FROM Agency.Staff staff WHERE staff.IsDeleted=0 AND staff.IsActive=1 AND staff.Role=N'Producer';

	MERGE Search.EntityProjection target USING @ProducerSource source ON target.TenantId=source.TenantId AND target.EntityTypeCode=source.EntityTypeCode AND target.EntityId=source.EntityId AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,SecondaryText=source.SecondaryText,NavigationRoute=source.NavigationRoute,SourceModifiedDateUtc=source.SourceModifiedDateUtc,SearchText=source.SearchText,NormalizedFieldsJson=source.NormalizedFieldsJson,ExactIdentifiersJson=source.ExactIdentifiersJson,PermissionCode=source.PermissionCode,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(EntityProjectionId,TenantId,EntityTypeCode,EntityId,DisplayName,SecondaryText,NavigationRoute,SourceSchemaName,SourceTableName,SourceModifiedDateUtc,SearchText,NormalizedFieldsJson,ExactIdentifiersJson,PermissionCode,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.EntityTypeCode,source.EntityId,source.DisplayName,source.SecondaryText,source.NavigationRoute,source.SourceSchemaName,source.SourceTableName,source.SourceModifiedDateUtc,source.SearchText,source.NormalizedFieldsJson,source.ExactIdentifiersJson,source.PermissionCode,1,SYSUTCDATETIME(),0);

	MERGE AI.SearchDocument target USING(SELECT projection.TenantId,projection.EntityTypeCode,projection.EntityId,projection.SourceSchemaName ModuleCode,projection.DisplayName Title,COALESCE(projection.SearchText,N'') ContentText,CONCAT_WS(N' ',projection.DisplayName,projection.SecondaryText,projection.ExactIdentifiersJson) Keywords,projection.SourceModifiedDateUtc,CONVERT(char(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),CONCAT_WS(N'|',projection.DisplayName,projection.SecondaryText,projection.SearchText,projection.NormalizedFieldsJson,projection.ExactIdentifiersJson))),2) ContentHash FROM Search.EntityProjection projection WHERE projection.EntityTypeCode=N'Producer' AND projection.IsActive=1 AND projection.IsDeleted=0) source
	ON target.TenantId=source.TenantId AND target.EntityTypeCode=source.EntityTypeCode AND target.EntityId=source.EntityId AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET ModuleCode=source.ModuleCode,Title=source.Title,ContentText=source.ContentText,Keywords=source.Keywords,SecurityScopeJson=N'{"permissionCode":"Intelligence.Search"}',ContentHash=source.ContentHash,SourceModifiedDateUtc=source.SourceModifiedDateUtc,SourceCreatedDateUtc=COALESCE(target.SourceCreatedDateUtc,source.SourceModifiedDateUtc),IndexedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),IsDeleted=0
	WHEN NOT MATCHED THEN INSERT(SearchDocumentId,TenantId,EntityTypeCode,EntityId,ModuleCode,Title,ContentText,Keywords,ConceptIdsJson,SecurityScopeJson,ContentHash,IndexedDateUtc,SourceModifiedDateUtc,SourceCreatedDateUtc,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.EntityTypeCode,source.EntityId,source.ModuleCode,source.Title,source.ContentText,source.Keywords,N'[]',N'{"permissionCode":"Intelligence.Search"}',source.ContentHash,SYSUTCDATETIME(),source.SourceModifiedDateUtc,source.SourceModifiedDateUtc,SYSUTCDATETIME(),0);

	IF OBJECT_ID(N'IAM.RolePermission',N'U') IS NOT NULL AND OBJECT_ID(N'AI.SearchPermission',N'U') IS NOT NULL
	BEGIN
		MERGE AI.SearchPermission target USING
		(
			SELECT DISTINCT document.TenantId,document.SearchDocumentId,N'ROLE' PrincipalTypeCode,rolePermission.RoleId PrincipalId,N'READ' PermissionCode
			FROM AI.SearchDocument document
			JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0
			JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=document.TenantId AND rolePermission.PermissionCode=projection.PermissionCode AND rolePermission.IsDeleted=0
			WHERE document.IsDeleted=0 AND projection.EntityTypeCode=N'Producer'
		) source ON target.TenantId=source.TenantId AND target.SearchDocumentId=source.SearchDocumentId AND target.PrincipalTypeCode=source.PrincipalTypeCode AND target.PrincipalId=source.PrincipalId AND target.PermissionCode=source.PermissionCode
		WHEN MATCHED AND target.IsDeleted=1 THEN UPDATE SET IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
		WHEN NOT MATCHED THEN INSERT(SearchPermissionId,TenantId,SearchDocumentId,PrincipalTypeCode,PrincipalId,PermissionCode,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.SearchDocumentId,source.PrincipalTypeCode,source.PrincipalId,source.PermissionCode,SYSUTCDATETIME(),0);
	END;
END;

DECLARE @Patterns TABLE(PatternCode NVARCHAR(100) PRIMARY KEY,EntityTypeCode NVARCHAR(100) NULL,ModuleCode NVARCHAR(100) NULL,ExtractionStrategyCode NVARCHAR(50),Priority INT,IsEntityList BIT);
INSERT @Patterns VALUES
(N'RECENCY_TERMS',NULL,NULL,N'RECENCY',10,0),
(N'ACCOUNT_BY_CONTACT_NAME',N'Account',N'Client',N'AFTER_MARKER',20,0),
(N'ACCOUNT_BY_CONTACT_CALLED',N'Account',N'Client',N'AFTER_MARKER',21,0),
(N'CONTACT_PERSON_FOR_ACCOUNT',N'Contact',N'Client',N'PREFIX',30,0),
(N'PRIMARY_CONTACT_FOR_ACCOUNT',N'Contact',N'Client',N'PREFIX',31,0),
(N'PRODUCER_FOR_ACCOUNT',N'Producer',N'Agency',N'PREFIX',32,0),
(N'POLICIES_FOR_ACCOUNT',N'Policy',N'Submissions',N'AFTER_MARKER',50,1),
(N'SUBMISSIONS_FOR_ACCOUNT',N'Submission',N'Submissions',N'AFTER_MARKER',51,1),
(N'CLAIMS_FOR_ACCOUNT',N'Claim',N'Claims',N'AFTER_MARKER',52,1),
(N'DOCUMENTS_FOR_ACCOUNT',N'Document',N'DMS',N'AFTER_MARKER',53,1),
(N'CERTIFICATES_FOR_ACCOUNT',N'Certificate',N'DMS',N'AFTER_MARKER',54,1),
(N'CONTACTS_FOR_ACCOUNT',N'Contact',N'Client',N'AFTER_MARKER',57,1),
(N'OPEN_SUBMISSIONS',N'Submission',N'Submissions',N'NONE',70,1),
(N'PENDING_SUBMISSIONS',N'Submission',N'Submissions',N'NONE',71,1),
(N'BOUND_POLICIES',N'Policy',N'Submissions',N'NONE',72,1),
(N'ACTIVE_POLICIES',N'Policy',N'Submissions',N'NONE',73,1),
(N'OPEN_CLAIMS',N'Claim',N'Claims',N'NONE',74,1),
(N'CLOSED_CLAIMS',N'Claim',N'Claims',N'NONE',75,1),
(N'ACTIVE_CONTACTS',N'Contact',N'Client',N'NONE',78,1),
(N'ACTIVE_ACCOUNTS',N'Account',N'Client',N'NONE',79,1),
(N'SUBMISSION_ENTITY',N'Submission',N'Submissions',N'NONE',100,0),
(N'SUBMISSIONS_ENTITY_LIST',N'Submission',N'Submissions',N'NONE',101,1),
(N'ACCOUNT_ENTITY',N'Account',N'Client',N'NONE',110,0),
(N'ACCOUNTS_ENTITY_LIST',N'Account',N'Client',N'NONE',111,1),
(N'CONTACT_ENTITY',N'Contact',N'Client',N'NONE',120,0),
(N'CONTACTS_ENTITY_LIST',N'Contact',N'Client',N'NONE',121,1),
(N'LEAD_ENTITY',N'Lead',N'CRM',N'NONE',130,0),
(N'LEADS_ENTITY_LIST',N'Lead',N'CRM',N'NONE',131,1),
(N'POLICY_ENTITY',N'Policy',N'Submissions',N'NONE',140,0),
(N'POLICIES_ENTITY_LIST',N'Policy',N'Submissions',N'NONE',141,1),
(N'CLAIM_ENTITY',N'Claim',N'Claims',N'NONE',150,0),
(N'CLAIMS_ENTITY_LIST',N'Claim',N'Claims',N'NONE',151,1),
(N'DOCUMENT_ENTITY',N'Document',N'DMS',N'NONE',160,0),
(N'DOCUMENTS_ENTITY_LIST',N'Document',N'DMS',N'NONE',161,1),
(N'CERTIFICATE_ENTITY',N'Certificate',N'DMS',N'NONE',170,0),
(N'CERTIFICATES_ENTITY_LIST',N'Certificate',N'DMS',N'NONE',171,1),
(N'CARRIER_ENTITY',N'Carrier',N'Agency',N'NONE',180,0),
(N'CARRIERS_ENTITY_LIST',N'Carrier',N'Agency',N'NONE',181,1),
(N'PRODUCER_ENTITY_LIST',N'Producer',N'Agency',N'NONE',185,1),
(N'LOCATION_ENTITY',N'Location',N'Client',N'NONE',190,0),
(N'LOCATIONS_ENTITY_LIST',N'Location',N'Client',N'NONE',191,1),
(N'VEHICLE_ENTITY',N'Vehicle',N'Client',N'NONE',200,0),
(N'VEHICLES_ENTITY_LIST',N'Vehicle',N'Client',N'NONE',201,1),
(N'COMMISSION_ENTITY_LIST',N'CommissionLine',N'Commission',N'NONE',210,1);

MERGE AI.SearchIntentPattern target USING @Patterns source
ON target.TenantId IS NULL AND target.PatternCode=source.PatternCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET EntityTypeCode=source.EntityTypeCode,ModuleCode=source.ModuleCode,ExtractionStrategyCode=source.ExtractionStrategyCode,Priority=source.Priority,IsEntityList=source.IsEntityList,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(SearchIntentPatternId,TenantId,PatternCode,EntityTypeCode,ModuleCode,ExtractionStrategyCode,Priority,IsEntityList,IsActive,CreatedDateUtc,IsDeleted)
VALUES(NEWID(),NULL,source.PatternCode,source.EntityTypeCode,source.ModuleCode,source.ExtractionStrategyCode,source.Priority,source.IsEntityList,1,SYSUTCDATETIME(),0);

DECLARE @Phrases TABLE(PatternCode NVARCHAR(100),PhraseKindCode NVARCHAR(30),PhraseText NVARCHAR(300),SortOrder INT,PRIMARY KEY(PatternCode,PhraseKindCode,PhraseText));
INSERT @Phrases VALUES
(N'RECENCY_TERMS',N'MATCH',N'latest',1),(N'RECENCY_TERMS',N'MATCH',N'newest',2),(N'RECENCY_TERMS',N'MATCH',N'most recent',3),(N'RECENCY_TERMS',N'MATCH',N'recently created',4),(N'RECENCY_TERMS',N'MATCH',N'last created',5),(N'RECENCY_TERMS',N'MATCH',N'recent',6),
(N'ACCOUNT_BY_CONTACT_NAME',N'MATCH',N'account',1),(N'ACCOUNT_BY_CONTACT_NAME',N'MATCH',N'contact named',2),(N'ACCOUNT_BY_CONTACT_NAME',N'EXTRACT',N' named ',1),(N'ACCOUNT_BY_CONTACT_NAME',N'EXTRACT',N' called ',2),(N'ACCOUNT_BY_CONTACT_CALLED',N'MATCH',N'account',1),(N'ACCOUNT_BY_CONTACT_CALLED',N'MATCH',N'contact called',2),(N'ACCOUNT_BY_CONTACT_CALLED',N'EXTRACT',N' called ',1),
(N'CONTACT_PERSON_FOR_ACCOUNT',N'MATCH',N'contact person',1),(N'CONTACT_PERSON_FOR_ACCOUNT',N'EXTRACT',N'who is the contact person for ',1),(N'CONTACT_PERSON_FOR_ACCOUNT',N'EXTRACT',N'contact person for ',2),(N'CONTACT_PERSON_FOR_ACCOUNT',N'EXTRACT',N'contact for ',3),
(N'PRIMARY_CONTACT_FOR_ACCOUNT',N'MATCH',N'primary contact',1),(N'PRIMARY_CONTACT_FOR_ACCOUNT',N'EXTRACT',N'who is the primary contact for ',1),(N'PRIMARY_CONTACT_FOR_ACCOUNT',N'EXTRACT',N'primary contact for ',2),
(N'PRODUCER_FOR_ACCOUNT',N'MATCH',N'producer',1),(N'PRODUCER_FOR_ACCOUNT',N'EXTRACT',N'who is the producer for ',1),(N'PRODUCER_FOR_ACCOUNT',N'EXTRACT',N'producer for ',2),
(N'POLICIES_FOR_ACCOUNT',N'MATCH',N'policies',1),(N'POLICIES_FOR_ACCOUNT',N'MATCH',N'account',2),(N'POLICIES_FOR_ACCOUNT',N'EXTRACT',N' for ',1),
(N'SUBMISSIONS_FOR_ACCOUNT',N'MATCH',N'submissions',1),(N'SUBMISSIONS_FOR_ACCOUNT',N'MATCH',N'account',2),(N'SUBMISSIONS_FOR_ACCOUNT',N'EXTRACT',N' for ',1),
(N'CLAIMS_FOR_ACCOUNT',N'MATCH',N'claims',1),(N'CLAIMS_FOR_ACCOUNT',N'MATCH',N'account',2),(N'CLAIMS_FOR_ACCOUNT',N'EXTRACT',N' for ',1),
(N'DOCUMENTS_FOR_ACCOUNT',N'MATCH',N'documents',1),(N'DOCUMENTS_FOR_ACCOUNT',N'MATCH',N'account',2),(N'DOCUMENTS_FOR_ACCOUNT',N'EXTRACT',N' for ',1),
(N'CERTIFICATES_FOR_ACCOUNT',N'MATCH',N'certificates',1),(N'CERTIFICATES_FOR_ACCOUNT',N'MATCH',N'account',2),(N'CERTIFICATES_FOR_ACCOUNT',N'EXTRACT',N' for ',1),
(N'CONTACTS_FOR_ACCOUNT',N'MATCH',N'contacts',1),(N'CONTACTS_FOR_ACCOUNT',N'MATCH',N'account',2),(N'CONTACTS_FOR_ACCOUNT',N'EXTRACT',N' for ',1),
(N'OPEN_SUBMISSIONS',N'MATCH',N'open',1),(N'OPEN_SUBMISSIONS',N'MATCH',N'submissions',2),(N'PENDING_SUBMISSIONS',N'MATCH',N'pending',1),(N'PENDING_SUBMISSIONS',N'MATCH',N'submissions',2),(N'BOUND_POLICIES',N'MATCH',N'bound',1),(N'BOUND_POLICIES',N'MATCH',N'policies',2),(N'ACTIVE_POLICIES',N'MATCH',N'active',1),(N'ACTIVE_POLICIES',N'MATCH',N'policies',2),(N'OPEN_CLAIMS',N'MATCH',N'open',1),(N'OPEN_CLAIMS',N'MATCH',N'claims',2),(N'CLOSED_CLAIMS',N'MATCH',N'closed',1),(N'CLOSED_CLAIMS',N'MATCH',N'claims',2),(N'ACTIVE_CONTACTS',N'MATCH',N'active',1),(N'ACTIVE_CONTACTS',N'MATCH',N'contacts',2),(N'ACTIVE_ACCOUNTS',N'MATCH',N'active',1),(N'ACTIVE_ACCOUNTS',N'MATCH',N'accounts',2),
(N'SUBMISSION_ENTITY',N'MATCH',N'submission',1),(N'SUBMISSIONS_ENTITY_LIST',N'MATCH',N'submissions',1),(N'ACCOUNT_ENTITY',N'MATCH',N'account',1),(N'ACCOUNTS_ENTITY_LIST',N'MATCH',N'accounts',1),(N'CONTACT_ENTITY',N'MATCH',N'contact',1),(N'CONTACTS_ENTITY_LIST',N'MATCH',N'contacts',1),(N'LEAD_ENTITY',N'MATCH',N'lead',1),(N'LEADS_ENTITY_LIST',N'MATCH',N'leads',1),(N'POLICY_ENTITY',N'MATCH',N'policy',1),(N'POLICIES_ENTITY_LIST',N'MATCH',N'policies',1),(N'CLAIM_ENTITY',N'MATCH',N'claim',1),(N'CLAIMS_ENTITY_LIST',N'MATCH',N'claims',1),(N'DOCUMENT_ENTITY',N'MATCH',N'document',1),(N'DOCUMENTS_ENTITY_LIST',N'MATCH',N'documents',1),(N'CERTIFICATE_ENTITY',N'MATCH',N'certificate',1),(N'CERTIFICATES_ENTITY_LIST',N'MATCH',N'certificates',1),(N'CARRIER_ENTITY',N'MATCH',N'carrier',1),(N'CARRIERS_ENTITY_LIST',N'MATCH',N'carriers',1),(N'PRODUCER_ENTITY_LIST',N'MATCH',N'producers',1),(N'PRODUCER_ENTITY_LIST',N'MATCH',N'producer',2),(N'LOCATION_ENTITY',N'MATCH',N'location',1),(N'LOCATIONS_ENTITY_LIST',N'MATCH',N'locations',1),(N'VEHICLE_ENTITY',N'MATCH',N'vehicle',1),(N'VEHICLES_ENTITY_LIST',N'MATCH',N'vehicles',1),(N'COMMISSION_ENTITY_LIST',N'MATCH',N'commissions',1);

MERGE AI.SearchIntentPatternPhrase target USING
(
	SELECT pattern.TenantId,pattern.SearchIntentPatternId,phrase.PhraseKindCode,phrase.PhraseText,phrase.SortOrder
	FROM @Phrases phrase
	JOIN AI.SearchIntentPattern pattern ON pattern.TenantId IS NULL AND pattern.PatternCode=phrase.PatternCode AND pattern.IsDeleted=0
) source
ON target.SearchIntentPatternId=source.SearchIntentPatternId AND target.PhraseKindCode=source.PhraseKindCode AND target.PhraseText=source.PhraseText AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(SearchIntentPatternPhraseId,TenantId,SearchIntentPatternId,PhraseKindCode,PhraseText,SortOrder,IsActive,CreatedDateUtc,IsDeleted)
VALUES(NEWID(),source.TenantId,source.SearchIntentPatternId,source.PhraseKindCode,source.PhraseText,source.SortOrder,1,SYSUTCDATETIME(),0);
