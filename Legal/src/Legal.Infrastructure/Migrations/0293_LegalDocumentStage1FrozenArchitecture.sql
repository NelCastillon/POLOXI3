SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DocumentLayoutArtifact', N'U') IS NOT NULL
BEGIN
	IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'POLOXI.Legal_DocumentLayoutArtifact') AND name=N'CK_Legal_DocumentLayoutArtifact_Type')
		ALTER TABLE POLOXI.Legal_DocumentLayoutArtifact DROP CONSTRAINT CK_Legal_DocumentLayoutArtifact_Type;
	IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'POLOXI.Legal_DocumentLayoutArtifact') AND name=N'CK_Legal_DocumentLayoutArtifact_Type')
		ALTER TABLE POLOXI.Legal_DocumentLayoutArtifact ADD CONSTRAINT CK_Legal_DocumentLayoutArtifact_Type CHECK (ArtifactTypeCode IN (N'PARAGRAPH',N'LINE',N'WORD',N'SELECTION_MARK',N'SECTION',N'TABLE',N'FIGURE'));
END;

IF OBJECT_ID(N'POLOXI.Legal_DocumentSearchProjectionOutbox', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DocumentSearchProjectionOutbox
(
	LegalDocumentSearchProjectionOutboxId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DocumentSearchProjectionOutbox PRIMARY KEY DEFAULT NEWID(),
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DocumentSearchProjectionOutbox_Version REFERENCES POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId),
	StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_DocumentSearchProjectionOutbox_Status DEFAULT N'PENDING',
	AttemptCount INT NOT NULL CONSTRAINT DF_Legal_DocumentSearchProjectionOutbox_Attempt DEFAULT 0,
	NextAttemptDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentSearchProjectionOutbox_Next DEFAULT SYSUTCDATETIME(),
	LastError NVARCHAR(4000) NULL,
	ProcessingStartedDateUtc DATETIME2 NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentSearchProjectionOutbox_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ProcessedDateUtc DATETIME2 NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DocumentSearchProjectionOutbox_Deleted DEFAULT 0
);
IF COL_LENGTH(N'POLOXI.Legal_DocumentSearchProjectionOutbox',N'ProcessingStartedDateUtc') IS NULL
	ALTER TABLE POLOXI.Legal_DocumentSearchProjectionOutbox ADD ProcessingStartedDateUtc DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentSearchProjectionOutbox') AND name=N'UX_Legal_DocumentSearchProjectionOutbox_Version')
	CREATE UNIQUE INDEX UX_Legal_DocumentSearchProjectionOutbox_Version ON POLOXI.Legal_DocumentSearchProjectionOutbox (LegalDocumentVersionId) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentSearchProjectionOutbox') AND name=N'IX_Legal_DocumentSearchProjectionOutbox_Pending')
	CREATE INDEX IX_Legal_DocumentSearchProjectionOutbox_Pending ON POLOXI.Legal_DocumentSearchProjectionOutbox (StatusCode,NextAttemptDateUtc,CreatedDateUtc) INCLUDE (TenantId,LegalDocumentVersionId,AttemptCount) WHERE IsDeleted=0;

COMMIT TRANSACTION;