SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySearchPlan',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AuthoritySearchPlan
(
	LegalSearchPlanId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AuthoritySearchPlan PRIMARY KEY,
	DecisionResearchNeedId UNIQUEIDENTIFIER NOT NULL,
	DecisionSessionId UNIQUEIDENTIFIER NOT NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	Proposition NVARCHAR(MAX) NOT NULL,
	Jurisdiction NVARCHAR(400) NULL,
	AuthorityCutoffDate DATE NULL,
	OutcomeCode NVARCHAR(40) NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_AuthoritySearchPlan_Created DEFAULT SYSUTCDATETIME(),
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_AuthoritySearchPlan_Deleted DEFAULT 0,
	CONSTRAINT FK_Legal_AuthoritySearchPlan_Need FOREIGN KEY (DecisionResearchNeedId) REFERENCES POLOXI.Legal_DecisionResearchNeed(DecisionResearchNeedId)
);

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySearchOperation',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AuthoritySearchOperation
(
	LegalSearchOperationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AuthoritySearchOperation PRIMARY KEY,
	LegalSearchPlanId UNIQUEIDENTIFIER NOT NULL,
	ParentOperationId UNIQUEIDENTIFIER NULL,
	OperationKindCode NVARCHAR(40) NOT NULL,
	Query NVARCHAR(4000) NOT NULL,
	AuthorityKindCode NVARCHAR(40) NOT NULL,
	SequenceNumber INT NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_AuthoritySearchOperation_Created DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_Legal_AuthoritySearchOperation_Plan FOREIGN KEY (LegalSearchPlanId) REFERENCES POLOXI.Legal_AuthoritySearchPlan(LegalSearchPlanId),
	CONSTRAINT FK_Legal_AuthoritySearchOperation_Parent FOREIGN KEY (ParentOperationId) REFERENCES POLOXI.Legal_AuthoritySearchOperation(LegalSearchOperationId)
);

IF OBJECT_ID(N'POLOXI.Legal_AuthorityProviderAttempt',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AuthorityProviderAttempt
(
	LegalProviderAttemptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AuthorityProviderAttempt PRIMARY KEY,
	LegalSearchPlanId UNIQUEIDENTIFIER NOT NULL,
	LegalSearchOperationId UNIQUEIDENTIFIER NOT NULL,
	ProviderCode NVARCHAR(80) NOT NULL,
	OutcomeCode NVARCHAR(40) NOT NULL,
	RawResultCount INT NOT NULL,
	ReturnedCount INT NOT NULL,
	Detail NVARCHAR(2000) NULL,
	DurationMilliseconds BIGINT NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_AuthorityProviderAttempt_Created DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_Legal_AuthorityProviderAttempt_Plan FOREIGN KEY (LegalSearchPlanId) REFERENCES POLOXI.Legal_AuthoritySearchPlan(LegalSearchPlanId),
	CONSTRAINT FK_Legal_AuthorityProviderAttempt_Operation FOREIGN KEY (LegalSearchOperationId) REFERENCES POLOXI.Legal_AuthoritySearchOperation(LegalSearchOperationId)
);

IF OBJECT_ID(N'POLOXI.Legal_NormalizedAuthority',N'U') IS NULL
CREATE TABLE POLOXI.Legal_NormalizedAuthority
(
	NormalizedAuthorityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_NormalizedAuthority PRIMARY KEY DEFAULT NEWID(),
	AuthorityIdentity NVARCHAR(128) NOT NULL,
	SourceRef NVARCHAR(2000) NOT NULL,
	Title NVARCHAR(1000) NOT NULL,
	Passage NVARCHAR(MAX) NOT NULL,
	Jurisdiction NVARCHAR(400) NULL,
	AuthorityDate DATE NULL,
	AuthorityKindCode NVARCHAR(40) NULL,
	ProviderCode NVARCHAR(80) NULL,
	SourceVersion NVARCHAR(80) NULL,
	ProviderIdentityVerified BIT NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_NormalizedAuthority_Created DEFAULT SYSUTCDATETIME(),
	CONSTRAINT UX_Legal_NormalizedAuthority_Identity UNIQUE (AuthorityIdentity)
);

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySearchResult',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AuthoritySearchResult
(
	LegalSearchPlanId UNIQUEIDENTIFIER NOT NULL,
	NormalizedAuthorityId UNIQUEIDENTIFIER NOT NULL,
	LegalSearchOperationId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_AuthoritySearchResult_Created DEFAULT SYSUTCDATETIME(),
	CONSTRAINT PK_Legal_AuthoritySearchResult PRIMARY KEY (LegalSearchPlanId,NormalizedAuthorityId,LegalSearchOperationId),
	CONSTRAINT FK_Legal_AuthoritySearchResult_Plan FOREIGN KEY (LegalSearchPlanId) REFERENCES POLOXI.Legal_AuthoritySearchPlan(LegalSearchPlanId),
	CONSTRAINT FK_Legal_AuthoritySearchResult_Authority FOREIGN KEY (NormalizedAuthorityId) REFERENCES POLOXI.Legal_NormalizedAuthority(NormalizedAuthorityId),
	CONSTRAINT FK_Legal_AuthoritySearchResult_Operation FOREIGN KEY (LegalSearchOperationId) REFERENCES POLOXI.Legal_AuthoritySearchOperation(LegalSearchOperationId)
);

IF OBJECT_ID(N'POLOXI.Legal_VerifiedLegalProposition',N'U') IS NULL
CREATE TABLE POLOXI.Legal_VerifiedLegalProposition
(
	VerifiedLegalPropositionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_VerifiedLegalProposition PRIMARY KEY DEFAULT NEWID(),
	DecisionResearchNeedId UNIQUEIDENTIFIER NOT NULL,
	DecisionSessionId UNIQUEIDENTIFIER NOT NULL,
	DecisionBranchId UNIQUEIDENTIFIER NOT NULL,
	NormalizedAuthorityId UNIQUEIDENTIFIER NOT NULL,
	DecisionEvidenceVerificationId UNIQUEIDENTIFIER NOT NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	Proposition NVARCHAR(MAX) NOT NULL,
	DispositionCode NVARCHAR(40) NOT NULL,
	SupportingPassage NVARCHAR(MAX) NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_VerifiedLegalProposition_Created DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_Legal_VerifiedLegalProposition_Need FOREIGN KEY (DecisionResearchNeedId) REFERENCES POLOXI.Legal_DecisionResearchNeed(DecisionResearchNeedId),
	CONSTRAINT FK_Legal_VerifiedLegalProposition_Authority FOREIGN KEY (NormalizedAuthorityId) REFERENCES POLOXI.Legal_NormalizedAuthority(NormalizedAuthorityId),
	CONSTRAINT FK_Legal_VerifiedLegalProposition_Verification FOREIGN KEY (DecisionEvidenceVerificationId) REFERENCES POLOXI.Legal_DecisionEvidenceVerification(DecisionEvidenceVerificationId)
);

IF OBJECT_ID(N'POLOXI.Legal_AuthorityDecisionImpact',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AuthorityDecisionImpact
(
	LegalAuthorityDecisionImpactId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AuthorityDecisionImpact PRIMARY KEY DEFAULT NEWID(),
	DecisionResearchNeedId UNIQUEIDENTIFIER NOT NULL,
	DecisionSessionId UNIQUEIDENTIFIER NOT NULL,
	DecisionBranchId UNIQUEIDENTIFIER NOT NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	DecisionSignalAdmitted BIT NOT NULL,
	DependencyStateChanged BIT NOT NULL,
	RecompetitionTriggered BIT NOT NULL,
	WinnerChanged BIT NOT NULL,
	OutcomeCode NVARCHAR(60) NOT NULL,
	Detail NVARCHAR(2000) NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_AuthorityDecisionImpact_Created DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_Legal_AuthorityDecisionImpact_Need FOREIGN KEY (DecisionResearchNeedId) REFERENCES POLOXI.Legal_DecisionResearchNeed(DecisionResearchNeedId)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthoritySearchPlan') AND name=N'IX_Legal_AuthoritySearchPlan_Need')
	CREATE INDEX IX_Legal_AuthoritySearchPlan_Need ON POLOXI.Legal_AuthoritySearchPlan(DecisionResearchNeedId,CreatedDateUtc) WHERE IsDeleted=0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthorityProviderAttempt') AND name=N'IX_Legal_AuthorityProviderAttempt_Plan')
	CREATE INDEX IX_Legal_AuthorityProviderAttempt_Plan ON POLOXI.Legal_AuthorityProviderAttempt(LegalSearchPlanId,LegalSearchOperationId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_VerifiedLegalProposition') AND name=N'IX_Legal_VerifiedLegalProposition_Need')
	CREATE INDEX IX_Legal_VerifiedLegalProposition_Need ON POLOXI.Legal_VerifiedLegalProposition(DecisionResearchNeedId,DecisionBranchId,CreatedDateUtc);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthorityDecisionImpact') AND name=N'IX_Legal_AuthorityDecisionImpact_Need')
	CREATE INDEX IX_Legal_AuthorityDecisionImpact_Need ON POLOXI.Legal_AuthorityDecisionImpact(DecisionResearchNeedId,CreatedDateUtc);

COMMIT TRANSACTION;
