SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name=N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

IF OBJECT_ID(N'POLOXI.Capability',N'U') IS NULL
CREATE TABLE POLOXI.Capability
(
	CapabilityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_Capability PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NULL,
	CapabilityCode NVARCHAR(120) NOT NULL,
	DisplayName NVARCHAR(200) NOT NULL,
	Description NVARCHAR(1000) NOT NULL,
	EntityTypeCode NVARCHAR(100) NOT NULL,
	ModuleCode NVARCHAR(100) NOT NULL,
	ExecutionHandlerCode NVARCHAR(100) NOT NULL,
	ApprovedTermsJson NVARCHAR(MAX) NOT NULL,
	SupportsRecency BIT NOT NULL,
	MinimumConfidence DECIMAL(5,4) NOT NULL,
	SortOrder INT NOT NULL,
	IsActive BIT NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL,
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_Capability_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT CK_POLOXI_Capability_Confidence CHECK(MinimumConfidence BETWEEN 0 AND 1),
	CONSTRAINT CK_POLOXI_Capability_TermsJson CHECK(ISJSON(ApprovedTermsJson)=1)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Capability') AND name=N'UX_POLOXI_Capability_Code') CREATE UNIQUE INDEX UX_POLOXI_Capability_Code ON POLOXI.Capability(CapabilityCode) WHERE TenantId IS NULL AND IsDeleted=0;

IF OBJECT_ID(N'POLOXI.Hierarchy',N'U') IS NULL
CREATE TABLE POLOXI.Hierarchy
(
	HierarchyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_Hierarchy PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	QuerySignature CHAR(64) NOT NULL,
	ConceptCode NVARCHAR(120) NOT NULL,
	DisplayName NVARCHAR(300) NOT NULL,
	NormalizedQuery NVARCHAR(1000) NOT NULL,
	VersionNumber INT NOT NULL,
	StatusCode NVARCHAR(30) NOT NULL,
	GeneratedByProviderCode NVARCHAR(100) NULL,
	GeneratedByModelCode NVARCHAR(100) NULL,
	Confidence DECIMAL(5,4) NOT NULL,
	UsageCount INT NOT NULL CONSTRAINT DF_POLOXI_Hierarchy_Usage DEFAULT 0,
	SuccessfulUsageCount INT NOT NULL CONSTRAINT DF_POLOXI_Hierarchy_Success DEFAULT 0,
	LastUsedDateUtc DATETIME2 NULL,
	ExpiresDateUtc DATETIME2 NULL,
	CreatedDateUtc DATETIME2 NOT NULL,
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_Hierarchy_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT CK_POLOXI_Hierarchy_Confidence CHECK(Confidence BETWEEN 0 AND 1),
	CONSTRAINT CK_POLOXI_Hierarchy_Version CHECK(VersionNumber>0)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Hierarchy') AND name=N'UX_POLOXI_Hierarchy_Version') CREATE UNIQUE INDEX UX_POLOXI_Hierarchy_Version ON POLOXI.Hierarchy(TenantId,QuerySignature,VersionNumber) WHERE IsDeleted=0;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Hierarchy') AND name=N'IX_POLOXI_Hierarchy_Reusable') CREATE INDEX IX_POLOXI_Hierarchy_Reusable ON POLOXI.Hierarchy(TenantId,QuerySignature,StatusCode,ExpiresDateUtc) INCLUDE(Confidence,UsageCount,SuccessfulUsageCount);

IF OBJECT_ID(N'POLOXI.HierarchyBranch',N'U') IS NULL
CREATE TABLE POLOXI.HierarchyBranch
(
	HierarchyBranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_HierarchyBranch PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	HierarchyId UNIQUEIDENTIFIER NOT NULL,
	ParentHierarchyBranchId UNIQUEIDENTIFIER NULL,
	BranchCode NVARCHAR(120) NOT NULL,
	DisplayName NVARCHAR(300) NOT NULL,
	ProposedCondition NVARCHAR(1000) NOT NULL,
	CapabilityCode NVARCHAR(120) NULL,
	ValidationStatusCode NVARCHAR(30) NOT NULL,
	ValidationMessage NVARCHAR(1000) NULL,
	SearchText NVARCHAR(500) NULL,
	OrderByRecency BIT NOT NULL,
	Confidence DECIMAL(5,4) NOT NULL,
	SortOrder INT NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL,
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_Branch_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT FK_POLOXI_Branch_Hierarchy FOREIGN KEY(HierarchyId) REFERENCES POLOXI.Hierarchy(HierarchyId),
	CONSTRAINT FK_POLOXI_Branch_Parent FOREIGN KEY(ParentHierarchyBranchId) REFERENCES POLOXI.HierarchyBranch(HierarchyBranchId),
	CONSTRAINT CK_POLOXI_Branch_Confidence CHECK(Confidence BETWEEN 0 AND 1)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.HierarchyBranch') AND name=N'IX_POLOXI_Branch_Hierarchy') CREATE INDEX IX_POLOXI_Branch_Hierarchy ON POLOXI.HierarchyBranch(TenantId,HierarchyId,SortOrder) INCLUDE(ValidationStatusCode,CapabilityCode);

IF OBJECT_ID(N'POLOXI.Execution',N'U') IS NULL
CREATE TABLE POLOXI.Execution
(
	PoloxiExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_Execution PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	HierarchyId UNIQUEIDENTIFIER NOT NULL,
	UserId UNIQUEIDENTIFIER NOT NULL,
	QueryText NVARCHAR(1000) NOT NULL,
	CorrelationId NVARCHAR(120) NOT NULL,
	StatusCode NVARCHAR(30) NOT NULL,
	WasHierarchyReused BIT NOT NULL,
	ValidBranchCount INT NOT NULL,
	UnsupportedBranchCount INT NOT NULL,
	ResultCount INT NOT NULL,
	Confidence DECIMAL(5,4) NOT NULL,
	ExplanationStatusCode NVARCHAR(30) NOT NULL,
	Explanation NVARCHAR(MAX) NULL,
	DurationMilliseconds BIGINT NULL,
	StartedDateUtc DATETIME2 NOT NULL,
	CompletedDateUtc DATETIME2 NULL,
	ErrorMessage NVARCHAR(4000) NULL,
	CreatedDateUtc DATETIME2 NOT NULL,
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_Execution_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT FK_POLOXI_Execution_Hierarchy FOREIGN KEY(HierarchyId) REFERENCES POLOXI.Hierarchy(HierarchyId),
	CONSTRAINT CK_POLOXI_Execution_Confidence CHECK(Confidence BETWEEN 0 AND 1)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Execution') AND name=N'IX_POLOXI_Execution_TenantDate') CREATE INDEX IX_POLOXI_Execution_TenantDate ON POLOXI.Execution(TenantId,CreatedDateUtc DESC) INCLUDE(StatusCode,ResultCount,Confidence);

IF OBJECT_ID(N'POLOXI.ExecutionEvidence',N'U') IS NULL
CREATE TABLE POLOXI.ExecutionEvidence
(
	ExecutionEvidenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_ExecutionEvidence PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	PoloxiExecutionId UNIQUEIDENTIFIER NOT NULL,
	HierarchyBranchId UNIQUEIDENTIFIER NOT NULL,
	SearchDocumentId UNIQUEIDENTIFIER NOT NULL,
	EntityTypeCode NVARCHAR(100) NOT NULL,
	EntityId UNIQUEIDENTIFIER NOT NULL,
	SourceReference NVARCHAR(2000) NOT NULL,
	Title NVARCHAR(500) NOT NULL,
	Excerpt NVARCHAR(2000) NULL,
	RelevanceScore DECIMAL(9,6) NOT NULL,
	RankNumber INT NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL,
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_Evidence_IsDeleted DEFAULT 0,
	CONSTRAINT FK_POLOXI_Evidence_Execution FOREIGN KEY(PoloxiExecutionId) REFERENCES POLOXI.Execution(PoloxiExecutionId),
	CONSTRAINT FK_POLOXI_Evidence_Branch FOREIGN KEY(HierarchyBranchId) REFERENCES POLOXI.HierarchyBranch(HierarchyBranchId),
	CONSTRAINT CK_POLOXI_Evidence_Score CHECK(RelevanceScore BETWEEN 0 AND 1)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.ExecutionEvidence') AND name=N'IX_POLOXI_Evidence_Execution') CREATE INDEX IX_POLOXI_Evidence_Execution ON POLOXI.ExecutionEvidence(TenantId,PoloxiExecutionId,RankNumber) INCLUDE(HierarchyBranchId,SearchDocumentId,RelevanceScore);

DECLARE @Capabilities TABLE(CapabilityCode NVARCHAR(120),DisplayName NVARCHAR(200),Description NVARCHAR(1000),EntityTypeCode NVARCHAR(100),ModuleCode NVARCHAR(100),ApprovedTermsJson NVARCHAR(MAX),SupportsRecency BIT,MinimumConfidence DECIMAL(5,4),SortOrder INT);
INSERT @Capabilities VALUES
(N'SEARCH_SUBMISSIONS',N'Submission evidence',N'Authorized submission records, including recency and indexed submission terms.',N'Submission',N'Submissions',N'["submission","submissions","renewal","quote","underwriting","marketing"]',1,.6500,10),
(N'SEARCH_POLICIES',N'Policy evidence',N'Authorized policy records and indexed policy terms.',N'Policy',N'Submissions',N'["policy","policies","renewal","expiration","cancellation","coverage","premium"]',1,.6500,20),
(N'SEARCH_CLAIMS',N'Claim evidence',N'Authorized claim records and indexed claim terms.',N'Claim',N'Claims',N'["claim","claims","loss","severity","open","closed"]',1,.6500,30),
(N'SEARCH_ACCOUNTS',N'Account evidence',N'Authorized account and customer records.',N'Account',N'Client',N'["account","accounts","customer","customers","client","retention","attrition"]',1,.6500,40),
(N'SEARCH_DOCUMENTS',N'Document evidence',N'Authorized document records and indexed document terms.',N'Document',N'DMS',N'["document","documents","missing","requirement","compliance"]',1,.6500,50),
(N'SEARCH_TASKS',N'Task evidence',N'Authorized indexed task records when available.',N'Task',N'Agency',N'["task","tasks","unresolved","overdue","service"]',1,.6500,60);
MERGE POLOXI.Capability target USING @Capabilities source ON target.TenantId IS NULL AND target.CapabilityCode=source.CapabilityCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,EntityTypeCode=source.EntityTypeCode,ModuleCode=source.ModuleCode,ExecutionHandlerCode=N'AUTHORIZED_SEARCH_DOCUMENT',ApprovedTermsJson=source.ApprovedTermsJson,SupportsRecency=source.SupportsRecency,MinimumConfidence=source.MinimumConfidence,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(CapabilityId,TenantId,CapabilityCode,DisplayName,Description,EntityTypeCode,ModuleCode,ExecutionHandlerCode,ApprovedTermsJson,SupportsRecency,MinimumConfidence,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.CapabilityCode,source.DisplayName,source.Description,source.EntityTypeCode,source.ModuleCode,N'AUTHORIZED_SEARCH_DOCUMENT',source.ApprovedTermsJson,source.SupportsRecency,source.MinimumConfidence,source.SortOrder,1,SYSUTCDATETIME(),0);

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(2000),DataTypeCode NVARCHAR(50),Description NVARCHAR(1000));
	INSERT @Settings VALUES
	(N'Intelligence.Poloxi.EnableHierarchyReuse',N'true',N'Boolean',N'Reuse validated POLOXI hierarchies by normalized query signature.'),
	(N'Intelligence.Poloxi.HierarchyCacheHours',N'168',N'Integer',N'Hours a validated generated POLOXI hierarchy remains reusable.'),
	(N'Intelligence.Poloxi.MinimumBranchConfidence',N'0.65',N'Decimal',N'Minimum confidence for an POLOXI branch to be executed.'),
	(N'Intelligence.Poloxi.MaximumBranches',N'12',N'Integer',N'Maximum proposed POLOXI branches per search.'),
	(N'Intelligence.Poloxi.MaximumResults',N'50',N'Integer',N'Maximum ranked POLOXI evidence results per search.');
	MERGE Core.ConfigurationSetting target USING @Settings source ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.SettingValue,DataTypeCode=source.DataTypeCode,Description=source.Description,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0);
END;

IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL AND OBJECT_ID(N'AI.ModelDeployment',N'U') IS NOT NULL AND OBJECT_ID(N'Core.Tenant',N'U') IS NOT NULL
BEGIN
	;WITH Routes AS(SELECT tenant.TenantId,(SELECT TOP(1) model.ModelDeploymentId FROM AI.ModelDeployment model JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0 WHERE model.IsActive=1 AND model.IsDeleted=0 AND model.CapabilityCode=N'CHAT' AND (model.TenantId=tenant.TenantId OR model.TenantId IS NULL) ORDER BY CASE WHEN model.TenantId=tenant.TenantId THEN 0 ELSE 1 END,model.IsFallback,model.Priority) ModelDeploymentId FROM Core.Tenant tenant WHERE tenant.IsDeleted=0),
	Features AS(SELECT N'INTELLIGENCE_POLOXI_HIERARCHY' FeatureCode,CONVERT(decimal(4,3),.100) Temperature,8000 MaximumInputTokens,3000 MaximumOutputTokens,45 TimeoutSeconds,CONVERT(decimal(5,4),.6500) MinimumConfidence UNION ALL SELECT N'INTELLIGENCE_POLOXI_EXPLANATION',CONVERT(decimal(4,3),.200),16000,2000,60,CONVERT(decimal(5,4),0)),
	SourcePolicy AS(SELECT route.TenantId,feature.*,route.ModelDeploymentId FROM Routes route CROSS JOIN Features feature WHERE route.ModelDeploymentId IS NOT NULL)
	MERGE AI.FeaturePolicy target USING SourcePolicy source ON target.TenantId=source.TenantId AND target.FeatureCode=source.FeatureCode AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',PrimaryModelDeploymentId=COALESCE(target.PrimaryModelDeploymentId,source.ModelDeploymentId),Temperature=source.Temperature,MaximumInputTokens=source.MaximumInputTokens,MaximumOutputTokens=source.MaximumOutputTokens,TimeoutSeconds=source.TimeoutSeconds,MinimumConfidence=source.MinimumConfidence,IsEnabled=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(FeaturePolicyId,TenantId,FeatureCode,ModuleCode,PrimaryModelDeploymentId,FallbackModelDeploymentId,Temperature,MaximumInputTokens,MaximumOutputTokens,TimeoutSeconds,DailyCostLimit,MonthlyCostLimit,MinimumConfidence,RequiresHumanReview,IsEnabled,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.FeatureCode,N'Intelligence',source.ModelDeploymentId,NULL,source.Temperature,source.MaximumInputTokens,source.MaximumOutputTokens,source.TimeoutSeconds,NULL,NULL,source.MinimumConfidence,0,1,SYSUTCDATETIME(),0);
END;

COMMIT TRANSACTION;
