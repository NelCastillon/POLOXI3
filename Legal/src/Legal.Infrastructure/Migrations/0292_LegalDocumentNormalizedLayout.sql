SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DocumentPage', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DocumentPage
(
	LegalDocumentPageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DocumentPage PRIMARY KEY DEFAULT NEWID(),
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DocumentPage_Version REFERENCES POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId),
	PageNumber INT NOT NULL,
	PageText NVARCHAR(MAX) NOT NULL,
	ExtractionMethodCode NVARCHAR(60) NOT NULL,
	ExtractionConfidence DECIMAL(5,4) NULL,
	Width DECIMAL(18,6) NULL,
	Height DECIMAL(18,6) NULL,
	MeasurementUnit NVARCHAR(30) NULL,
	IsNativeTextReliable BIT NOT NULL,
	ContentHash CHAR(64) NOT NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentPage_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DocumentPage_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_DocumentPage_Number CHECK (PageNumber > 0),
	CONSTRAINT CK_Legal_DocumentPage_Confidence CHECK (ExtractionConfidence IS NULL OR ExtractionConfidence BETWEEN 0 AND 1)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentPage') AND name=N'UX_Legal_DocumentPage_Number')
	CREATE UNIQUE INDEX UX_Legal_DocumentPage_Number ON POLOXI.Legal_DocumentPage (LegalDocumentVersionId, PageNumber) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentPage') AND name=N'IX_Legal_DocumentPage_TenantVersion')
	CREATE INDEX IX_Legal_DocumentPage_TenantVersion ON POLOXI.Legal_DocumentPage (TenantId, LegalDocumentVersionId, PageNumber) INCLUDE (ExtractionMethodCode, IsNativeTextReliable, ContentHash) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_DocumentLayoutArtifact', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DocumentLayoutArtifact
(
	LegalDocumentLayoutArtifactId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DocumentLayoutArtifact PRIMARY KEY DEFAULT NEWID(),
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DocumentLayoutArtifact_Version REFERENCES POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId),
	LegalDocumentPageId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_DocumentLayoutArtifact_Page REFERENCES POLOXI.Legal_DocumentPage (LegalDocumentPageId),
	ArtifactTypeCode NVARCHAR(40) NOT NULL,
	SequenceNumber INT NOT NULL,
	RoleCode NVARCHAR(80) NULL,
	ArtifactText NVARCHAR(MAX) NULL,
	Confidence DECIMAL(5,4) NULL,
	BoundingRegionJson NVARCHAR(MAX) NULL,
	SourceSpanJson NVARCHAR(MAX) NULL,
	ContentJson NVARCHAR(MAX) NULL,
	ContentHash CHAR(64) NOT NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentLayoutArtifact_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DocumentLayoutArtifact_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_DocumentLayoutArtifact_Type CHECK (ArtifactTypeCode IN (N'PARAGRAPH',N'LINE',N'WORD',N'SELECTION_MARK',N'SECTION',N'TABLE')),
	CONSTRAINT CK_Legal_DocumentLayoutArtifact_Confidence CHECK (Confidence IS NULL OR Confidence BETWEEN 0 AND 1)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentLayoutArtifact') AND name=N'UX_Legal_DocumentLayoutArtifact_Sequence')
	CREATE UNIQUE INDEX UX_Legal_DocumentLayoutArtifact_Sequence ON POLOXI.Legal_DocumentLayoutArtifact (LegalDocumentVersionId, ArtifactTypeCode, LegalDocumentPageId, SequenceNumber) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentLayoutArtifact') AND name=N'IX_Legal_DocumentLayoutArtifact_Page')
	CREATE INDEX IX_Legal_DocumentLayoutArtifact_Page ON POLOXI.Legal_DocumentLayoutArtifact (TenantId, LegalDocumentVersionId, LegalDocumentPageId, ArtifactTypeCode, SequenceNumber) INCLUDE (RoleCode, Confidence, ContentHash) WHERE IsDeleted=0;
GO

COMMIT TRANSACTION;
