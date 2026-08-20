SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'Search.SearchCapability',N'U') IS NULL
BEGIN
	CREATE TABLE Search.SearchCapability
	(
		SearchCapabilityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_SearchCapability PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		CapabilityCode NVARCHAR(80) NOT NULL,
		DisplayName NVARCHAR(160) NOT NULL,
		IsAvailable BIT NOT NULL CONSTRAINT DF_Search_SearchCapability_Available DEFAULT 0,
		IsEnabled BIT NOT NULL CONSTRAINT DF_Search_SearchCapability_Enabled DEFAULT 1,
		ConfigurationJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Search_SearchCapability_Config DEFAULT N'{}',
		LastVerifiedDateUtc DATETIME2 NULL,
		LastError NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_SearchCapability_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_SearchCapability_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Search_SearchCapability_Config CHECK(ISJSON(ConfigurationJson)=1)
	);
	CREATE UNIQUE INDEX UX_Search_SearchCapability_Global ON Search.SearchCapability(CapabilityCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Search_SearchCapability_Tenant ON Search.SearchCapability(TenantId,CapabilityCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.SemanticQueryEvidence',N'U') IS NULL
BEGIN
	CREATE TABLE Search.SemanticQueryEvidence
	(
		SemanticQueryEvidenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_SemanticQueryEvidence PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		RequestedByUserId UNIQUEIDENTIFIER NULL,
		CorrelationId NVARCHAR(200) NOT NULL,
		QueryText NVARCHAR(500) NOT NULL,
		ExpandedTermsJson NVARCHAR(MAX) NOT NULL,
		ConceptsJson NVARCHAR(MAX) NOT NULL,
		ProviderCode NVARCHAR(80) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_SemanticQueryEvidence_Created DEFAULT SYSUTCDATETIME(),
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_SemanticQueryEvidence_Deleted DEFAULT 0,
		CONSTRAINT CK_Search_SemanticQueryEvidence_Terms CHECK(ISJSON(ExpandedTermsJson)=1),
		CONSTRAINT CK_Search_SemanticQueryEvidence_Concepts CHECK(ISJSON(ConceptsJson)=1)
	);
	CREATE INDEX IX_Search_SemanticQueryEvidence_TenantDate ON Search.SemanticQueryEvidence(TenantId,CreatedDateUtc DESC) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'Search.MatchReviewDecision',N'U') IS NULL
BEGIN
	CREATE TABLE Search.MatchReviewDecision
	(
		MatchReviewDecisionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Search_MatchReviewDecision PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		MatchExecutionId UNIQUEIDENTIFIER NOT NULL,
		CandidateEntityId UNIQUEIDENTIFIER NULL,
		DecisionCode NVARCHAR(40) NOT NULL,
		Notes NVARCHAR(2000) NULL,
		RequestedByUserId UNIQUEIDENTIFIER NOT NULL,
		CorrelationId NVARCHAR(200) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Search_MatchReviewDecision_Created DEFAULT SYSUTCDATETIME(),
		IsDeleted BIT NOT NULL CONSTRAINT DF_Search_MatchReviewDecision_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Search_MatchReviewDecision_Execution FOREIGN KEY(MatchExecutionId) REFERENCES Search.MatchExecution(MatchExecutionId),
		CONSTRAINT CK_Search_MatchReviewDecision_Code CHECK(DecisionCode IN(N'USE_EXISTING',N'CREATE_NEW',N'COMPARE',N'MERGE_REQUEST'))
	);
	CREATE INDEX IX_Search_MatchReviewDecision_Execution ON Search.MatchReviewDecision(TenantId,MatchExecutionId,CreatedDateUtc DESC) WHERE IsDeleted=0;
END;
GO

DECLARE @FullTextAvailable BIT=CASE WHEN FULLTEXTSERVICEPROPERTY(N'IsFullTextInstalled')=1 THEN 1 ELSE 0 END;
MERGE Search.SearchCapability target USING(VALUES
(N'EXACT',N'Exact and normalized database search',CAST(1 AS BIT),N'{"fallback":"bounded-like"}'),
(N'FULL_TEXT',N'SQL Server Full-Text Search',@FullTextAvailable,N'{"catalog":"AmsSearchCatalog","table":"Search.EntityProjection"}'),
(N'PHONETIC',N'Phonetic matching',CAST(1 AS BIT),N'{"algorithm":"Soundex"}'),
(N'EDIT_DISTANCE',N'Edit-distance matching',CAST(1 AS BIT),N'{"algorithm":"Damerau-Levenshtein"}'),
(N'FUZZY',N'Weighted fuzzy matching',CAST(1 AS BIT),N'{"algorithm":"TokenJaccard"}'),
(N'SEMANTIC',N'Knowledge-backed semantic expansion',CAST(1 AS BIT),N'{"provider":"KnowledgePlatform","advisoryOnly":true,"maximumTokens":12,"maximumPhraseLength":3,"maximumPhrases":30}')) source(CapabilityCode,DisplayName,IsAvailable,ConfigurationJson)
ON target.TenantId IS NULL AND target.CapabilityCode=source.CapabilityCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,IsAvailable=source.IsAvailable,IsEnabled=1,ConfigurationJson=source.ConfigurationJson,LastVerifiedDateUtc=SYSUTCDATETIME(),LastError=NULL,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(SearchCapabilityId,TenantId,CapabilityCode,DisplayName,IsAvailable,IsEnabled,ConfigurationJson,LastVerifiedDateUtc,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.CapabilityCode,source.DisplayName,source.IsAvailable,1,source.ConfigurationJson,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
GO

DECLARE @Profiles TABLE(ProfileCode NVARCHAR(120),EntityTypeCode NVARCHAR(80),DisplayName NVARCHAR(200),Description NVARCHAR(1000),ExactThreshold DECIMAL(5,2),StrongThreshold DECIMAL(5,2),PossibleThreshold DECIMAL(5,2),MaximumCandidates INT);
INSERT @Profiles VALUES
(N'LOCATION_MATCH',N'Location',N'Location Matching',N'Matches account and submission locations by normalized address without automatic linkage.',95,85,70,25),
(N'VEHICLE_MATCH',N'Vehicle',N'Vehicle Matching',N'Matches vehicles with VIN as an exact-only critical identifier.',98,90,75,25),
(N'CLAIM_PARTY_MATCH',N'ClaimParty',N'Claim Party Matching',N'Matches claimants and other claim parties by person and contact signals.',95,85,70,25),
(N'COMMISSION_LINE_RECONCILIATION',N'CommissionLine',N'Commission Line Reconciliation',N'Ranks expected receivables for carrier statement lines; approval remains explicit.',98,90,75,50);
MERGE Search.MatchProfile target USING @Profiles source ON target.TenantId IS NULL AND target.ProfileCode=source.ProfileCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET EntityTypeCode=source.EntityTypeCode,DisplayName=source.DisplayName,Description=source.Description,ExactThreshold=source.ExactThreshold,StrongThreshold=source.StrongThreshold,PossibleThreshold=source.PossibleThreshold,MaximumCandidates=source.MaximumCandidates,AllowAutomaticLink=0,RequiresReview=1,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(MatchProfileId,TenantId,ProfileCode,EntityTypeCode,DisplayName,Description,ExactThreshold,StrongThreshold,PossibleThreshold,MaximumCandidates,AllowAutomaticLink,RequiresReview,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.ProfileCode,source.EntityTypeCode,source.DisplayName,source.Description,source.ExactThreshold,source.StrongThreshold,source.PossibleThreshold,source.MaximumCandidates,0,1,1,SYSUTCDATETIME(),0);

DECLARE @Rules TABLE(ProfileCode NVARCHAR(120),FieldCode NVARCHAR(100),DisplayName NVARCHAR(160),AlgorithmCode NVARCHAR(80),Weight DECIMAL(7,4),MinimumSimilarity DECIMAL(5,2),IsRequired BIT,IsCriticalIdentifier BIT,ExactMatchOnly BIT,IsSensitive BIT,SortOrder INT);
INSERT @Rules VALUES
(N'LOCATION_MATCH',N'Address',N'Address',N'TOKEN_JACCARD',60,60,1,0,0,0,10),(N'LOCATION_MATCH',N'PostalCode',N'Postal Code',N'NORMALIZED_EXACT',25,100,0,0,0,0,20),(N'LOCATION_MATCH',N'LocationName',N'Location Name',N'DAMERAU_LEVENSHTEIN',15,70,0,0,0,0,30),
(N'VEHICLE_MATCH',N'Vin',N'VIN',N'EXACT',70,100,0,1,1,1,10),(N'VEHICLE_MATCH',N'MakeModel',N'Make and Model',N'TOKEN_JACCARD',15,60,1,0,0,0,20),(N'VEHICLE_MATCH',N'ModelYear',N'Model Year',N'EXACT',15,100,0,0,0,0,30),
(N'CLAIM_PARTY_MATCH',N'FullName',N'Party Name',N'DAMERAU_LEVENSHTEIN',35,75,1,0,0,0,10),(N'CLAIM_PARTY_MATCH',N'FullName',N'Party Name Phonetic',N'SOUNDEX',15,100,0,0,0,0,11),(N'CLAIM_PARTY_MATCH',N'Email',N'Email',N'NORMALIZED_EXACT',25,100,0,0,0,1,20),(N'CLAIM_PARTY_MATCH',N'Phone',N'Phone',N'NORMALIZED_EXACT',25,100,0,0,0,1,30),
(N'COMMISSION_LINE_RECONCILIATION',N'PolicyNumber',N'Policy Number',N'EXACT',45,100,0,1,1,0,10),(N'COMMISSION_LINE_RECONCILIATION',N'CarrierId',N'Carrier',N'EXACT',20,100,1,1,1,0,20),(N'COMMISSION_LINE_RECONCILIATION',N'InsuredName',N'Insured Name',N'TOKEN_JACCARD',20,65,0,0,0,0,30),(N'COMMISSION_LINE_RECONCILIATION',N'PremiumAmount',N'Premium Amount',N'EXACT',15,100,0,0,0,0,40);
MERGE Search.MatchFieldRule target USING(SELECT profile.MatchProfileId,fieldRule.*,algorithm.MatchAlgorithmId FROM @Rules fieldRule JOIN Search.MatchProfile profile ON profile.TenantId IS NULL AND profile.ProfileCode=fieldRule.ProfileCode AND profile.IsDeleted=0 JOIN Search.MatchAlgorithm algorithm ON algorithm.TenantId IS NULL AND algorithm.AlgorithmCode=fieldRule.AlgorithmCode AND algorithm.IsDeleted=0) source
ON target.TenantId IS NULL AND target.MatchProfileId=source.MatchProfileId AND target.FieldCode=source.FieldCode AND target.MatchAlgorithmId=source.MatchAlgorithmId AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Weight=source.Weight,MinimumSimilarity=source.MinimumSimilarity,IsRequired=source.IsRequired,IsCriticalIdentifier=source.IsCriticalIdentifier,ExactMatchOnly=source.ExactMatchOnly,IsSensitive=source.IsSensitive,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(MatchFieldRuleId,TenantId,MatchProfileId,FieldCode,DisplayName,MatchAlgorithmId,Weight,MinimumSimilarity,IsRequired,IsCriticalIdentifier,ExactMatchOnly,IsSensitive,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.MatchProfileId,source.FieldCode,source.DisplayName,source.MatchAlgorithmId,source.Weight,source.MinimumSimilarity,source.IsRequired,source.IsCriticalIdentifier,source.ExactMatchOnly,source.IsSensitive,source.SortOrder,1,SYSUTCDATETIME(),0);
GO
