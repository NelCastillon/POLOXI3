SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'knowledge') EXEC(N'CREATE SCHEMA knowledge');

IF OBJECT_ID(N'knowledge.LookupType', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.LookupType
	(
		LookupTypeCode VARCHAR(100) NOT NULL CONSTRAINT PK_KnowledgeLookupType PRIMARY KEY,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(1000) NULL,
		IsSystemDefined BIT NOT NULL CONSTRAINT DF_KnowledgeLookupType_System DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_KnowledgeLookupType_Active DEFAULT 1,
		ModifiedDateUtc DATETIME2(7) NULL
	);
END;

IF OBJECT_ID(N'knowledge.LookupValue', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.LookupValue
	(
		LookupValueId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KnowledgeLookupValue PRIMARY KEY DEFAULT NEWID(),
		LookupTypeCode VARCHAR(100) NOT NULL,
		ValueCode VARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(1000) NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_KnowledgeLookupValue_Sort DEFAULT 0,
		TenantId UNIQUEIDENTIFIER NULL,
		IsSystemDefined BIT NOT NULL CONSTRAINT DF_KnowledgeLookupValue_System DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_KnowledgeLookupValue_Active DEFAULT 1,
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_KnowledgeLookupValue_Created DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc DATETIME2(7) NULL,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_KnowledgeLookupValue_Type FOREIGN KEY (LookupTypeCode) REFERENCES knowledge.LookupType(LookupTypeCode)
	);
	CREATE UNIQUE INDEX UX_KnowledgeLookupValue_System ON knowledge.LookupValue(LookupTypeCode, ValueCode) WHERE TenantId IS NULL;
	CREATE UNIQUE INDEX UX_KnowledgeLookupValue_Tenant ON knowledge.LookupValue(TenantId, LookupTypeCode, ValueCode) WHERE TenantId IS NOT NULL;
END;

IF OBJECT_ID(N'knowledge.Configuration', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.Configuration
	(
		ConfigurationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KnowledgeConfiguration PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		ConfigurationCode VARCHAR(150) NOT NULL,
		ConfigurationValue NVARCHAR(MAX) NOT NULL,
		DataTypeCode VARCHAR(30) NOT NULL,
		Description NVARCHAR(1000) NULL,
		IsSystemDefined BIT NOT NULL CONSTRAINT DF_KnowledgeConfiguration_System DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_KnowledgeConfiguration_Active DEFAULT 1,
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_KnowledgeConfiguration_Created DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc DATETIME2(7) NULL,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_KnowledgeConfiguration_System ON knowledge.Configuration(ConfigurationCode) WHERE TenantId IS NULL;
	CREATE UNIQUE INDEX UX_KnowledgeConfiguration_Tenant ON knowledge.Configuration(TenantId, ConfigurationCode) WHERE TenantId IS NOT NULL;
END;

IF OBJECT_ID(N'knowledge.ConceptScheme', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ConceptScheme
	(
		ConceptSchemeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ConceptScheme PRIMARY KEY,
		SchemeCode VARCHAR(100) NOT NULL,
		Name NVARCHAR(200) NOT NULL,
		Description NVARCHAR(MAX) NULL,
		AuthorityCode VARCHAR(100) NOT NULL,
		VersionLabel VARCHAR(50) NULL,
		StatusCode VARCHAR(30) NOT NULL,
		TenantId UNIQUEIDENTIFIER NULL,
		IsSystemDefined BIT NOT NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ConceptScheme_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_ConceptScheme_SystemCode ON knowledge.ConceptScheme(SchemeCode) WHERE TenantId IS NULL AND IsDeleted = 0;
	CREATE UNIQUE INDEX UX_ConceptScheme_TenantCode ON knowledge.ConceptScheme(TenantId, SchemeCode) WHERE TenantId IS NOT NULL AND IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.KnowledgeConcept', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.KnowledgeConcept
	(
		KnowledgeConceptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KnowledgeConcept PRIMARY KEY,
		ConceptSchemeId UNIQUEIDENTIFIER NOT NULL,
		ConceptCode VARCHAR(100) NOT NULL,
		ConceptTypeCode VARCHAR(50) NOT NULL,
		PreferredLabel NVARCHAR(250) NOT NULL,
		NormalizedPreferredLabel NVARCHAR(250) NOT NULL,
		Definition NVARCHAR(MAX) NULL,
		ParentConceptId UNIQUEIDENTIFIER NULL,
		IsAbstract BIT NOT NULL CONSTRAINT DF_KnowledgeConcept_Abstract DEFAULT 0,
		IsSelectable BIT NOT NULL CONSTRAINT DF_KnowledgeConcept_Selectable DEFAULT 1,
		StatusCode VARCHAR(30) NOT NULL,
		EffectiveFromUtc DATETIME2(7) NOT NULL,
		EffectiveToUtc DATETIME2(7) NULL,
		VersionNumber INT NOT NULL,
		SupersedesConceptId UNIQUEIDENTIFIER NULL,
		TenantId UNIQUEIDENTIFIER NULL,
		IsSystemDefined BIT NOT NULL,
		OwnerUserId UNIQUEIDENTIFIER NOT NULL,
		BusinessStewardUserId UNIQUEIDENTIFIER NOT NULL,
		TechnicalStewardUserId UNIQUEIDENTIFIER NULL,
		DefinitionSource NVARCHAR(500) NOT NULL,
		LicensingNotes NVARCHAR(2000) NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_KnowledgeConcept_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_KnowledgeConcept_Version CHECK (VersionNumber >= 1),
		CONSTRAINT CK_KnowledgeConcept_Effective CHECK (EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc),
		CONSTRAINT FK_KnowledgeConcept_Scheme FOREIGN KEY (ConceptSchemeId) REFERENCES knowledge.ConceptScheme(ConceptSchemeId),
		CONSTRAINT FK_KnowledgeConcept_Parent FOREIGN KEY (ParentConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId),
		CONSTRAINT FK_KnowledgeConcept_Supersedes FOREIGN KEY (SupersedesConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId),
		CONSTRAINT UQ_KnowledgeConcept_CodeVersion UNIQUE (ConceptSchemeId, ConceptCode, VersionNumber)
	);
	CREATE INDEX IX_KnowledgeConcept_Search ON knowledge.KnowledgeConcept(TenantId, ConceptSchemeId, StatusCode, ConceptTypeCode, NormalizedPreferredLabel) INCLUDE (ConceptCode, PreferredLabel, VersionNumber, IsSelectable) WHERE IsDeleted = 0;
	CREATE INDEX IX_KnowledgeConcept_Parent ON knowledge.KnowledgeConcept(ParentConceptId) WHERE ParentConceptId IS NOT NULL AND IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.ConceptLabel', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ConceptLabel
	(
		ConceptLabelId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ConceptLabel PRIMARY KEY,
		KnowledgeConceptId UNIQUEIDENTIFIER NOT NULL,
		Label NVARCHAR(250) NOT NULL,
		NormalizedLabel NVARCHAR(250) NOT NULL,
		LabelTypeCode VARCHAR(30) NOT NULL,
		LanguageCode VARCHAR(10) NOT NULL,
		Source NVARCHAR(100) NULL,
		IsSearchable BIT NOT NULL CONSTRAINT DF_ConceptLabel_Searchable DEFAULT 1,
		IsDeprecated BIT NOT NULL CONSTRAINT DF_ConceptLabel_Deprecated DEFAULT 0,
		TenantId UNIQUEIDENTIFIER NULL,
		IsSystemDefined BIT NOT NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ConceptLabel_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_ConceptLabel_Concept FOREIGN KEY (KnowledgeConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId)
	);
	CREATE UNIQUE INDEX UX_ConceptLabel_Active ON knowledge.ConceptLabel(KnowledgeConceptId, LanguageCode, NormalizedLabel) WHERE IsDeleted = 0 AND IsDeprecated = 0;
	CREATE INDEX IX_ConceptLabel_Resolution ON knowledge.ConceptLabel(TenantId, NormalizedLabel, LabelTypeCode) INCLUDE (KnowledgeConceptId, Label, LanguageCode) WHERE IsDeleted = 0 AND IsDeprecated = 0 AND IsSearchable = 1;
END;

IF OBJECT_ID(N'knowledge.RelationshipPredicate', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.RelationshipPredicate
	(
		PredicateCode VARCHAR(100) NOT NULL CONSTRAINT PK_RelationshipPredicate PRIMARY KEY,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(1000) NULL,
		IsHierarchical BIT NOT NULL,
		SubjectIsChild BIT NOT NULL,
		InversePredicateCode VARCHAR(100) NULL,
		IsSystemDefined BIT NOT NULL CONSTRAINT DF_RelationshipPredicate_System DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_RelationshipPredicate_Active DEFAULT 1,
		ModifiedDateUtc DATETIME2(7) NULL
	);
END;

IF OBJECT_ID(N'knowledge.ConceptRelationship', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ConceptRelationship
	(
		ConceptRelationshipId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ConceptRelationship PRIMARY KEY,
		SubjectConceptId UNIQUEIDENTIFIER NOT NULL,
		PredicateCode VARCHAR(100) NOT NULL,
		ObjectConceptId UNIQUEIDENTIFIER NOT NULL,
		RelationshipStrength DECIMAL(5,4) NULL,
		Source NVARCHAR(100) NULL,
		EffectiveFromUtc DATETIME2(7) NOT NULL,
		EffectiveToUtc DATETIME2(7) NULL,
		StatusCode VARCHAR(30) NOT NULL,
		TenantId UNIQUEIDENTIFIER NULL,
		IsSystemDefined BIT NOT NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ConceptRelationship_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_ConceptRelationship_Distinct CHECK (SubjectConceptId <> ObjectConceptId),
		CONSTRAINT CK_ConceptRelationship_Strength CHECK (RelationshipStrength IS NULL OR RelationshipStrength BETWEEN 0 AND 1),
		CONSTRAINT CK_ConceptRelationship_Effective CHECK (EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc),
		CONSTRAINT FK_ConceptRelationship_Subject FOREIGN KEY (SubjectConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId),
		CONSTRAINT FK_ConceptRelationship_Object FOREIGN KEY (ObjectConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId),
		CONSTRAINT FK_ConceptRelationship_Predicate FOREIGN KEY (PredicateCode) REFERENCES knowledge.RelationshipPredicate(PredicateCode),
		CONSTRAINT UQ_ConceptRelationship UNIQUE (SubjectConceptId, PredicateCode, ObjectConceptId, EffectiveFromUtc)
	);
	CREATE INDEX IX_ConceptRelationship_Object ON knowledge.ConceptRelationship(ObjectConceptId, PredicateCode, StatusCode) WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.ConceptHierarchyClosure', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ConceptHierarchyClosure
	(
		AncestorConceptId UNIQUEIDENTIFIER NOT NULL,
		DescendantConceptId UNIQUEIDENTIFIER NOT NULL,
		Depth INT NOT NULL,
		PublicationId UNIQUEIDENTIFIER NULL,
		RefreshedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_ConceptHierarchyClosure_Refreshed DEFAULT SYSUTCDATETIME(),
		CONSTRAINT PK_ConceptHierarchyClosure PRIMARY KEY (AncestorConceptId, DescendantConceptId),
		CONSTRAINT CK_ConceptHierarchyClosure_Depth CHECK (Depth >= 0),
		CONSTRAINT FK_ConceptHierarchyClosure_Ancestor FOREIGN KEY (AncestorConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId),
		CONSTRAINT FK_ConceptHierarchyClosure_Descendant FOREIGN KEY (DescendantConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId)
	);
	CREATE INDEX IX_ConceptHierarchyClosure_Descendant ON knowledge.ConceptHierarchyClosure(DescendantConceptId, Depth, AncestorConceptId);
END;

IF OBJECT_ID(N'knowledge.ExternalConceptMapping', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ExternalConceptMapping
	(
		ExternalConceptMappingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExternalConceptMapping PRIMARY KEY,
		KnowledgeConceptId UNIQUEIDENTIFIER NOT NULL,
		SourceSystemTypeCode VARCHAR(50) NOT NULL,
		SourceSystemId UNIQUEIDENTIFIER NULL,
		ExternalCode NVARCHAR(150) NULL,
		ExternalValue NVARCHAR(500) NOT NULL,
		NormalizedExternalValue NVARCHAR(500) NOT NULL,
		ExternalPath NVARCHAR(500) NULL,
		MappingDirectionCode VARCHAR(20) NOT NULL,
		MatchTypeCode VARCHAR(30) NOT NULL,
		ConfidenceScore DECIMAL(5,4) NULL,
		StateCode CHAR(2) NULL,
		LineOfBusinessConceptId UNIQUEIDENTIFIER NULL,
		CarrierProductId UNIQUEIDENTIFIER NULL,
		EffectiveFromUtc DATETIME2(7) NOT NULL,
		EffectiveToUtc DATETIME2(7) NULL,
		IsApproved BIT NOT NULL CONSTRAINT DF_ExternalConceptMapping_Approved DEFAULT 0,
		ApprovedByUserId UNIQUEIDENTIFIER NULL,
		ApprovedDateUtc DATETIME2(7) NULL,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IsSystemDefined BIT NOT NULL CONSTRAINT DF_ExternalConceptMapping_System DEFAULT 0,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ExternalConceptMapping_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_ExternalConceptMapping_Input CHECK (ExternalCode IS NOT NULL OR LEN(ExternalValue) > 0),
		CONSTRAINT CK_ExternalConceptMapping_Confidence CHECK (ConfidenceScore IS NULL OR ConfidenceScore BETWEEN 0 AND 1),
		CONSTRAINT CK_ExternalConceptMapping_Effective CHECK (EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc),
		CONSTRAINT FK_ExternalConceptMapping_Concept FOREIGN KEY (KnowledgeConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId),
		CONSTRAINT FK_ExternalConceptMapping_Lob FOREIGN KEY (LineOfBusinessConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId)
	);
	CREATE INDEX IX_ExternalConceptMapping_ResolveCode ON knowledge.ExternalConceptMapping(TenantId, SourceSystemTypeCode, SourceSystemId, ExternalCode, EffectiveFromUtc) INCLUDE (KnowledgeConceptId, ConfidenceScore, MatchTypeCode, CarrierProductId, StateCode, LineOfBusinessConceptId) WHERE IsDeleted = 0 AND IsApproved = 1;
	CREATE INDEX IX_ExternalConceptMapping_ResolveValue ON knowledge.ExternalConceptMapping(TenantId, SourceSystemTypeCode, NormalizedExternalValue, EffectiveFromUtc) INCLUDE (KnowledgeConceptId, ConfidenceScore, MatchTypeCode) WHERE IsDeleted = 0 AND IsApproved = 1;
END;

IF OBJECT_ID(N'knowledge.MappingReview', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.MappingReview
	(
		MappingReviewId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MappingReview PRIMARY KEY,
		ExternalConceptMappingId UNIQUEIDENTIFIER NOT NULL,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		StatusCode VARCHAR(30) NOT NULL,
		RecommendationJson NVARCHAR(MAX) NULL,
		ReviewedByUserId UNIQUEIDENTIFIER NULL,
		ReviewedDateUtc DATETIME2(7) NULL,
		ReviewReason NVARCHAR(1000) NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_MappingReview_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_MappingReview_Mapping FOREIGN KEY (ExternalConceptMappingId) REFERENCES knowledge.ExternalConceptMapping(ExternalConceptMappingId)
	);
	CREATE INDEX IX_MappingReview_Queue ON knowledge.MappingReview(TenantId, StatusCode, CreatedDateUtc) WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.EntitySemanticTag', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.EntitySemanticTag
	(
		EntitySemanticTagId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EntitySemanticTag PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode VARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		KnowledgeConceptId UNIQUEIDENTIFIER NOT NULL,
		ConceptVersionNumber INT NOT NULL,
		TagSourceCode VARCHAR(30) NOT NULL,
		ConfidenceScore DECIMAL(5,4) NULL,
		IsVerified BIT NOT NULL CONSTRAINT DF_EntitySemanticTag_Verified DEFAULT 0,
		VerifiedByUserId UNIQUEIDENTIFIER NULL,
		VerifiedDateUtc DATETIME2(7) NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EntitySemanticTag_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_EntitySemanticTag_Confidence CHECK (ConfidenceScore IS NULL OR ConfidenceScore BETWEEN 0 AND 1),
		CONSTRAINT FK_EntitySemanticTag_Concept FOREIGN KEY (KnowledgeConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId)
	);
	CREATE UNIQUE INDEX UX_EntitySemanticTag_Active ON knowledge.EntitySemanticTag(TenantId, EntityTypeCode, EntityId, KnowledgeConceptId) WHERE IsDeleted = 0;
	CREATE INDEX IX_EntitySemanticTag_Concept ON knowledge.EntitySemanticTag(TenantId, KnowledgeConceptId, EntityTypeCode) INCLUDE (EntityId, IsVerified) WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.ConceptVersion', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ConceptVersion
	(
		ConceptVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ConceptVersion PRIMARY KEY,
		KnowledgeConceptId UNIQUEIDENTIFIER NOT NULL,
		VersionNumber INT NOT NULL,
		StatusCode VARCHAR(30) NOT NULL,
		SnapshotJson NVARCHAR(MAX) NOT NULL,
		ChangeReason NVARCHAR(1000) NOT NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		CONSTRAINT FK_ConceptVersion_Concept FOREIGN KEY (KnowledgeConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId),
		CONSTRAINT UQ_ConceptVersion UNIQUE (KnowledgeConceptId, VersionNumber)
	);
END;

IF OBJECT_ID(N'knowledge.ConceptValidationRule', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ConceptValidationRule
	(
		ConceptValidationRuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ConceptValidationRule PRIMARY KEY,
		AppliesToConceptId UNIQUEIDENTIFIER NOT NULL,
		RuleCode VARCHAR(100) NOT NULL,
		RuleTypeCode VARCHAR(50) NOT NULL,
		PropertyPath NVARCHAR(500) NULL,
		OperatorCode VARCHAR(50) NOT NULL,
		ExpectedValue NVARCHAR(MAX) NULL,
		MinimumCount INT NULL,
		MaximumCount INT NULL,
		SeverityCode VARCHAR(30) NOT NULL,
		Message NVARCHAR(1000) NOT NULL,
		EffectiveFromUtc DATETIME2(7) NOT NULL,
		EffectiveToUtc DATETIME2(7) NULL,
		StatusCode VARCHAR(30) NOT NULL,
		TenantId UNIQUEIDENTIFIER NULL,
		IsSystemDefined BIT NOT NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ConceptValidationRule_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_ConceptValidationRule_Count CHECK (MinimumCount IS NULL OR MaximumCount IS NULL OR MinimumCount <= MaximumCount),
		CONSTRAINT FK_ConceptValidationRule_Concept FOREIGN KEY (AppliesToConceptId) REFERENCES knowledge.KnowledgeConcept(KnowledgeConceptId)
	);
	CREATE UNIQUE INDEX UX_ConceptValidationRule_System ON knowledge.ConceptValidationRule(RuleCode) WHERE TenantId IS NULL AND IsDeleted = 0;
	CREATE UNIQUE INDEX UX_ConceptValidationRule_Tenant ON knowledge.ConceptValidationRule(TenantId, RuleCode) WHERE TenantId IS NOT NULL AND IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.ChangeRequest', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ChangeRequest
	(
		ChangeRequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KnowledgeChangeRequest PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NULL,
		IsSystemDefined BIT NOT NULL,
		EntityTypeCode VARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		ChangeTypeCode VARCHAR(50) NOT NULL,
		Reason NVARCHAR(1000) NOT NULL,
		ProposedChangeJson NVARCHAR(MAX) NOT NULL,
		DownstreamImpact NVARCHAR(2000) NOT NULL,
		StatusCode VARCHAR(30) NOT NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_KnowledgeChangeRequest_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE INDEX IX_KnowledgeChangeRequest_Queue ON knowledge.ChangeRequest(TenantId, StatusCode, CreatedDateUtc) WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.ChangeRequestApproval', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ChangeRequestApproval
	(
		ChangeRequestApprovalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ChangeRequestApproval PRIMARY KEY,
		ChangeRequestId UNIQUEIDENTIFIER NOT NULL,
		ApprovalRoleCode VARCHAR(100) NOT NULL,
		DecisionStatusCode VARCHAR(30) NOT NULL,
		DecisionReason NVARCHAR(1000) NULL,
		DecidedByUserId UNIQUEIDENTIFIER NULL,
		DecidedDateUtc DATETIME2(7) NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_ChangeRequestApproval_Created DEFAULT SYSUTCDATETIME(),
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_ChangeRequestApproval_Request FOREIGN KEY (ChangeRequestId) REFERENCES knowledge.ChangeRequest(ChangeRequestId),
		CONSTRAINT UQ_ChangeRequestApproval_Role UNIQUE (ChangeRequestId, ApprovalRoleCode)
	);
END;

IF OBJECT_ID(N'knowledge.Publication', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.Publication
	(
		PublicationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KnowledgePublication PRIMARY KEY,
		PublicationCode VARCHAR(100) NOT NULL,
		Name NVARCHAR(200) NOT NULL,
		VersionLabel VARCHAR(50) NOT NULL,
		StatusCode VARCHAR(30) NOT NULL,
		TenantId UNIQUEIDENTIFIER NULL,
		IsSystemDefined BIT NOT NULL,
		PublishedByUserId UNIQUEIDENTIFIER NULL,
		PublishedDateUtc DATETIME2(7) NULL,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_KnowledgePublication_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_KnowledgePublication_System ON knowledge.Publication(PublicationCode, VersionLabel) WHERE TenantId IS NULL AND IsDeleted = 0;
	CREATE UNIQUE INDEX UX_KnowledgePublication_Tenant ON knowledge.Publication(TenantId, PublicationCode, VersionLabel) WHERE TenantId IS NOT NULL AND IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.PublicationItem', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.PublicationItem
	(
		PublicationItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KnowledgePublicationItem PRIMARY KEY,
		PublicationId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode VARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		VersionNumber INT NOT NULL,
		SnapshotJson NVARCHAR(MAX) NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_KnowledgePublicationItem_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT FK_KnowledgePublicationItem_Publication FOREIGN KEY (PublicationId) REFERENCES knowledge.Publication(PublicationId),
		CONSTRAINT UQ_KnowledgePublicationItem UNIQUE (PublicationId, EntityTypeCode, EntityId, VersionNumber)
	);
END;

IF OBJECT_ID(N'knowledge.ImportJob', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ImportJob
	(
		ImportJobId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KnowledgeImportJob PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ImportTypeCode VARCHAR(50) NOT NULL,
		SourceFileName NVARCHAR(260) NOT NULL,
		StorageReference NVARCHAR(1000) NOT NULL,
		StatusCode VARCHAR(30) NOT NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		RecordsReceived INT NOT NULL CONSTRAINT DF_KnowledgeImportJob_Received DEFAULT 0,
		RecordsProcessed INT NOT NULL CONSTRAINT DF_KnowledgeImportJob_Processed DEFAULT 0,
		RecordsFailed INT NOT NULL CONSTRAINT DF_KnowledgeImportJob_Failed DEFAULT 0,
		ErrorMessage NVARCHAR(MAX) NULL,
		LeaseOwner NVARCHAR(200) NULL,
		LeaseExpiresDateUtc DATETIME2(7) NULL,
		RetryCount INT NOT NULL CONSTRAINT DF_KnowledgeImportJob_Retry DEFAULT 0,
		CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(7) NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_KnowledgeImportJob_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE INDEX IX_KnowledgeImportJob_Work ON knowledge.ImportJob(StatusCode, LeaseExpiresDateUtc, CreatedDateUtc) INCLUDE (TenantId, ImportTypeCode, RetryCount) WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'knowledge.ImportStagingRecord', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ImportStagingRecord
	(
		ImportStagingRecordId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ImportStagingRecord PRIMARY KEY DEFAULT NEWID(),
		ImportJobId UNIQUEIDENTIFIER NOT NULL,
		RecordNumber INT NOT NULL,
		SourceJson NVARCHAR(MAX) NOT NULL,
		NormalizedJson NVARCHAR(MAX) NULL,
		StatusCode VARCHAR(30) NOT NULL,
		TargetEntityId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_ImportStagingRecord_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT FK_ImportStagingRecord_Job FOREIGN KEY (ImportJobId) REFERENCES knowledge.ImportJob(ImportJobId),
		CONSTRAINT UQ_ImportStagingRecord_Number UNIQUE (ImportJobId, RecordNumber)
	);
END;

IF OBJECT_ID(N'knowledge.ImportValidationError', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.ImportValidationError
	(
		ImportValidationErrorId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ImportValidationError PRIMARY KEY DEFAULT NEWID(),
		ImportJobId UNIQUEIDENTIFIER NOT NULL,
		ImportStagingRecordId UNIQUEIDENTIFIER NULL,
		ErrorCode VARCHAR(100) NOT NULL,
		FieldName NVARCHAR(250) NULL,
		ErrorMessage NVARCHAR(1000) NOT NULL,
		SeverityCode VARCHAR(30) NOT NULL,
		CreatedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_ImportValidationError_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT FK_ImportValidationError_Job FOREIGN KEY (ImportJobId) REFERENCES knowledge.ImportJob(ImportJobId),
		CONSTRAINT FK_ImportValidationError_Record FOREIGN KEY (ImportStagingRecordId) REFERENCES knowledge.ImportStagingRecord(ImportStagingRecordId)
	);
	CREATE INDEX IX_ImportValidationError_Job ON knowledge.ImportValidationError(ImportJobId, SeverityCode, ErrorCode);
END;

IF OBJECT_ID(N'knowledge.SemanticOutboxMessage', N'U') IS NULL
BEGIN
	CREATE TABLE knowledge.SemanticOutboxMessage
	(
		SemanticOutboxMessageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SemanticOutboxMessage PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EventTypeCode VARCHAR(200) NOT NULL,
		AggregateTypeCode VARCHAR(100) NOT NULL,
		AggregateId UNIQUEIDENTIFIER NOT NULL,
		PayloadJson NVARCHAR(MAX) NOT NULL,
		StatusCode VARCHAR(30) NOT NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		OccurredDateUtc DATETIME2(7) NOT NULL,
		AvailableDateUtc DATETIME2(7) NOT NULL,
		ProcessedDateUtc DATETIME2(7) NULL,
		RetryCount INT NOT NULL CONSTRAINT DF_SemanticOutboxMessage_Retry DEFAULT 0,
		LeaseOwner NVARCHAR(200) NULL,
		LeaseExpiresDateUtc DATETIME2(7) NULL,
		LastError NVARCHAR(MAX) NULL,
		DeadLetterDateUtc DATETIME2(7) NULL,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE INDEX IX_SemanticOutboxMessage_Work ON knowledge.SemanticOutboxMessage(StatusCode, AvailableDateUtc, LeaseExpiresDateUtc, OccurredDateUtc) INCLUDE (TenantId, EventTypeCode, RetryCount);
END;

DECLARE @LookupTypes TABLE (LookupTypeCode VARCHAR(100) PRIMARY KEY, DisplayName NVARCHAR(200), Description NVARCHAR(1000));
INSERT INTO @LookupTypes VALUES
('AUTHORITY', N'Authority', N'Provenance authority for a scheme or concept.'),
('CHANGE_REQUEST_STATUS', N'Change request status', N'Governance workflow states.'),
('CONCEPT_STATUS', N'Concept status', N'Concept lifecycle states.'),
('CONCEPT_TYPE', N'Concept type', N'Canonical semantic concept classifications.'),
('IMPORT_STATUS', N'Import status', N'Knowledge import processing states.'),
('LABEL_TYPE', N'Label type', N'SKOS-style terminology classifications.'),
('MAPPING_DIRECTION', N'Mapping direction', N'External mapping directions.'),
('MAPPING_REVIEW_STATUS', N'Mapping review status', N'Human mapping review workflow states.'),
('MATCH_TYPE', N'Match type', N'Deterministic or suggested match methods.'),
('OUTBOX_STATUS', N'Outbox status', N'Durable semantic event processing states.'),
('PUBLICATION_STATUS', N'Publication status', N'Knowledge publication lifecycle states.'),
('RULE_TYPE', N'Rule type', N'Relational semantic validation rule types.'),
('SCHEME_STATUS', N'Scheme status', N'Concept scheme lifecycle states.'),
('SEVERITY', N'Severity', N'Advisory and enforceable validation severities.'),
('SOURCE_SYSTEM_TYPE', N'Source system type', N'External terminology source classifications.'),
('TAG_SOURCE', N'Tag source', N'Semantic tag provenance classifications.');
MERGE knowledge.LookupType AS target
USING @LookupTypes AS source ON source.LookupTypeCode = target.LookupTypeCode
WHEN MATCHED AND target.IsSystemDefined = 1 THEN UPDATE SET DisplayName = source.DisplayName, Description = source.Description, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (LookupTypeCode, DisplayName, Description, IsSystemDefined, IsActive) VALUES (source.LookupTypeCode, source.DisplayName, source.Description, 1, 1);

DECLARE @LookupValues TABLE (LookupTypeCode VARCHAR(100), ValueCode VARCHAR(100), DisplayName NVARCHAR(200), Description NVARCHAR(1000), SortOrder INT, PRIMARY KEY (LookupTypeCode, ValueCode));
INSERT INTO @LookupValues VALUES
('AUTHORITY','AGENCYBINDER',N'AgencyBinder',N'AgencyBinder standard semantic authority.',10),('AUTHORITY','ACORD',N'ACORD',N'ACORD-informed content only where verified and licensed.',20),('AUTHORITY','ISO',N'ISO',N'Insurance Services Office reference.',30),('AUTHORITY','NAIC',N'NAIC',N'National Association of Insurance Commissioners reference.',40),('AUTHORITY','CARRIER',N'Carrier',N'Carrier-owned terminology.',50),('AUTHORITY','STATE_DOI',N'State DOI',N'State insurance regulator reference.',60),('AUTHORITY','TENANT',N'Tenant',N'Tenant-owned extension.',70),
('CONCEPT_STATUS','DRAFT',N'Draft',N'Editable working definition.',10),('CONCEPT_STATUS','UNDER_REVIEW',N'Under Review',N'Awaiting steward review.',20),('CONCEPT_STATUS','APPROVED',N'Approved',N'Approved for publication.',30),('CONCEPT_STATUS','PUBLISHED',N'Published',N'Authoritative published definition.',40),('CONCEPT_STATUS','DEPRECATED',N'Deprecated',N'Available for historical use only.',50),('CONCEPT_STATUS','RETIRED',N'Retired',N'No longer available for new use.',60),
('SCHEME_STATUS','DRAFT',N'Draft',N'Editable scheme.',10),('SCHEME_STATUS','PUBLISHED',N'Published',N'Published scheme.',20),('SCHEME_STATUS','RETIRED',N'Retired',N'Retired scheme.',30),
('CONCEPT_TYPE','ENTITY',N'Entity',NULL,10),('CONCEPT_TYPE','PARTY_ROLE',N'Party Role',NULL,20),('CONCEPT_TYPE','INSURANCE_PRODUCT',N'Insurance Product',NULL,30),('CONCEPT_TYPE','LINE_OF_BUSINESS',N'Line of Business',NULL,40),('CONCEPT_TYPE','COVERAGE',N'Coverage',NULL,50),('CONCEPT_TYPE','PERIL',N'Peril',NULL,60),('CONCEPT_TYPE','EXPOSURE',N'Exposure',NULL,70),('CONCEPT_TYPE','ASSET_TYPE',N'Asset Type',NULL,80),('CONCEPT_TYPE','DOCUMENT_TYPE',N'Document Type',NULL,90),('CONCEPT_TYPE','TRANSACTION_TYPE',N'Transaction Type',NULL,100),('CONCEPT_TYPE','STATUS',N'Status',NULL,110),('CONCEPT_TYPE','WORKFLOW_ACTION',N'Workflow Action',NULL,120),('CONCEPT_TYPE','FINANCIAL_CONCEPT',N'Financial Concept',NULL,130),('CONCEPT_TYPE','REGULATORY_CONCEPT',N'Regulatory Concept',NULL,140),
('LABEL_TYPE','PREFERRED',N'Preferred',NULL,10),('LABEL_TYPE','ALTERNATIVE',N'Alternative',NULL,20),('LABEL_TYPE','ABBREVIATION',N'Abbreviation',NULL,30),('LABEL_TYPE','CARRIER_TERM',N'Carrier Term',NULL,40),('LABEL_TYPE','LEGACY_TERM',N'Legacy Term',NULL,50),('LABEL_TYPE','MISSPELLING',N'Misspelling',NULL,60),('LABEL_TYPE','TECHNICAL',N'Technical',NULL,70),('LABEL_TYPE','DISPLAY',N'Display',NULL,80),('LABEL_TYPE','DEPRECATED',N'Deprecated',NULL,90),
('MAPPING_DIRECTION','INBOUND',N'Inbound',NULL,10),('MAPPING_DIRECTION','OUTBOUND',N'Outbound',NULL,20),('MAPPING_DIRECTION','BIDIRECTIONAL',N'Inbound and Outbound',NULL,30),
('MATCH_TYPE','EXACT_EXTERNAL_CODE',N'Exact External Code',NULL,10),('MATCH_TYPE','EXACT_PREFERRED_LABEL',N'Exact Preferred Label',NULL,20),('MATCH_TYPE','EXACT_APPROVED_SYNONYM',N'Exact Approved Synonym',NULL,30),('MATCH_TYPE','CONTEXT_CARRIER_TERM',N'Context-qualified Carrier Term',NULL,40),('MATCH_TYPE','FUZZY',N'Fuzzy',NULL,50),('MATCH_TYPE','AI_SUGGESTED',N'AI Suggested',NULL,60),
('MAPPING_REVIEW_STATUS','PENDING',N'Pending',NULL,10),('MAPPING_REVIEW_STATUS','APPROVED',N'Approved',NULL,20),('MAPPING_REVIEW_STATUS','REJECTED',N'Rejected',NULL,30),('MAPPING_REVIEW_STATUS','NEEDS_INFORMATION',N'Needs Information',NULL,40),
('IMPORT_STATUS','QUEUED',N'Queued',NULL,10),('IMPORT_STATUS','PROCESSING',N'Processing',NULL,20),('IMPORT_STATUS','COMPLETED',N'Completed',NULL,30),('IMPORT_STATUS','COMPLETED_WITH_ERRORS',N'Completed with Errors',NULL,40),('IMPORT_STATUS','FAILED',N'Failed',NULL,50),('IMPORT_STATUS','CANCELLED',N'Cancelled',NULL,60),
('PUBLICATION_STATUS','DRAFT',N'Draft',NULL,10),('PUBLICATION_STATUS','UNDER_REVIEW',N'Under Review',NULL,20),('PUBLICATION_STATUS','APPROVED',N'Approved',NULL,30),('PUBLICATION_STATUS','PUBLISHED',N'Published',NULL,40),('PUBLICATION_STATUS','WITHDRAWN',N'Withdrawn',NULL,50),
('CHANGE_REQUEST_STATUS','DRAFT',N'Draft',NULL,10),('CHANGE_REQUEST_STATUS','SUBMITTED',N'Submitted',NULL,20),('CHANGE_REQUEST_STATUS','UNDER_REVIEW',N'Under Review',NULL,30),('CHANGE_REQUEST_STATUS','APPROVED',N'Approved',NULL,40),('CHANGE_REQUEST_STATUS','REJECTED',N'Rejected',NULL,50),('CHANGE_REQUEST_STATUS','IMPLEMENTED',N'Implemented',NULL,60),
('RULE_TYPE','REQUIREDPROPERTY',N'Required Property',NULL,10),('RULE_TYPE','MINIMUMCOUNT',N'Minimum Count',NULL,20),('RULE_TYPE','MAXIMUMCOUNT',N'Maximum Count',NULL,30),('RULE_TYPE','ALLOWEDVALUE',N'Allowed Value',NULL,40),('RULE_TYPE','PROHIBITEDVALUE',N'Prohibited Value',NULL,50),('RULE_TYPE','RELATIONSHIPREQUIRED',N'Relationship Required',NULL,60),('RULE_TYPE','DATECONSTRAINT',N'Date Constraint',NULL,70),('RULE_TYPE','NUMERICRANGE',N'Numeric Range',NULL,80),('RULE_TYPE','DOCUMENTREQUIRED',N'Document Required',NULL,90),('RULE_TYPE','ROLEREQUIRED',N'Role Required',NULL,100),
('SEVERITY','WARNING',N'Warning',N'Advisory finding.',10),('SEVERITY','SOFT_BLOCKER',N'Soft Blocker',N'Requires acknowledgment or configured override.',20),('SEVERITY','HARD_BLOCKER',N'Hard Blocker',N'Enforceable only when explicitly enabled.',30),
('TAG_SOURCE','SYSTEM',N'System',NULL,10),('TAG_SOURCE','USER',N'User',NULL,20),('TAG_SOURCE','IMPORT',N'Import',NULL,30),('TAG_SOURCE','CARRIER',N'Carrier',NULL,40),('TAG_SOURCE','OCR',N'OCR',NULL,50),('TAG_SOURCE','AI',N'AI',NULL,60),('TAG_SOURCE','RULE',N'Rule',NULL,70),('TAG_SOURCE','MIGRATION',N'Migration',NULL,80),
('SOURCE_SYSTEM_TYPE','CARRIER_API',N'Carrier API',NULL,10),('SOURCE_SYSTEM_TYPE','CARRIER_DOWNLOAD',N'Carrier Download',NULL,20),('SOURCE_SYSTEM_TYPE','LEGACY_AMS',N'Legacy AMS',NULL,30),('SOURCE_SYSTEM_TYPE','DOCUMENT',N'Document',NULL,40),('SOURCE_SYSTEM_TYPE','TENANT',N'Tenant',NULL,50),
('OUTBOX_STATUS','PENDING',N'Pending',NULL,10),('OUTBOX_STATUS','PROCESSING',N'Processing',NULL,20),('OUTBOX_STATUS','COMPLETED',N'Completed',NULL,30),('OUTBOX_STATUS','RETRY',N'Retry',NULL,40),('OUTBOX_STATUS','DEAD_LETTER',N'Dead Letter',NULL,50);
MERGE knowledge.LookupValue AS target
USING @LookupValues AS source ON target.TenantId IS NULL AND target.LookupTypeCode = source.LookupTypeCode AND target.ValueCode = source.ValueCode
WHEN MATCHED AND target.IsSystemDefined = 1 THEN UPDATE SET DisplayName = source.DisplayName, Description = source.Description, SortOrder = source.SortOrder, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (LookupValueId, LookupTypeCode, ValueCode, DisplayName, Description, SortOrder, TenantId, IsSystemDefined, IsActive) VALUES (NEWID(), source.LookupTypeCode, source.ValueCode, source.DisplayName, source.Description, source.SortOrder, NULL, 1, 1);

DECLARE @Predicates TABLE (PredicateCode VARCHAR(100) PRIMARY KEY, DisplayName NVARCHAR(200), Description NVARCHAR(1000), IsHierarchical BIT, SubjectIsChild BIT, InversePredicateCode VARCHAR(100));
INSERT INTO @Predicates VALUES
('IS_A',N'Is A',N'Subject is a narrower type of object.',1,1,'HAS_NARROWER'),('HAS_NARROWER',N'Has Narrower',N'Subject has object as a narrower concept.',1,0,'IS_A'),('PART_OF',N'Part Of',NULL,0,1,NULL),('RELATED_TO',N'Related To',NULL,0,1,'RELATED_TO'),('REQUIRES',N'Requires',NULL,0,1,NULL),('APPLIES_TO',N'Applies To',NULL,0,1,NULL),('COVERS',N'Covers',NULL,0,1,NULL),('EXCLUDES',N'Excludes',NULL,0,1,NULL),('TRIGGERS',N'Triggers',NULL,0,1,NULL),('REPLACED_BY',N'Replaced By',NULL,0,1,'SUPERSEDES'),('SUPERSEDES',N'Supersedes',NULL,0,1,'REPLACED_BY'),('MAPPED_TO',N'Mapped To',NULL,0,1,NULL),('RECOMMENDED_FOR',N'Recommended For',NULL,0,1,NULL),('INCOMPATIBLE_WITH',N'Incompatible With',NULL,0,1,'INCOMPATIBLE_WITH'),('EVIDENCED_BY',N'Evidenced By',NULL,0,1,NULL),('REGULATED_BY',N'Regulated By',NULL,0,1,NULL);
MERGE knowledge.RelationshipPredicate AS target
USING @Predicates AS source ON source.PredicateCode = target.PredicateCode
WHEN MATCHED AND target.IsSystemDefined = 1 THEN UPDATE SET DisplayName = source.DisplayName, Description = source.Description, IsHierarchical = source.IsHierarchical, SubjectIsChild = source.SubjectIsChild, InversePredicateCode = source.InversePredicateCode, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (PredicateCode, DisplayName, Description, IsHierarchical, SubjectIsChild, InversePredicateCode, IsSystemDefined, IsActive) VALUES (source.PredicateCode, source.DisplayName, source.Description, source.IsHierarchical, source.SubjectIsChild, source.InversePredicateCode, 1, 1);

DECLARE @Configurations TABLE (ConfigurationCode VARCHAR(150) PRIMARY KEY, ConfigurationValue NVARCHAR(MAX), DataTypeCode VARCHAR(30), Description NVARCHAR(1000));
INSERT INTO @Configurations VALUES
('FEATURE_ENABLED',N'false','BOOLEAN',N'Enables optional semantic enrichment integrations; administration remains available.'),
('RESOLUTION_AUTO_THRESHOLD',N'0.95','DECIMAL',N'Minimum confidence for deterministic automatic resolution.'),
('RESOLUTION_REVIEW_THRESHOLD',N'0.80','DECIMAL',N'Minimum confidence for presenting a review candidate.'),
('RESOLUTION_MAX_CANDIDATES',N'10','INTEGER',N'Maximum candidates returned by concept resolution.'),
('CONFIDENCE_EXACT_EXTERNAL_CODE',N'1.00','DECIMAL',N'Confidence assigned to an approved exact external-code match.'),
('CONFIDENCE_EXACT_PREFERRED_LABEL',N'0.98','DECIMAL',N'Confidence assigned to an exact preferred-label match.'),
('CONFIDENCE_EXACT_APPROVED_SYNONYM',N'0.95','DECIMAL',N'Confidence assigned to an exact approved synonym match.'),
('CONFIDENCE_CONTEXT_CARRIER_TERM',N'0.90','DECIMAL',N'Confidence assigned to a context-qualified approved carrier term.'),
('CONFIDENCE_FUZZY',N'0.80','DECIMAL',N'Base confidence assigned to a fuzzy candidate before review.'),
('VALIDATION_BLOCKING_SEVERITIES',N'["HARD_BLOCKER"]','JSON',N'Severities treated as blocking when semantic enforcement is enabled.'),
('WORKER_POLL_SECONDS',N'30','INTEGER',N'Semantic worker polling interval.'),
('WORKER_BATCH_SIZE',N'25','INTEGER',N'Maximum work items leased per polling cycle.'),
('WORKER_MAX_RETRIES',N'5','INTEGER',N'Maximum processing attempts before dead-lettering.'),
('WORKER_LEASE_SECONDS',N'120','INTEGER',N'Lease duration for semantic work items.');
MERGE knowledge.Configuration AS target
USING @Configurations AS source ON target.TenantId IS NULL AND target.ConfigurationCode = source.ConfigurationCode
WHEN MATCHED AND target.IsSystemDefined = 1 THEN UPDATE SET ConfigurationValue = source.ConfigurationValue, DataTypeCode = source.DataTypeCode, Description = source.Description, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (ConfigurationId, TenantId, ConfigurationCode, ConfigurationValue, DataTypeCode, Description, IsSystemDefined, IsActive) VALUES (NEWID(), NULL, source.ConfigurationCode, source.ConfigurationValue, source.DataTypeCode, source.Description, 1, 1);

IF OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
   AND OBJECT_ID(N'Master.PermissionAction', N'U') IS NOT NULL
   AND COL_LENGTH(N'IAM.Permission', N'PermissionName') IS NOT NULL
BEGIN
	DECLARE @PermissionTenantId UNIQUEIDENTIFIER = (SELECT TOP (1) TenantId FROM Core.Tenant WHERE IsDeleted = 0 ORDER BY CreatedDateUtc);
	DECLARE @ReadPermissionActionId INT =
	(
		SELECT TOP (1) PermissionActionId
		FROM Master.PermissionAction
		WHERE UPPER(ActionCode) IN (N'READ', N'VIEW') OR UPPER(ActionName) IN (N'READ', N'VIEW')
		ORDER BY CASE WHEN UPPER(ActionCode) = N'READ' OR UPPER(ActionName) = N'READ' THEN 0 ELSE 1 END, PermissionActionId
	);
	DECLARE @ManagePermissionActionId INT =
	(
		SELECT TOP (1) PermissionActionId
		FROM Master.PermissionAction
		WHERE UPPER(ActionCode) IN (N'MANAGE', N'WRITE') OR UPPER(ActionName) IN (N'MANAGE', N'WRITE')
		ORDER BY CASE WHEN UPPER(ActionCode) = N'MANAGE' OR UPPER(ActionName) = N'MANAGE' THEN 0 ELSE 1 END, PermissionActionId
	);
	SET @ManagePermissionActionId = COALESCE(@ManagePermissionActionId, @ReadPermissionActionId);
	IF @ReadPermissionActionId IS NULL OR @ManagePermissionActionId IS NULL
		THROW 51011, 'Knowledge migration could not resolve required Master.PermissionAction rows for Read/View and Manage/Write.', 1;

	DECLARE @Permissions TABLE (PermissionCode NVARCHAR(200) PRIMARY KEY, PermissionName NVARCHAR(200), ActionCode NVARCHAR(100), PermissionActionId INT, Description NVARCHAR(500));
	INSERT INTO @Permissions VALUES
	(N'Knowledge.Concepts.Read',N'Read Knowledge Concepts',N'Read',@ReadPermissionActionId,N'View concept schemes, concepts, labels, hierarchies, and relationships.'),
	(N'Knowledge.Concepts.Manage',N'Manage Knowledge Concepts',N'Manage',@ManagePermissionActionId,N'Create and govern concepts, labels, and relationships.'),
	(N'Knowledge.Mappings.Read',N'Read Knowledge Mappings',N'Read',@ReadPermissionActionId,N'View carrier and external terminology mappings.'),
	(N'Knowledge.Mappings.Manage',N'Manage Knowledge Mappings',N'Manage',@ManagePermissionActionId,N'Create and update external terminology mappings.'),
	(N'Knowledge.Mappings.Approve',N'Approve Knowledge Mappings',N'Approve',@ManagePermissionActionId,N'Approve or reject external terminology mappings.'),
	(N'Knowledge.Rules.Manage',N'Manage Knowledge Rules',N'Manage',@ManagePermissionActionId,N'Manage semantic validation rule metadata.'),
	(N'Knowledge.Publish',N'Publish Knowledge',N'Execute',@ManagePermissionActionId,N'Publish approved semantic versions.'),
	(N'Knowledge.Import',N'Import Knowledge',N'Execute',@ManagePermissionActionId,N'Import and export governed knowledge data.'),
	(N'Knowledge.Audit.Read',N'Read Knowledge Audit',N'Read',@ReadPermissionActionId,N'View semantic governance and audit history.');
	UPDATE target SET PermissionName = source.PermissionName, ResourceCode = N'Knowledge', ActionCode = source.ActionCode, PermissionActionId = COALESCE(source.PermissionActionId, target.PermissionActionId), ModuleCode = N'Knowledge', Description = source.Description, IsBuiltIn = 1, IsActive = 1, IsDeleted = 0
	FROM IAM.Permission target INNER JOIN @Permissions source ON source.PermissionCode = target.PermissionCode;
	INSERT INTO IAM.Permission (PermissionId, TenantId, PermissionCode, PermissionName, ResourceCode, ActionCode, PermissionActionId, ModuleCode, Description, IsBuiltIn, IsActive, CreatedDateUtc, IsDeleted)
	SELECT NEWID(), @PermissionTenantId, source.PermissionCode, source.PermissionName, N'Knowledge', source.ActionCode, source.PermissionActionId, N'Knowledge', source.Description, 1, 1, SYSUTCDATETIME(), 0
	FROM @Permissions source WHERE @PermissionTenantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM IAM.Permission target WHERE target.PermissionCode = source.PermissionCode);
END;
