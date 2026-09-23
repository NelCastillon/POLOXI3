SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NULL
CREATE TABLE POLOXI.Legal_AuthoritySource
(
	LegalAuthoritySourceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AuthoritySource PRIMARY KEY DEFAULT NEWID(),
	ProviderCode NVARCHAR(80) NOT NULL,
	JurisdictionCode NVARCHAR(40) NOT NULL,
	AuthorityKindCode NVARCHAR(40) NOT NULL,
	CitationPattern NVARCHAR(1000) NOT NULL,
	BaseUrl NVARCHAR(1000) NOT NULL,
	DocumentUrlTemplate NVARCHAR(2000) NOT NULL,
	SectionAnchorTemplate NVARCHAR(500) NULL,
	ExtractionStrategyCode NVARCHAR(40) NOT NULL,
	DiscoveryMethodCode NVARCHAR(40) NULL,
	DiscoveryEvidenceUrl NVARCHAR(2000) NULL,
	VerifiedDateUtc DATETIME2 NULL,
	Priority INT NOT NULL CONSTRAINT DF_Legal_AuthoritySource_Priority DEFAULT 100,
	IsEnabled BIT NOT NULL CONSTRAINT DF_Legal_AuthoritySource_Enabled DEFAULT 1,
	TenantId UNIQUEIDENTIFIER NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_AuthoritySource_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_AuthoritySource_Deleted DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthoritySource') AND name=N'UX_Legal_AuthoritySource_ProviderJurisdictionKind')
	CREATE UNIQUE INDEX UX_Legal_AuthoritySource_ProviderJurisdictionKind
	ON POLOXI.Legal_AuthoritySource (ProviderCode,JurisdictionCode,AuthorityKindCode,TenantId)
	WHERE IsDeleted=0;

COMMIT TRANSACTION;
