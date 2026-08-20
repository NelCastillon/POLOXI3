SET XACT_ABORT ON;
GO

IF SCHEMA_ID(N'Search') IS NULL EXEC(N'CREATE SCHEMA Search');
GO

IF OBJECT_ID(N'Search.MatchAlgorithm',N'U') IS NULL
BEGIN
	CREATE TABLE Search.MatchAlgorithm
	(
		MatchAlgorithmId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_MatchAlgorithm PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		AlgorithmCode NVARCHAR(80) NOT NULL,
		DisplayName NVARCHAR(160) NOT NULL,
		AlgorithmKindCode NVARCHAR(40) NOT NULL,
		Description NVARCHAR(1000) NULL,
		ConfigurationJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Search_MatchAlgorithm_Config DEFAULT N'{}',
		IsActive BIT NOT NULL CONSTRAINT DF_Search_MatchAlgorithm_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_MatchAlgorithm_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_MatchAlgorithm_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Search_MatchAlgorithm_Kind CHECK(AlgorithmKindCode IN(N'EXACT',N'NORMALIZED',N'PHONETIC',N'EDIT_DISTANCE',N'FUZZY',N'SEMANTIC')),
		CONSTRAINT CK_Search_MatchAlgorithm_Config CHECK(ISJSON(ConfigurationJson)=1)
	);
	CREATE UNIQUE INDEX UX_Search_MatchAlgorithm_Global ON Search.MatchAlgorithm(AlgorithmCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Search_MatchAlgorithm_Tenant ON Search.MatchAlgorithm(TenantId,AlgorithmCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.MatchProfile',N'U') IS NULL
BEGIN
	CREATE TABLE Search.MatchProfile
	(
		MatchProfileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_MatchProfile PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		ProfileCode NVARCHAR(120) NOT NULL,
		EntityTypeCode NVARCHAR(80) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(1000) NULL,
		ExactThreshold DECIMAL(5,2) NOT NULL,
		StrongThreshold DECIMAL(5,2) NOT NULL,
		PossibleThreshold DECIMAL(5,2) NOT NULL,
		MaximumCandidates INT NOT NULL,
		AllowAutomaticLink BIT NOT NULL CONSTRAINT DF_Search_MatchProfile_AutoLink DEFAULT 0,
		RequiresReview BIT NOT NULL CONSTRAINT DF_Search_MatchProfile_Review DEFAULT 1,
		IsActive BIT NOT NULL CONSTRAINT DF_Search_MatchProfile_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_MatchProfile_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_MatchProfile_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Search_MatchProfile_Thresholds CHECK(ExactThreshold BETWEEN 0 AND 100 AND StrongThreshold BETWEEN 0 AND ExactThreshold AND PossibleThreshold BETWEEN 0 AND StrongThreshold),
		CONSTRAINT CK_Search_MatchProfile_Candidates CHECK(MaximumCandidates BETWEEN 1 AND 500),
		CONSTRAINT CK_Search_MatchProfile_NoAutoMerge CHECK(AllowAutomaticLink=0)
	);
	CREATE UNIQUE INDEX UX_Search_MatchProfile_Global ON Search.MatchProfile(ProfileCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Search_MatchProfile_Tenant ON Search.MatchProfile(TenantId,ProfileCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
	CREATE INDEX IX_Search_MatchProfile_Entity ON Search.MatchProfile(TenantId,EntityTypeCode,IsActive) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.MatchFieldRule',N'U') IS NULL
BEGIN
	CREATE TABLE Search.MatchFieldRule
	(
		MatchFieldRuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_MatchFieldRule PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		MatchProfileId UNIQUEIDENTIFIER NOT NULL,
		FieldCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(160) NOT NULL,
		MatchAlgorithmId UNIQUEIDENTIFIER NOT NULL,
		Weight DECIMAL(7,4) NOT NULL,
		MinimumSimilarity DECIMAL(5,2) NOT NULL,
		IsRequired BIT NOT NULL CONSTRAINT DF_Search_MatchFieldRule_Required DEFAULT 0,
		IsCriticalIdentifier BIT NOT NULL CONSTRAINT DF_Search_MatchFieldRule_Critical DEFAULT 0,
		ExactMatchOnly BIT NOT NULL CONSTRAINT DF_Search_MatchFieldRule_ExactOnly DEFAULT 0,
		IsSensitive BIT NOT NULL CONSTRAINT DF_Search_MatchFieldRule_Sensitive DEFAULT 0,
		SortOrder INT NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Search_MatchFieldRule_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_MatchFieldRule_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_MatchFieldRule_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Search_MatchFieldRule_Profile FOREIGN KEY(MatchProfileId) REFERENCES Search.MatchProfile(MatchProfileId),
		CONSTRAINT FK_Search_MatchFieldRule_Algorithm FOREIGN KEY(MatchAlgorithmId) REFERENCES Search.MatchAlgorithm(MatchAlgorithmId),
		CONSTRAINT CK_Search_MatchFieldRule_Weight CHECK(Weight>0 AND Weight<=100),
		CONSTRAINT CK_Search_MatchFieldRule_Similarity CHECK(MinimumSimilarity BETWEEN 0 AND 100),
		CONSTRAINT CK_Search_MatchFieldRule_Critical CHECK(IsCriticalIdentifier=0 OR ExactMatchOnly=1)
	);
	CREATE UNIQUE INDEX UX_Search_MatchFieldRule_Global ON Search.MatchFieldRule(MatchProfileId,FieldCode,MatchAlgorithmId) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Search_MatchFieldRule_Tenant ON Search.MatchFieldRule(TenantId,MatchProfileId,FieldCode,MatchAlgorithmId) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.NormalizationTerm',N'U') IS NULL
BEGIN
	CREATE TABLE Search.NormalizationTerm
	(
		NormalizationTermId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_NormalizationTerm PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		EntityTypeCode NVARCHAR(80) NOT NULL,
		FieldCode NVARCHAR(100) NOT NULL,
		SourceValue NVARCHAR(300) NOT NULL,
		NormalizedValue NVARCHAR(300) NOT NULL,
		TermKindCode NVARCHAR(40) NOT NULL,
		CultureCode NVARCHAR(20) NULL,
		SortOrder INT NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Search_NormalizationTerm_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_NormalizationTerm_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_NormalizationTerm_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Search_NormalizationTerm_Kind CHECK(TermKindCode IN(N'STOP_WORD',N'REPLACEMENT',N'ABBREVIATION',N'SYNONYM'))
	);
	CREATE UNIQUE INDEX UX_Search_NormalizationTerm_Global ON Search.NormalizationTerm(EntityTypeCode,FieldCode,SourceValue,CultureCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Search_NormalizationTerm_Tenant ON Search.NormalizationTerm(TenantId,EntityTypeCode,FieldCode,SourceValue,CultureCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.EntityProjection',N'U') IS NULL
BEGIN
	CREATE TABLE Search.EntityProjection
	(
		EntityProjectionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_EntityProjection PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(80) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		DisplayName NVARCHAR(500) NOT NULL,
		SecondaryText NVARCHAR(1000) NULL,
		NavigationRoute NVARCHAR(500) NULL,
		SourceSchemaName NVARCHAR(128) NOT NULL,
		SourceTableName NVARCHAR(128) NOT NULL,
		SourceModifiedDateUtc DATETIME2 NULL,
		SearchText NVARCHAR(MAX) NOT NULL,
		NormalizedFieldsJson NVARCHAR(MAX) NOT NULL,
		ExactIdentifiersJson NVARCHAR(MAX) NOT NULL,
		PermissionCode NVARCHAR(150) NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Search_EntityProjection_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_EntityProjection_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_EntityProjection_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Search_EntityProjection_Normalized CHECK(ISJSON(NormalizedFieldsJson)=1),
		CONSTRAINT CK_Search_EntityProjection_Identifiers CHECK(ISJSON(ExactIdentifiersJson)=1)
	);
	CREATE UNIQUE INDEX UX_Search_EntityProjection_Entity ON Search.EntityProjection(TenantId,EntityTypeCode,EntityId) WHERE IsDeleted=0;
	CREATE INDEX IX_Search_EntityProjection_Search ON Search.EntityProjection(TenantId,EntityTypeCode,IsActive,DisplayName) INCLUDE(EntityId,SecondaryText,NavigationRoute,PermissionCode,SourceModifiedDateUtc) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.MatchExecution',N'U') IS NULL
BEGIN
	CREATE TABLE Search.MatchExecution
	(
		MatchExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_MatchExecution PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		MatchProfileId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(80) NOT NULL,
		SourceEntityId UNIQUEIDENTIFIER NULL,
		CorrelationId NVARCHAR(200) NOT NULL,
		RequestHash VARBINARY(32) NOT NULL,
		StatusCode NVARCHAR(40) NOT NULL,
		CandidateCount INT NOT NULL CONSTRAINT DF_Search_MatchExecution_Candidates DEFAULT 0,
		RequestedByUserId UNIQUEIDENTIFIER NULL,
		StartedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_MatchExecution_Started DEFAULT SYSUTCDATETIME(),
		CompletedDateUtc DATETIME2 NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_MatchExecution_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_MatchExecution_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Search_MatchExecution_Profile FOREIGN KEY(MatchProfileId) REFERENCES Search.MatchProfile(MatchProfileId),
		CONSTRAINT CK_Search_MatchExecution_Status CHECK(StatusCode IN(N'RUNNING',N'COMPLETED',N'FAILED'))
	);
	CREATE UNIQUE INDEX UX_Search_MatchExecution_Correlation ON Search.MatchExecution(TenantId,CorrelationId) WHERE IsDeleted=0;
	CREATE INDEX IX_Search_MatchExecution_Entity ON Search.MatchExecution(TenantId,EntityTypeCode,SourceEntityId,StartedDateUtc DESC) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.MatchCandidate',N'U') IS NULL
BEGIN
	CREATE TABLE Search.MatchCandidate
	(
		MatchCandidateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_MatchCandidate PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		MatchExecutionId UNIQUEIDENTIFIER NOT NULL,
		CandidateEntityId UNIQUEIDENTIFIER NOT NULL,
		DisplayName NVARCHAR(500) NOT NULL,
		OverallScore DECIMAL(7,4) NOT NULL,
		ConfidenceBandCode NVARCHAR(40) NOT NULL,
		IsExactMatch BIT NOT NULL,
		RequiresReview BIT NOT NULL,
		RankOrder INT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_MatchCandidate_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_MatchCandidate_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Search_MatchCandidate_Execution FOREIGN KEY(MatchExecutionId) REFERENCES Search.MatchExecution(MatchExecutionId),
		CONSTRAINT CK_Search_MatchCandidate_Score CHECK(OverallScore BETWEEN 0 AND 100),
		CONSTRAINT CK_Search_MatchCandidate_Band CHECK(ConfidenceBandCode IN(N'EXACT',N'STRONG',N'POSSIBLE',N'BELOW_THRESHOLD'))
	);
	CREATE UNIQUE INDEX UX_Search_MatchCandidate_ExecutionEntity ON Search.MatchCandidate(MatchExecutionId,CandidateEntityId) WHERE IsDeleted=0;
	CREATE INDEX IX_Search_MatchCandidate_Rank ON Search.MatchCandidate(TenantId,MatchExecutionId,RankOrder) INCLUDE(CandidateEntityId,OverallScore,ConfidenceBandCode,RequiresReview) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.MatchReasonEvidence',N'U') IS NULL
BEGIN
	CREATE TABLE Search.MatchReasonEvidence
	(
		MatchReasonEvidenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_MatchReasonEvidence PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		MatchCandidateId UNIQUEIDENTIFIER NOT NULL,
		MatchFieldRuleId UNIQUEIDENTIFIER NOT NULL,
		FieldCode NVARCHAR(100) NOT NULL,
		AlgorithmCode NVARCHAR(80) NOT NULL,
		SimilarityScore DECIMAL(7,4) NOT NULL,
		WeightedScore DECIMAL(7,4) NOT NULL,
		ReasonCode NVARCHAR(80) NOT NULL,
		Explanation NVARCHAR(500) NOT NULL,
		IsExactMatch BIT NOT NULL,
		IsDiscrepancy BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_MatchReasonEvidence_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_MatchReasonEvidence_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Search_MatchReasonEvidence_Candidate FOREIGN KEY(MatchCandidateId) REFERENCES Search.MatchCandidate(MatchCandidateId),
		CONSTRAINT FK_Search_MatchReasonEvidence_Rule FOREIGN KEY(MatchFieldRuleId) REFERENCES Search.MatchFieldRule(MatchFieldRuleId),
		CONSTRAINT CK_Search_MatchReasonEvidence_Scores CHECK(SimilarityScore BETWEEN 0 AND 100 AND WeightedScore BETWEEN 0 AND 100)
	);
	CREATE INDEX IX_Search_MatchReasonEvidence_Candidate ON Search.MatchReasonEvidence(TenantId,MatchCandidateId) INCLUDE(FieldCode,AlgorithmCode,SimilarityScore,WeightedScore,ReasonCode,IsDiscrepancy) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.ProjectionCheckpoint',N'U') IS NULL
BEGIN
	CREATE TABLE Search.ProjectionCheckpoint
	(
		ProjectionCheckpointId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_ProjectionCheckpoint PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(80) NOT NULL,
		LastSourceModifiedDateUtc DATETIME2 NULL,
		LeaseOwner NVARCHAR(200) NULL,
		LeaseExpiresDateUtc DATETIME2 NULL,
		LastSuccessfulDateUtc DATETIME2 NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		RetryCount INT NOT NULL CONSTRAINT DF_Search_ProjectionCheckpoint_Retry DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_ProjectionCheckpoint_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_ProjectionCheckpoint_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_Search_ProjectionCheckpoint_Entity ON Search.ProjectionCheckpoint(TenantId,EntityTypeCode) WHERE IsDeleted=0;
END;
GO

SET XACT_ABORT ON;
GO

DECLARE @Algorithms TABLE(AlgorithmCode NVARCHAR(80),DisplayName NVARCHAR(160),AlgorithmKindCode NVARCHAR(40),Description NVARCHAR(1000),ConfigurationJson NVARCHAR(MAX));
INSERT @Algorithms VALUES
(N'EXACT',N'Exact Match',N'EXACT',N'Case-aware exact comparison for trusted identifiers and normalized values.',N'{"caseSensitive":false}'),
(N'NORMALIZED_EXACT',N'Normalized Exact Match',N'NORMALIZED',N'Exact comparison after configured normalization terms are applied.',N'{"removePunctuation":true,"collapseWhitespace":true,"removeDiacritics":true}'),
(N'SOUNDEX',N'Phonetic Soundex',N'PHONETIC',N'Phonetic similarity signal for person and business names; never decisive by itself.',N'{"maximumScore":100}'),
(N'DAMERAU_LEVENSHTEIN',N'Damerau-Levenshtein Similarity',N'EDIT_DISTANCE',N'Typo and transposition tolerant text similarity.',N'{"allowTransposition":true}'),
(N'TOKEN_JACCARD',N'Token Jaccard Similarity',N'FUZZY',N'Order-independent token overlap for names, addresses, and descriptive text.',N'{"distinctTokens":true}'),
(N'SEMANTIC_ADVISORY',N'Semantic Advisory Similarity',N'SEMANTIC',N'Optional Knowledge/Intelligence concept similarity; advisory and non-blocking.',N'{"required":false,"maximumContribution":15}');
MERGE Search.MatchAlgorithm target USING @Algorithms source ON target.TenantId IS NULL AND target.AlgorithmCode=source.AlgorithmCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,AlgorithmKindCode=source.AlgorithmKindCode,Description=source.Description,ConfigurationJson=source.ConfigurationJson,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(MatchAlgorithmId,TenantId,AlgorithmCode,DisplayName,AlgorithmKindCode,Description,ConfigurationJson,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.AlgorithmCode,source.DisplayName,source.AlgorithmKindCode,source.Description,source.ConfigurationJson,1,SYSUTCDATETIME(),0);

DECLARE @Profiles TABLE(ProfileCode NVARCHAR(120),EntityTypeCode NVARCHAR(80),DisplayName NVARCHAR(200),Description NVARCHAR(1000),ExactThreshold DECIMAL(5,2),StrongThreshold DECIMAL(5,2),PossibleThreshold DECIMAL(5,2),MaximumCandidates INT);
INSERT @Profiles VALUES
(N'LEAD_DUPLICATE',N'Lead',N'Lead Duplicate Matching',N'Matches leads by trusted contact values, normalized names, phone, email, and address.',95,85,70,25),
(N'ACCOUNT_DUPLICATE',N'Account',N'Account Duplicate Matching',N'Matches organizations and people before account creation.',95,85,70,25),
(N'CONTACT_DUPLICATE',N'Contact',N'Contact Duplicate Matching',N'Matches account contacts and named individuals.',95,85,70,25),
(N'SUBMISSION_ENTITY',N'Submission',N'Submission Entity Matching',N'Finds related submissions by account, submission number, line, and effective date.',95,85,70,25),
(N'POLICY_RECONCILIATION',N'Policy',N'Policy Reconciliation',N'Matches carrier and document policy data; policy number fuzzy results are warnings only.',98,90,75,25),
(N'CLAIM_RECONCILIATION',N'Claim',N'Claim Reconciliation',N'Matches claimants, policies, vehicles, and loss records; claim number is exact-only.',98,90,75,25),
(N'DOCUMENT_ROUTING',N'Document',N'Document-to-Record Routing',N'Ranks likely AMS records for OCR/document routing without automatic attachment.',95,85,70,50),
(N'ACCOUNTING_RECONCILIATION',N'Accounting',N'Accounting Reconciliation',N'Matches carrier statements, policy numbers, insured names, and premium values.',98,90,75,50),
(N'CERTIFICATE_PARTY',N'Certificate',N'Certificate Party Matching',N'Matches certificate holders and additional insureds.',95,85,70,25),
(N'CARRIER_NORMALIZATION',N'Carrier',N'Carrier Name Normalization',N'Normalizes imported carrier names and codes against authoritative carriers.',95,85,70,25),
(N'GLOBAL_ENTERPRISE_SEARCH',N'Global',N'Global Enterprise Search',N'Typo-tolerant permission-scoped search across projected AMS modules.',95,80,60,50);
MERGE Search.MatchProfile target USING @Profiles source ON target.TenantId IS NULL AND target.ProfileCode=source.ProfileCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET EntityTypeCode=source.EntityTypeCode,DisplayName=source.DisplayName,Description=source.Description,ExactThreshold=source.ExactThreshold,StrongThreshold=source.StrongThreshold,PossibleThreshold=source.PossibleThreshold,MaximumCandidates=source.MaximumCandidates,AllowAutomaticLink=0,RequiresReview=1,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(MatchProfileId,TenantId,ProfileCode,EntityTypeCode,DisplayName,Description,ExactThreshold,StrongThreshold,PossibleThreshold,MaximumCandidates,AllowAutomaticLink,RequiresReview,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.ProfileCode,source.EntityTypeCode,source.DisplayName,source.Description,source.ExactThreshold,source.StrongThreshold,source.PossibleThreshold,source.MaximumCandidates,0,1,1,SYSUTCDATETIME(),0);

DECLARE @Rules TABLE(ProfileCode NVARCHAR(120),FieldCode NVARCHAR(100),DisplayName NVARCHAR(160),AlgorithmCode NVARCHAR(80),Weight DECIMAL(7,4),MinimumSimilarity DECIMAL(5,2),IsRequired BIT,IsCriticalIdentifier BIT,ExactMatchOnly BIT,IsSensitive BIT,SortOrder INT);
INSERT @Rules VALUES
(N'ACCOUNT_DUPLICATE',N'Fein',N'FEIN',N'EXACT',45,100,0,1,1,1,10),(N'ACCOUNT_DUPLICATE',N'BusinessName',N'Legal Name',N'NORMALIZED_EXACT',20,100,1,0,0,0,20),(N'ACCOUNT_DUPLICATE',N'BusinessName',N'Legal Name Typo Similarity',N'DAMERAU_LEVENSHTEIN',10,70,1,0,0,0,21),(N'ACCOUNT_DUPLICATE',N'BusinessName',N'Legal Name Phonetic Similarity',N'SOUNDEX',5,100,0,0,0,0,22),(N'ACCOUNT_DUPLICATE',N'Address',N'Address',N'TOKEN_JACCARD',15,60,0,0,0,0,30),(N'ACCOUNT_DUPLICATE',N'Phone',N'Phone',N'NORMALIZED_EXACT',10,100,0,0,0,0,40),(N'ACCOUNT_DUPLICATE',N'EmailDomain',N'Email Domain',N'NORMALIZED_EXACT',5,100,0,0,0,0,50),
(N'LEAD_DUPLICATE',N'Email',N'Email',N'NORMALIZED_EXACT',30,100,0,0,0,0,10),(N'LEAD_DUPLICATE',N'Phone',N'Phone',N'NORMALIZED_EXACT',25,100,0,0,0,0,20),(N'LEAD_DUPLICATE',N'FullName',N'Full Name',N'DAMERAU_LEVENSHTEIN',20,75,1,0,0,0,30),(N'LEAD_DUPLICATE',N'FullName',N'Full Name Phonetic',N'SOUNDEX',10,100,0,0,0,0,31),(N'LEAD_DUPLICATE',N'CompanyName',N'Company',N'TOKEN_JACCARD',10,60,0,0,0,0,40),(N'LEAD_DUPLICATE',N'Address',N'Address',N'TOKEN_JACCARD',5,60,0,0,0,0,50),
(N'CONTACT_DUPLICATE',N'Email',N'Email',N'NORMALIZED_EXACT',30,100,0,0,0,0,10),(N'CONTACT_DUPLICATE',N'Phone',N'Phone',N'NORMALIZED_EXACT',25,100,0,0,0,0,20),(N'CONTACT_DUPLICATE',N'FullName',N'Full Name',N'DAMERAU_LEVENSHTEIN',25,75,1,0,0,0,30),(N'CONTACT_DUPLICATE',N'FullName',N'Full Name Phonetic',N'SOUNDEX',10,100,0,0,0,0,31),(N'CONTACT_DUPLICATE',N'Address',N'Address',N'TOKEN_JACCARD',10,60,0,0,0,0,40),
(N'SUBMISSION_ENTITY',N'SubmissionNumber',N'Submission Number',N'EXACT',40,100,0,1,1,0,10),(N'SUBMISSION_ENTITY',N'AccountId',N'Account',N'EXACT',30,100,1,1,1,0,20),(N'SUBMISSION_ENTITY',N'LineOfBusiness',N'Line of Business',N'NORMALIZED_EXACT',15,100,0,0,0,0,30),(N'SUBMISSION_ENTITY',N'EffectiveDate',N'Effective Date',N'EXACT',15,100,0,0,0,0,40),
(N'POLICY_RECONCILIATION',N'PolicyNumber',N'Policy Number',N'EXACT',45,100,0,1,1,0,10),(N'POLICY_RECONCILIATION',N'PolicyNumber',N'Policy Number Discrepancy',N'DAMERAU_LEVENSHTEIN',5,85,0,1,1,0,11),(N'POLICY_RECONCILIATION',N'CarrierId',N'Carrier',N'EXACT',20,100,1,1,1,0,20),(N'POLICY_RECONCILIATION',N'NamedInsured',N'Named Insured',N'TOKEN_JACCARD',20,70,1,0,0,0,30),(N'POLICY_RECONCILIATION',N'EffectiveDate',N'Effective Date',N'EXACT',10,100,0,0,0,0,40),
(N'CLAIM_RECONCILIATION',N'ClaimNumber',N'Claim Number',N'EXACT',40,100,0,1,1,0,10),(N'CLAIM_RECONCILIATION',N'PolicyNumber',N'Policy Number',N'EXACT',30,100,0,1,1,0,20),(N'CLAIM_RECONCILIATION',N'ClaimantName',N'Claimant Name',N'DAMERAU_LEVENSHTEIN',15,75,0,0,0,0,30),(N'CLAIM_RECONCILIATION',N'LossDate',N'Loss Date',N'EXACT',15,100,0,0,0,0,40),
(N'DOCUMENT_ROUTING',N'PolicyNumber',N'Policy Number',N'EXACT',30,100,0,1,1,0,10),(N'DOCUMENT_ROUTING',N'AccountName',N'Account Name',N'TOKEN_JACCARD',25,60,0,0,0,0,20),(N'DOCUMENT_ROUTING',N'CarrierName',N'Carrier Name',N'DAMERAU_LEVENSHTEIN',15,75,0,0,0,0,30),(N'DOCUMENT_ROUTING',N'DocumentText',N'Document Text',N'SEMANTIC_ADVISORY',15,60,0,0,0,0,40),(N'DOCUMENT_ROUTING',N'EffectiveDate',N'Effective Date',N'EXACT',15,100,0,0,0,0,50),
(N'ACCOUNTING_RECONCILIATION',N'PolicyNumber',N'Policy Number',N'EXACT',40,100,0,1,1,0,10),(N'ACCOUNTING_RECONCILIATION',N'CarrierId',N'Carrier',N'EXACT',25,100,1,1,1,0,20),(N'ACCOUNTING_RECONCILIATION',N'InsuredName',N'Insured Name',N'TOKEN_JACCARD',20,65,0,0,0,0,30),(N'ACCOUNTING_RECONCILIATION',N'PremiumAmount',N'Premium Amount',N'EXACT',15,100,0,0,0,0,40),
(N'CERTIFICATE_PARTY',N'PartyName',N'Party Name',N'DAMERAU_LEVENSHTEIN',35,75,1,0,0,0,10),(N'CERTIFICATE_PARTY',N'PartyName',N'Party Name Phonetic',N'SOUNDEX',10,100,0,0,0,0,11),(N'CERTIFICATE_PARTY',N'Address',N'Address',N'TOKEN_JACCARD',30,60,0,0,0,0,20),(N'CERTIFICATE_PARTY',N'Email',N'Email',N'NORMALIZED_EXACT',15,100,0,0,0,0,30),(N'CERTIFICATE_PARTY',N'Phone',N'Phone',N'NORMALIZED_EXACT',10,100,0,0,0,0,40),
(N'CARRIER_NORMALIZATION',N'CarrierCode',N'Carrier Code',N'EXACT',45,100,0,1,1,0,10),(N'CARRIER_NORMALIZATION',N'CarrierName',N'Carrier Name',N'NORMALIZED_EXACT',25,100,1,0,0,0,20),(N'CARRIER_NORMALIZATION',N'CarrierName',N'Carrier Name Typo Similarity',N'DAMERAU_LEVENSHTEIN',20,75,1,0,0,0,21),(N'CARRIER_NORMALIZATION',N'CarrierName',N'Carrier Name Phonetic',N'SOUNDEX',10,100,0,0,0,0,22),
(N'GLOBAL_ENTERPRISE_SEARCH',N'DisplayName',N'Display Name',N'DAMERAU_LEVENSHTEIN',35,60,1,0,0,0,10),(N'GLOBAL_ENTERPRISE_SEARCH',N'DisplayName',N'Display Name Tokens',N'TOKEN_JACCARD',30,40,1,0,0,0,11),(N'GLOBAL_ENTERPRISE_SEARCH',N'SearchText',N'Search Text',N'TOKEN_JACCARD',20,30,0,0,0,0,20),(N'GLOBAL_ENTERPRISE_SEARCH',N'SearchText',N'Semantic Search',N'SEMANTIC_ADVISORY',15,50,0,0,0,0,30);
MERGE Search.MatchFieldRule target USING
(
	SELECT profile.MatchProfileId,fieldRule.* ,algorithm.MatchAlgorithmId
	FROM @Rules fieldRule
	JOIN Search.MatchProfile profile ON profile.TenantId IS NULL AND profile.ProfileCode=fieldRule.ProfileCode AND profile.IsDeleted=0
	JOIN Search.MatchAlgorithm algorithm ON algorithm.TenantId IS NULL AND algorithm.AlgorithmCode=fieldRule.AlgorithmCode AND algorithm.IsDeleted=0
) source ON target.TenantId IS NULL AND target.MatchProfileId=source.MatchProfileId AND target.FieldCode=source.FieldCode AND target.MatchAlgorithmId=source.MatchAlgorithmId AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Weight=source.Weight,MinimumSimilarity=source.MinimumSimilarity,IsRequired=source.IsRequired,IsCriticalIdentifier=source.IsCriticalIdentifier,ExactMatchOnly=source.ExactMatchOnly,IsSensitive=source.IsSensitive,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(MatchFieldRuleId,TenantId,MatchProfileId,FieldCode,DisplayName,MatchAlgorithmId,Weight,MinimumSimilarity,IsRequired,IsCriticalIdentifier,ExactMatchOnly,IsSensitive,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.MatchProfileId,source.FieldCode,source.DisplayName,source.MatchAlgorithmId,source.Weight,source.MinimumSimilarity,source.IsRequired,source.IsCriticalIdentifier,source.ExactMatchOnly,source.IsSensitive,source.SortOrder,1,SYSUTCDATETIME(),0);

DECLARE @Terms TABLE(EntityTypeCode NVARCHAR(80),FieldCode NVARCHAR(100),SourceValue NVARCHAR(300),NormalizedValue NVARCHAR(300),TermKindCode NVARCHAR(40),CultureCode NVARCHAR(20),SortOrder INT);
INSERT @Terms VALUES
(N'Account',N'BusinessName',N'inc',N'',N'STOP_WORD',N'en-US',10),(N'Account',N'BusinessName',N'incorporated',N'',N'STOP_WORD',N'en-US',20),(N'Account',N'BusinessName',N'llc',N'',N'STOP_WORD',N'en-US',30),(N'Account',N'BusinessName',N'ltd',N'',N'STOP_WORD',N'en-US',40),(N'Account',N'BusinessName',N'corporation',N'',N'STOP_WORD',N'en-US',50),(N'Account',N'BusinessName',N'company',N'',N'STOP_WORD',N'en-US',60),(N'Account',N'BusinessName',N'mfg',N'manufacturing',N'ABBREVIATION',N'en-US',70),(N'Global',N'Address',N'st',N'street',N'ABBREVIATION',N'en-US',10),(N'Global',N'Address',N'rd',N'road',N'ABBREVIATION',N'en-US',20),(N'Global',N'Address',N'ave',N'avenue',N'ABBREVIATION',N'en-US',30),(N'Global',N'Address',N'blvd',N'boulevard',N'ABBREVIATION',N'en-US',40);
MERGE Search.NormalizationTerm target USING @Terms source ON target.TenantId IS NULL AND target.EntityTypeCode=source.EntityTypeCode AND target.FieldCode=source.FieldCode AND target.SourceValue=source.SourceValue AND ISNULL(target.CultureCode,N'')=ISNULL(source.CultureCode,N'') AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET NormalizedValue=source.NormalizedValue,TermKindCode=source.TermKindCode,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(NormalizationTermId,TenantId,EntityTypeCode,FieldCode,SourceValue,NormalizedValue,TermKindCode,CultureCode,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.EntityTypeCode,source.FieldCode,source.SourceValue,source.NormalizedValue,source.TermKindCode,source.CultureCode,source.SortOrder,1,SYSUTCDATETIME(),0);

UPDATE Platform.ServiceCatalog SET DisplayName=N'Search & Matching Platform',Description=N'Permission-aware exact, full-text, phonetic, edit-distance, fuzzy, semantic, duplicate-detection, entity-resolution, ranking, and explainability services.',ContractReference=N'IEntityMatchingService; ISearchMatchingRepository; IIntelligenceService.SearchAsync',AdministrationRoute=N'/intelligence/search',MaturityCode=N'EXECUTABLE',ImplementationStatusCode=N'IMPLEMENTED',ImplementationNotes=N'Database-backed tenant-aware profiles, field weights, normalization, projections, execution evidence, and review-safe scoring extend enterprise and semantic search.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND ServiceCode=N'SEARCH' AND IsDeleted=0;

UPDATE dependency SET AdoptionStatusCode=N'VERIFIED',ConsumerReference=N'IEntityMatchingService and Search.EntityProjection',LastVerifiedDateUtc=SYSUTCDATETIME(),IsRequired=1,ModifiedDateUtc=SYSUTCDATETIME()
FROM Platform.ModuleServiceDependency dependency JOIN Platform.ServiceCatalog service ON service.PlatformServiceId=dependency.PlatformServiceId AND service.ServiceCode=N'SEARCH' AND service.IsDeleted=0
WHERE dependency.TenantId IS NULL AND dependency.IsDeleted=0;

IF OBJECT_ID(N'CRM.DuplicateRule',N'U') IS NOT NULL
BEGIN
	MERGE CRM.DuplicateRule target USING
	(
		SELECT tenant.TenantId,profile.EntityTypeCode,profile.DisplayName,profile.PossibleThreshold,
			   STRING_AGG(field.FieldCode,N',') WITHIN GROUP(ORDER BY field.SortOrder) MatchFields
		FROM Core.Tenant tenant CROSS JOIN Search.MatchProfile profile
		JOIN Search.MatchFieldRule field ON field.MatchProfileId=profile.MatchProfileId AND field.TenantId IS NULL AND field.IsActive=1 AND field.IsDeleted=0
		WHERE tenant.IsDeleted=0 AND profile.TenantId IS NULL AND profile.ProfileCode IN(N'LEAD_DUPLICATE',N'ACCOUNT_DUPLICATE',N'CONTACT_DUPLICATE') AND profile.IsDeleted=0
		GROUP BY tenant.TenantId,profile.EntityTypeCode,profile.DisplayName,profile.PossibleThreshold
	) source ON target.TenantId=source.TenantId AND target.EntityType=source.EntityTypeCode AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET RuleName=source.DisplayName,MatchFields=source.MatchFields,MatchThreshold=CONVERT(INT,source.PossibleThreshold),ActionOnMatch=N'Review',Description=N'Synchronized from Search & Matching Platform profile.',IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(DuplicateRuleId,TenantId,RuleName,EntityType,MatchFields,MatchThreshold,ActionOnMatch,Description,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.DisplayName,source.EntityTypeCode,source.MatchFields,CONVERT(INT,source.PossibleThreshold),N'Review',N'Synchronized from Search & Matching Platform profile.',1,SYSUTCDATETIME(),0);
END;
GO
