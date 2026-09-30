SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- Judz Enterprise Document Evidence Upload — Stage 2 of 3 (Legal provenance identity).
--
-- This is the most important legal concept the canonical corpus stack does not model: a single
-- physical object (one SHA-256 in POLOXI.Legal_MatterDocumentVersion) can enter a matter through
-- MULTIPLE legally-distinct provenance occurrences — plaintiff production, defendant production, a
-- third-party subpoena, an email attachment — each with its own custodian, Bates range, and source.
--
--   EvidenceOccurrence : one row per provenance occurrence of a document in a matter. Physical
--                        content is deduplicated (same version reused) but provenance is NEVER
--                        collapsed. This is why "same hash" must not mean "same evidence".
--
--   DocumentFamily     : container/email family grouping (email + attachments, ZIP + children) so a
--                        duplicate attachment appearing in two emails remains two occurrences that
--                        share physical content while preserving family integrity.
--
-- ADDITIVE and fail-soft: creating an occurrence never changes extraction, embeddings, propositions,
-- CDC, or scoring. Occurrences reference the existing document/version rows. Schema-only.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'POLOXI.Legal_DocumentFamily', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DocumentFamily
(
	LegalDocumentFamilyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DocumentFamily PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DocumentFamily_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	FamilyLabel NVARCHAR(300) NULL,
	ContainerTypeCode NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DocumentFamily_Type DEFAULT N'LOOSE',
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DocumentFamily_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DocumentFamily_Deleted DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DocumentFamily') AND name=N'IX_Legal_DocumentFamily_Matter')
	CREATE INDEX IX_Legal_DocumentFamily_Matter ON POLOXI.Legal_DocumentFamily (TenantId, DecisionMatterId) INCLUDE (ContainerTypeCode, FamilyLabel) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'POLOXI.Legal_EvidenceOccurrence', N'U') IS NULL
CREATE TABLE POLOXI.Legal_EvidenceOccurrence
(
	LegalEvidenceOccurrenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_EvidenceOccurrence PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_EvidenceOccurrence_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	-- Physical content this occurrence points at. The version carries the immutable SHA-256; multiple
	-- occurrences may share the same version (identical bytes, different provenance).
	LegalDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_EvidenceOccurrence_Document REFERENCES POLOXI.Legal_MatterDocument (LegalDocumentId),
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_EvidenceOccurrence_Version REFERENCES POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId),
	-- The upload command that produced this occurrence (nullable for backfilled/system occurrences).
	LegalUploadOperationId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_EvidenceOccurrence_UploadOp REFERENCES POLOXI.Legal_UploadOperation (LegalUploadOperationId),
	-- Provenance metadata — the legally-meaningful identity of THIS occurrence.
	SourceTypeCode NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_EvidenceOccurrence_Source DEFAULT N'USER_UPLOAD',
	Custodian NVARCHAR(300) NULL,
	ProducedBy NVARCHAR(300) NULL,
	ProductionId NVARCHAR(120) NULL,
	BatesStart NVARCHAR(100) NULL,
	BatesEnd NVARCHAR(100) NULL,
	OriginalPath NVARCHAR(2000) NULL,
	ConfidentialityCode NVARCHAR(60) NULL,
	PrivilegeCode NVARCHAR(60) NULL,
	-- Family integrity: attachments/children preserve their container and position.
	LegalDocumentFamilyId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_EvidenceOccurrence_Family REFERENCES POLOXI.Legal_DocumentFamily (LegalDocumentFamilyId),
	ParentOccurrenceId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_EvidenceOccurrence_Parent REFERENCES POLOXI.Legal_EvidenceOccurrence (LegalEvidenceOccurrenceId),
	FamilyDepth INT NOT NULL CONSTRAINT DF_Legal_EvidenceOccurrence_Depth DEFAULT 0,
	FamilyOrdinal INT NOT NULL CONSTRAINT DF_Legal_EvidenceOccurrence_Ordinal DEFAULT 0,
	ContentReused BIT NOT NULL CONSTRAINT DF_Legal_EvidenceOccurrence_Reused DEFAULT 0,
	ReceivedDateUtc DATETIME2 NULL,
	Notes NVARCHAR(2000) NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_EvidenceOccurrence_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_EvidenceOccurrence_Deleted DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_EvidenceOccurrence') AND name=N'IX_Legal_EvidenceOccurrence_Document')
	CREATE INDEX IX_Legal_EvidenceOccurrence_Document ON POLOXI.Legal_EvidenceOccurrence (TenantId, LegalDocumentId) INCLUDE (SourceTypeCode, Custodian, ProductionId, ContentReused) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_EvidenceOccurrence') AND name=N'IX_Legal_EvidenceOccurrence_Matter')
	CREATE INDEX IX_Legal_EvidenceOccurrence_Matter ON POLOXI.Legal_EvidenceOccurrence (TenantId, DecisionMatterId, SourceTypeCode) INCLUDE (LegalDocumentVersionId, LegalUploadOperationId) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_EvidenceOccurrence') AND name=N'IX_Legal_EvidenceOccurrence_Version')
	CREATE INDEX IX_Legal_EvidenceOccurrence_Version ON POLOXI.Legal_EvidenceOccurrence (TenantId, LegalDocumentVersionId) WHERE IsDeleted=0;
GO

COMMIT TRANSACTION;
