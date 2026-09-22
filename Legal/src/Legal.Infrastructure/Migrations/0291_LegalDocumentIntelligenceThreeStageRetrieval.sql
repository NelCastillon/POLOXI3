SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackDocumentType', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackDocumentType
(
	DecisionDomainPackDocumentTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackDocumentType PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackDocumentType_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	DocumentTypeCode NVARCHAR(80) NOT NULL,
	Name NVARCHAR(200) NOT NULL,
	Description NVARCHAR(1000) NULL,
	PreferredEvidenceTypeCode NVARCHAR(60) NULL,
	ExtractionSchemaCode NVARCHAR(80) NULL,
	VerificationProfileCode NVARCHAR(60) NULL,
	IsActive BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDocumentType_Active DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDocumentType_Sort DEFAULT 0,
	TenantId UNIQUEIDENTIFIER NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDocumentType_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDocumentType_Deleted DEFAULT 0
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackDocumentType') AND name=N'UX_Legal_DecisionDomainPackDocumentType_Global')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainPackDocumentType_Global ON POLOXI.Legal_DecisionDomainPackDocumentType (DecisionDomainPackId, DocumentTypeCode) WHERE TenantId IS NULL AND IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackDocumentType') AND name=N'UX_Legal_DecisionDomainPackDocumentType_Tenant')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainPackDocumentType_Tenant ON POLOXI.Legal_DecisionDomainPackDocumentType (DecisionDomainPackId, TenantId, DocumentTypeCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterDocument', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterDocument
(
	LegalDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterDocument PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterDocument_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	DecisionDomainPackId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterDocument_DomainPack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	DocumentControlNumber NVARCHAR(100) NOT NULL,
	FileName NVARCHAR(500) NOT NULL,
	ContentType NVARCHAR(200) NOT NULL,
	DocumentTypeCode NVARCHAR(80) NULL,
	StatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterDocument_Status DEFAULT N'RECEIVED',
	CurrentVersionNumber INT NOT NULL CONSTRAINT DF_Legal_MatterDocument_Version DEFAULT 1,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterDocument_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterDocument_Deleted DEFAULT 0
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_MatterDocument') AND name=N'UX_Legal_MatterDocument_Control')
	CREATE UNIQUE INDEX UX_Legal_MatterDocument_Control ON POLOXI.Legal_MatterDocument (TenantId, DocumentControlNumber) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_MatterDocument') AND name=N'IX_Legal_MatterDocument_Matter')
	CREATE INDEX IX_Legal_MatterDocument_Matter ON POLOXI.Legal_MatterDocument (TenantId, DecisionMatterId, StatusCode, DocumentTypeCode) INCLUDE (FileName, CurrentVersionNumber) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterDocumentVersion', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterDocumentVersion
(
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterDocumentVersion PRIMARY KEY DEFAULT NEWID(),
	LegalDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterDocumentVersion_Document REFERENCES POLOXI.Legal_MatterDocument (LegalDocumentId),
	VersionNumber INT NOT NULL,
	Sha256Hash CHAR(64) NOT NULL,
	StorageReference NVARCHAR(2000) NOT NULL,
	FileSizeBytes BIGINT NOT NULL,
	MalwareStatusCode NVARCHAR(40) NOT NULL,
	ProcessingStatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterDocumentVersion_Status DEFAULT N'RECEIVED',
	NativeTextAvailable BIT NOT NULL CONSTRAINT DF_Legal_MatterDocumentVersion_Native DEFAULT 0,
	ExtractionProviderCode NVARCHAR(100) NULL,
	ExtractionModelCode NVARCHAR(100) NULL,
	ExtractionModelVersion NVARCHAR(60) NULL,
	ProcessedDateUtc DATETIME2 NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterDocumentVersion_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterDocumentVersion_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_MatterDocumentVersion_Size CHECK (FileSizeBytes > 0)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_MatterDocumentVersion') AND name=N'UX_Legal_MatterDocumentVersion_Number')
	CREATE UNIQUE INDEX UX_Legal_MatterDocumentVersion_Number ON POLOXI.Legal_MatterDocumentVersion (LegalDocumentId, VersionNumber) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_MatterDocumentVersion') AND name=N'IX_Legal_MatterDocumentVersion_Hash')
	CREATE INDEX IX_Legal_MatterDocumentVersion_Hash ON POLOXI.Legal_MatterDocumentVersion (TenantId, Sha256Hash) INCLUDE (LegalDocumentId, VersionNumber, ProcessingStatusCode) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_DocumentProcessingRun', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DocumentProcessingRun
(
	LegalDocumentProcessingRunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DocumentProcessingRun PRIMARY KEY DEFAULT NEWID(),
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DocumentProcessingRun_Version REFERENCES POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId),
	StageCode NVARCHAR(60) NOT NULL,
	StatusCode NVARCHAR(40) NOT NULL,
	ProviderCode NVARCHAR(100) NULL,
	ModelCode NVARCHAR(100) NULL,
	ModelVersion NVARCHAR(60) NULL,
	InputHash CHAR(64) NULL,
	OutputHash CHAR(64) NULL,
	ArtifactReference NVARCHAR(2000) NULL,
	AttemptCount INT NOT NULL CONSTRAINT DF_Legal_DocumentProcessingRun_Attempt DEFAULT 1,
	StartedDateUtc DATETIME2 NOT NULL,
	CompletedDateUtc DATETIME2 NULL,
	DurationMilliseconds BIGINT NULL,
	ErrorCode NVARCHAR(100) NULL,
	ErrorMessage NVARCHAR(4000) NULL,
	CorrelationId NVARCHAR(120) NOT NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentProcessingRun_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DocumentProcessingRun_Deleted DEFAULT 0
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentProcessingRun') AND name=N'IX_Legal_DocumentProcessingRun_VersionStage')
	CREATE INDEX IX_Legal_DocumentProcessingRun_VersionStage ON POLOXI.Legal_DocumentProcessingRun (TenantId, LegalDocumentVersionId, StageCode, CreatedDateUtc DESC) INCLUDE (StatusCode, ProviderCode, ModelCode) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_DocumentPassage', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DocumentPassage
(
	LegalDocumentPassageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DocumentPassage PRIMARY KEY DEFAULT NEWID(),
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DocumentPassage_Version REFERENCES POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId),
	PageNumber INT NULL,
	SectionPath NVARCHAR(1000) NULL,
	SequenceNumber INT NOT NULL,
	PassageText NVARCHAR(MAX) NOT NULL,
	ExtractionMethodCode NVARCHAR(60) NOT NULL,
	ExtractionConfidence DECIMAL(5,4) NULL,
	BoundingRegionJson NVARCHAR(MAX) NULL,
	SourceSpanJson NVARCHAR(MAX) NULL,
	EpistemicStateCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DocumentPassage_Epistemic DEFAULT N'PROPOSED',
	ContentHash CHAR(64) NOT NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentPassage_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DocumentPassage_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_DocumentPassage_Confidence CHECK (ExtractionConfidence IS NULL OR ExtractionConfidence BETWEEN 0 AND 1)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentPassage') AND name=N'UX_Legal_DocumentPassage_Sequence')
	CREATE UNIQUE INDEX UX_Legal_DocumentPassage_Sequence ON POLOXI.Legal_DocumentPassage (LegalDocumentVersionId, SequenceNumber) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentPassage') AND name=N'IX_Legal_DocumentPassage_Page')
	CREATE INDEX IX_Legal_DocumentPassage_Page ON POLOXI.Legal_DocumentPassage (TenantId, LegalDocumentVersionId, PageNumber, SequenceNumber) INCLUDE (ExtractionMethodCode, EpistemicStateCode, ContentHash) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterEvidenceItem', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterEvidenceItem
(
	LegalEvidenceItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterEvidenceItem PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterEvidenceItem_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterEvidenceItem_Version REFERENCES POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId),
	LegalDocumentPassageId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterEvidenceItem_Passage REFERENCES POLOXI.Legal_DocumentPassage (LegalDocumentPassageId),
	EvidenceTypeCode NVARCHAR(60) NOT NULL,
	DimensionCode NVARCHAR(60) NOT NULL,
	Summary NVARCHAR(2000) NOT NULL,
	EvidenceStateCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterEvidenceItem_State DEFAULT N'PROPOSED',
	Confidence DECIMAL(5,4) NULL,
	GenerationOriginCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterEvidenceItem_Origin DEFAULT N'DYNAMIC_LLM',
	DomainConceptCode NVARCHAR(80) NULL,
	VerificationProfileCode NVARCHAR(60) NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterEvidenceItem_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceItem_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_MatterEvidenceItem_Confidence CHECK (Confidence IS NULL OR Confidence BETWEEN 0 AND 1)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_MatterEvidenceItem') AND name=N'IX_Legal_MatterEvidenceItem_Matter')
	CREATE INDEX IX_Legal_MatterEvidenceItem_Matter ON POLOXI.Legal_MatterEvidenceItem (TenantId, DecisionMatterId, EvidenceStateCode, DimensionCode) INCLUDE (EvidenceTypeCode, LegalDocumentVersionId, LegalDocumentPassageId, DomainConceptCode) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterFactProposition', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterFactProposition
(
	LegalFactPropositionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterFactProposition PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterFactProposition_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	PropositionText NVARCHAR(3000) NOT NULL,
	FactStateCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterFactProposition_State DEFAULT N'ALLEGED',
	GenerationOriginCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterFactProposition_Origin DEFAULT N'DYNAMIC_LLM',
	Confidence DECIMAL(5,4) NULL,
	IsDecisionAuthoritative BIT NOT NULL CONSTRAINT DF_Legal_MatterFactProposition_Authoritative DEFAULT 0,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterFactProposition_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterFactProposition_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_MatterFactProposition_Confidence CHECK (Confidence IS NULL OR Confidence BETWEEN 0 AND 1),
	CONSTRAINT CK_Legal_MatterFactProposition_Authority CHECK (IsDecisionAuthoritative=0 OR FactStateCode IN (N'SUPPORTED',N'ESTABLISHED'))
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_MatterFactProposition') AND name=N'IX_Legal_MatterFactProposition_Matter')
	CREATE INDEX IX_Legal_MatterFactProposition_Matter ON POLOXI.Legal_MatterFactProposition (TenantId, DecisionMatterId, FactStateCode, IsDecisionAuthoritative) INCLUDE (Confidence, GenerationOriginCode) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterPropositionSupport', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterPropositionSupport
(
	LegalPropositionSupportId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterPropositionSupport PRIMARY KEY DEFAULT NEWID(),
	LegalFactPropositionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterPropositionSupport_Proposition REFERENCES POLOXI.Legal_MatterFactProposition (LegalFactPropositionId),
	LegalEvidenceItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterPropositionSupport_Evidence REFERENCES POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId),
	RelationshipTypeCode NVARCHAR(40) NOT NULL,
	AssessmentReason NVARCHAR(2000) NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterPropositionSupport_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterPropositionSupport_Deleted DEFAULT 0
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_MatterPropositionSupport') AND name=N'UX_Legal_MatterPropositionSupport_Edge')
	CREATE UNIQUE INDEX UX_Legal_MatterPropositionSupport_Edge ON POLOXI.Legal_MatterPropositionSupport (LegalFactPropositionId, LegalEvidenceItemId, RelationshipTypeCode) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_DecisionRetrievalTelemetry', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionRetrievalTelemetry
(
	DecisionRetrievalTelemetryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionRetrievalTelemetry PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_DecisionRetrievalTelemetry_Session REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId),
	DecisionMatterId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_DecisionRetrievalTelemetry_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	StageCode NVARCHAR(60) NOT NULL,
	EventCode NVARCHAR(80) NOT NULL,
	RouteCode NVARCHAR(60) NOT NULL,
	Enabled BIT NOT NULL,
	CandidateCount INT NOT NULL CONSTRAINT DF_Legal_DecisionRetrievalTelemetry_Candidates DEFAULT 0,
	FilteredCount INT NOT NULL CONSTRAINT DF_Legal_DecisionRetrievalTelemetry_Filtered DEFAULT 0,
	ReturnedCount INT NOT NULL CONSTRAINT DF_Legal_DecisionRetrievalTelemetry_Returned DEFAULT 0,
	ResearchNeedTypeCode NVARCHAR(60) NULL,
	SourceClassCode NVARCHAR(60) NULL,
	Jurisdiction NVARCHAR(120) NULL,
	DetailJson NVARCHAR(MAX) NULL,
	DurationMilliseconds BIGINT NOT NULL CONSTRAINT DF_Legal_DecisionRetrievalTelemetry_Duration DEFAULT 0,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionRetrievalTelemetry_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DecisionRetrievalTelemetry_Deleted DEFAULT 0
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DecisionRetrievalTelemetry') AND name=N'IX_Legal_DecisionRetrievalTelemetry_Session')
	CREATE INDEX IX_Legal_DecisionRetrievalTelemetry_Session ON POLOXI.Legal_DecisionRetrievalTelemetry (TenantId, DecisionSessionId, StageCode, CreatedDateUtc) INCLUDE (EventCode, RouteCode, ReturnedCount, DurationMilliseconds) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DecisionRetrievalTelemetry') AND name=N'IX_Legal_DecisionRetrievalTelemetry_Matter')
	CREATE INDEX IX_Legal_DecisionRetrievalTelemetry_Matter ON POLOXI.Legal_DecisionRetrievalTelemetry (TenantId, DecisionMatterId, StageCode, CreatedDateUtc) INCLUDE (EventCode, RouteCode, ReturnedCount, DurationMilliseconds) WHERE IsDeleted=0;
GO

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.DocumentIntelligence.SemanticEnrichment.Enabled', N'false', N'Boolean', N'Enables Stage 1 Domain Pack-guided semantic enrichment after deterministic document processing.'),
	(N'Decision.MatterContext.Enabled', N'false', N'Boolean', N'Enables Stage 2 provenance-aware matter context before the initial semantic proposal.'),
	(N'Decision.MatterContext.MaximumItems', N'12', N'Integer', N'Maximum structured matter-context items supplied to the discovery proposal.'),
	(N'Decision.MatterContext.MaximumCharacters', N'18000', N'Integer', N'Maximum total matter-context characters supplied to the discovery proposal.'),
	(N'Decision.MatterContext.LegacyProjectionFallback.Enabled', N'true', N'Boolean', N'Allows ACL-filtered AI.Legal_SearchDocument fallback when no authoritative Legal corpus artifacts exist.'),
	(N'Decision.Research.AuthoritativeRouting.Enabled', N'true', N'Boolean', N'Compatibility telemetry flag. Stage 3 authoritative ResearchNeed and source routing is mandatory and cannot be bypassed.'),
	(N'Decision.LegacyUnconditionalRetrieval.Enabled', N'false', N'Boolean', N'Ablation-only switch for the retired pre-researchability retrieval path.'),
	(N'Decision.RetrievalTelemetry.Enabled', N'true', N'Boolean', N'Persists stage and source-routing telemetry for controlled ablation tests.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey=source.SettingKey
WHEN MATCHED THEN UPDATE SET SettingValue=source.SettingValue, DataTypeCode=source.DataTypeCode, Description=source.Description, ModifiedDateUtc=SYSUTCDATETIME(), IsDeleted=0
WHEN NOT MATCHED THEN INSERT (SettingKey, SettingValue, DataTypeCode, Description) VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

DECLARE @PiPackId UNIQUEIDENTIFIER=(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack WHERE PackCode=N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted=0);
IF @PiPackId IS NULL THROW 50003, 'PERSONAL_INJURY domain pack must exist before migration 0291.', 1;

MERGE POLOXI.Legal_DecisionDomainPackEvidenceType AS target
USING (VALUES
	(N'INSURANCE_COVERAGE',N'Insurance Coverage Records',N'INSURANCE',N'Policies, declarations, endorsements, limits, exclusions, and coverage correspondence.',110)
) AS source (EvidenceTypeCode,Name,DimensionCode,Description,SortOrder)
ON target.DecisionDomainPackId=@PiPackId AND target.EvidenceTypeCode=source.EvidenceTypeCode AND target.TenantId IS NULL
WHEN MATCHED THEN UPDATE SET Name=source.Name,DimensionCode=source.DimensionCode,Description=source.Description,SortOrder=source.SortOrder,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (DecisionDomainPackId,EvidenceTypeCode,Name,DimensionCode,Description,SortOrder,TenantId) VALUES (@PiPackId,source.EvidenceTypeCode,source.Name,source.DimensionCode,source.Description,source.SortOrder,NULL);

MERGE POLOXI.Legal_DecisionDomainPackDocumentType AS target
USING (VALUES
	(N'POLICE_COLLISION_REPORT',N'Police / Collision Report',N'Official incident or collision report.',N'ACCIDENT_REPORT',N'PI_POLICE_REPORT_V1',N'LIABILITY_FACTS',10),
	(N'MEDICAL_RECORD',N'Medical Record',N'Treating-provider clinical record.',N'MEDICAL_RECORDS',N'PI_MEDICAL_RECORD_V1',N'MEDICAL_CAUSATION',20),
	(N'MEDICAL_BILL',N'Medical Bill',N'Provider billing and charge record.',N'MEDICAL_BILLS',N'PI_MEDICAL_BILL_V1',N'DAMAGES_QUANTUM',30),
	(N'IMAGING_REPORT',N'Imaging Report',N'Radiology or diagnostic imaging report.',N'IMAGING',N'PI_IMAGING_REPORT_V1',N'MEDICAL_CAUSATION',40),
	(N'WITNESS_STATEMENT',N'Witness Statement',N'Lay witness account.',N'WITNESS_STATEMENTS',N'PI_WITNESS_STATEMENT_V1',N'LIABILITY_FACTS',50),
	(N'DEPOSITION',N'Deposition',N'Sworn deposition transcript.',N'DEPOSITION',N'PI_DEPOSITION_V1',N'LIABILITY_FACTS',60),
	(N'EXPERT_REPORT',N'Expert Report',N'Medical, reconstruction, economic, or other expert report.',N'EXPERT_REPORTS',N'PI_EXPERT_REPORT_V1',N'EXPERT_FOUNDATION',70),
	(N'INSURANCE_POLICY',N'Insurance Policy',N'Policy contract, declarations, forms, and endorsements.',N'INSURANCE_COVERAGE',N'PI_INSURANCE_POLICY_V1',NULL,80),
	(N'COVERAGE_LETTER',N'Coverage Letter',N'Coverage position or reservation-of-rights correspondence.',N'INSURANCE_COVERAGE',N'PI_COVERAGE_LETTER_V1',NULL,90),
	(N'WAGE_RECORD',N'Wage Record',N'Employment and earnings documentation.',N'WAGE_RECORDS',N'PI_WAGE_RECORD_V1',N'DAMAGES_QUANTUM',100),
	(N'DEMAND_LETTER',N'Demand Letter',N'Claim demand and asserted support.',N'DEMAND_OFFER',N'PI_DEMAND_V1',NULL,110),
	(N'SETTLEMENT_OFFER',N'Settlement Offer',N'Settlement offer or related correspondence.',N'DEMAND_OFFER',N'PI_SETTLEMENT_V1',NULL,120),
	(N'PHOTOGRAPH',N'Photograph',N'Scene, vehicle, property, or injury photograph.',N'PHOTOS_VIDEO',N'PI_IMAGE_V1',N'LIABILITY_FACTS',130),
	(N'VIDEO',N'Video',N'Scene, surveillance, dashcam, bodycam, or other video.',N'PHOTOS_VIDEO',N'PI_VIDEO_V1',N'LIABILITY_FACTS',140)
) AS source (DocumentTypeCode,Name,Description,PreferredEvidenceTypeCode,ExtractionSchemaCode,VerificationProfileCode,SortOrder)
ON target.DecisionDomainPackId=@PiPackId AND target.DocumentTypeCode=source.DocumentTypeCode AND target.TenantId IS NULL
WHEN MATCHED THEN UPDATE SET Name=source.Name,Description=source.Description,PreferredEvidenceTypeCode=source.PreferredEvidenceTypeCode,ExtractionSchemaCode=source.ExtractionSchemaCode,VerificationProfileCode=source.VerificationProfileCode,SortOrder=source.SortOrder,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (DecisionDomainPackId,DocumentTypeCode,Name,Description,PreferredEvidenceTypeCode,ExtractionSchemaCode,VerificationProfileCode,SortOrder,TenantId) VALUES (@PiPackId,source.DocumentTypeCode,source.Name,source.Description,source.PreferredEvidenceTypeCode,source.ExtractionSchemaCode,source.VerificationProfileCode,source.SortOrder,NULL);

COMMIT TRANSACTION;
