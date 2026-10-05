SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 0401: POLOXI Legal — Media & Machine Evidence channel (Phase 3, Slice 1).
--
-- Adds the asset/anchor/review infrastructure for a THIRD evidence channel (channel MEDIA_MACHINE_DATA)
-- that converges on the SAME shared proposition-integration funnel used by the manual Attorney Decision
-- Input (ADI) path and the Document-Retrieval path (migration 0399 + the IPropositionIntegrationService
-- seam). Media supplies new atomic propositions anchored to exact source regions/intervals; it NEVER
-- scores candidates, picks winners, or initializes interpolation. POLOXI Core remains the sole
-- competition authority.
--
-- Converges on the existing review/park machinery: a parked media proposition is a row in
-- POLOXI.Legal_RetrievedProposition (0399) whose LegalDocumentVersionId references a MediaAssetVersion
-- and whose placements are Legal_PropositionNodeLink rows. The media-specific tables below add the
-- immutable asset/version bytes-reference, derivatives, precise evidence anchors, and processing runs,
-- and link an anchor to a parked proposition via PropositionEvidenceLink.
--
-- Six additive tables (all with standard base/audit fields: TenantId, CreatedDateUtc, CreatedByUserId,
-- ModifiedDateUtc, ModifiedByUserId, IsDeleted). Idempotent via OBJECT_ID guards; safe to rerun.
--
--   1. Legal_MediaAsset            — a registered media/machine asset (logical, versioned container).
--   2. Legal_MediaAssetVersion     — an immutable version: private storage key, hash, metadata, capture.
--   3. Legal_MediaDerivative       — thumbnail/preview/transcript/frame/parsed-log of an asset version.
--   4. Legal_EvidenceAnchor        — a precise source location: image region | media interval | record.
--   5. Legal_PropositionEvidenceLink — proposition ↔ anchor ↔ relationship ↔ extraction run ↔ review.
--   6. Legal_MediaProcessingRun    — processor/version, status, errors, output references (idempotent).
--
-- STRICT INVARIANTS:
--   * An asset content hash identifies BYTES; it does NOT prove authenticity.
--   * Every anchor/link stays within one tenant AND one matter.
--   * Media-relative time (interval ms) is distinct from asserted real-world event time.
--   * Reprocessing creates a NEW run; it never overwrites reviewed propositions or anchors.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── 1. MediaAsset: a logical, versioned media/machine asset registered against a matter. ───────────
IF OBJECT_ID(N'POLOXI.Legal_MediaAsset',N'U') IS NULL
CREATE TABLE POLOXI.Legal_MediaAsset
(
	MediaAssetId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MediaAsset PRIMARY KEY DEFAULT NEWID(),

	DecisionMatterId      UNIQUEIDENTIFIER NOT NULL,
	-- Optional link to an existing MatterResource (authorization anchor) when one exists.
	MatterResourceId      UNIQUEIDENTIFIER NULL,

	-- PHOTO | VIDEO | AUDIO | GPS | TELEMATICS | DEVICE_LOG | SYSTEM_LOG.
	AssetTypeCode         NVARCHAR(30) NOT NULL,

	OriginalFileName      NVARCHAR(400) NOT NULL,
	MimeType              NVARCHAR(150) NOT NULL,
	-- Provenance of the asset (device, upload, carrier portal, etc.). Advisory until reviewed.
	SourceDescription     NVARCHAR(400) NULL,
	UploadedByUserId      UNIQUEIDENTIFIER NULL,

	-- Standard base/audit fields.
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_MediaAsset_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_MediaAsset_IsDeleted DEFAULT 0,
	RowVersion            ROWVERSION
);

GO

IF OBJECT_ID(N'POLOXI.IX_Legal_MediaAsset_Matter',N'IX') IS NULL
	CREATE INDEX IX_Legal_MediaAsset_Matter
		ON POLOXI.Legal_MediaAsset (TenantId, DecisionMatterId) WHERE IsDeleted = 0;

GO

-- ── 2. MediaAssetVersion: an immutable version of an asset's bytes + metadata + capture assertion. ─
IF OBJECT_ID(N'POLOXI.Legal_MediaAssetVersion',N'U') IS NULL
CREATE TABLE POLOXI.Legal_MediaAssetVersion
(
	MediaAssetVersionId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MediaAssetVersion PRIMARY KEY DEFAULT NEWID(),

	MediaAssetId          UNIQUEIDENTIFIER NOT NULL
		CONSTRAINT FK_Legal_MediaAssetVersion_Asset REFERENCES POLOXI.Legal_MediaAsset (MediaAssetId),

	-- Monotonic version number within the asset (1-based). Immutable once written.
	VersionNumber         INT NOT NULL,

	-- Private storage reference (opaque key returned by the binary store). Bytes are NEVER in SQL.
	StorageKey            NVARCHAR(1000) NOT NULL,
	-- SHA-256 (or configured) hash of the stored bytes. Identifies bytes; does NOT prove authenticity.
	ContentHash           NVARCHAR(128) NOT NULL,
	ByteLength            BIGINT NOT NULL,

	-- Type-specific metadata as JSON (dimensions, codecs, sample rate, schema, timezone, etc.).
	MetadataJson          NVARCHAR(MAX) NULL,
	-- Asserted capture instant (from device/EXIF/record). Advisory; distinct from ingestion time.
	CaptureAssertedAtUtc  DATETIME2 NULL,
	IngestedAtUtc         DATETIME2 NOT NULL CONSTRAINT DF_Legal_MediaVer_Ingested DEFAULT SYSUTCDATETIME(),

	-- Duration (ms) for time-based media; width/height (px) for visual media. Null where N/A.
	DurationMs            BIGINT NULL,
	WidthPx               INT NULL,
	HeightPx              INT NULL,

	-- Standard base/audit fields.
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_MediaVer_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_MediaVer_IsDeleted DEFAULT 0,
	RowVersion            ROWVERSION,

	CONSTRAINT UQ_Legal_MediaAssetVersion UNIQUE (MediaAssetId, VersionNumber)
);

GO

-- ── 3. MediaDerivative: a generated artifact (thumbnail/preview/transcript/frame/parsed log). ──────
IF OBJECT_ID(N'POLOXI.Legal_MediaDerivative',N'U') IS NULL
CREATE TABLE POLOXI.Legal_MediaDerivative
(
	MediaDerivativeId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MediaDerivative PRIMARY KEY DEFAULT NEWID(),

	MediaAssetVersionId   UNIQUEIDENTIFIER NOT NULL
		CONSTRAINT FK_Legal_MediaDerivative_Version REFERENCES POLOXI.Legal_MediaAssetVersion (MediaAssetVersionId),
	-- The processing run that produced this derivative (null for user-provided artifacts).
	MediaProcessingRunId  UNIQUEIDENTIFIER NULL,

	-- THUMBNAIL | PREVIEW | TRANSCRIPT | FRAME | PARSED_LOG.
	DerivativeTypeCode    NVARCHAR(30) NOT NULL,
	-- Private storage reference for binary derivatives; null when payload is inline JSON/text.
	StorageKey            NVARCHAR(1000) NULL,
	MimeType              NVARCHAR(150) NULL,
	-- Inline payload (e.g. transcript segments JSON, parsed records). Null when stored as bytes.
	PayloadJson           NVARCHAR(MAX) NULL,

	-- Standard base/audit fields.
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_MediaDeriv_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_MediaDeriv_IsDeleted DEFAULT 0,
	RowVersion            ROWVERSION
);

GO

IF OBJECT_ID(N'POLOXI.IX_Legal_MediaDerivative_Version',N'IX') IS NULL
	CREATE INDEX IX_Legal_MediaDerivative_Version
		ON POLOXI.Legal_MediaDerivative (TenantId, MediaAssetVersionId) WHERE IsDeleted = 0;

GO

-- ── 4. EvidenceAnchor: a precise, type-specific source location within an asset version. ───────────
IF OBJECT_ID(N'POLOXI.Legal_EvidenceAnchor',N'U') IS NULL
CREATE TABLE POLOXI.Legal_EvidenceAnchor
(
	EvidenceAnchorId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_EvidenceAnchor PRIMARY KEY DEFAULT NEWID(),

	DecisionMatterId      UNIQUEIDENTIFIER NOT NULL,
	MediaAssetVersionId   UNIQUEIDENTIFIER NOT NULL
		CONSTRAINT FK_Legal_EvidenceAnchor_Version REFERENCES POLOXI.Legal_MediaAssetVersion (MediaAssetVersionId),

	-- IMAGE_REGION | VIDEO_INTERVAL | AUDIO_INTERVAL | STRUCTURED_RECORD.
	AnchorTypeCode        NVARCHAR(30) NOT NULL,

	-- IMAGE_REGION (and optional VIDEO frame region): normalized [0,1] rectangle.
	NormX                 DECIMAL(9,6) NULL,
	NormY                 DECIMAL(9,6) NULL,
	NormWidth             DECIMAL(9,6) NULL,
	NormHeight            DECIMAL(9,6) NULL,

	-- VIDEO_INTERVAL / AUDIO_INTERVAL: media-relative time in milliseconds (NOT real-world time).
	StartMs               BIGINT NULL,
	EndMs                 BIGINT NULL,
	-- Optional frame reference for a video interval.
	FrameNumber           BIGINT NULL,
	-- Optional audio speaker label (tentative until reviewed) + transcript segment reference.
	SpeakerLabel          NVARCHAR(100) NULL,
	TranscriptSegmentRef  NVARCHAR(200) NULL,

	-- STRUCTURED_RECORD: record ids / row range / field names / unit / coordinate reference (JSON).
	RecordReferenceJson   NVARCHAR(MAX) NULL,

	-- Standard base/audit fields.
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_EvidenceAnchor_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_EvidenceAnchor_IsDeleted DEFAULT 0,
	RowVersion            ROWVERSION
);

GO

IF OBJECT_ID(N'POLOXI.IX_Legal_EvidenceAnchor_Version',N'IX') IS NULL
	CREATE INDEX IX_Legal_EvidenceAnchor_Version
		ON POLOXI.Legal_EvidenceAnchor (TenantId, MediaAssetVersionId) WHERE IsDeleted = 0;

GO

-- ── 5. PropositionEvidenceLink: binds a parked proposition to an anchor + relationship + run. ──────
IF OBJECT_ID(N'POLOXI.Legal_PropositionEvidenceLink',N'U') IS NULL
CREATE TABLE POLOXI.Legal_PropositionEvidenceLink
(
	PropositionEvidenceLinkId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PropEvidenceLink PRIMARY KEY DEFAULT NEWID(),

	DecisionMatterId      UNIQUEIDENTIFIER NOT NULL,
	-- The parked/accepted proposition (POLOXI.Legal_RetrievedProposition from 0399).
	RetrievedPropositionId UNIQUEIDENTIFIER NOT NULL,
	EvidenceAnchorId      UNIQUEIDENTIFIER NOT NULL
		CONSTRAINT FK_Legal_PropEvidenceLink_Anchor REFERENCES POLOXI.Legal_EvidenceAnchor (EvidenceAnchorId),

	-- OBSERVATION | ATTRIBUTED_ASSERTION | PROPOSED_INTERPRETATION — how the proposal relates to the
	-- anchored source content. Qualitative; carries NO numeric outcome support.
	EvidenceRelationshipCode NVARCHAR(40) NOT NULL,
	-- The extraction run that proposed this link (null for manual annotation).
	MediaProcessingRunId  UNIQUEIDENTIFIER NULL,
	-- Confirmed | Pending | Rejected — the reviewer's confirmation of the anchor↔proposition link.
	ReviewStatusCode      NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_PropEvidenceLink_Review DEFAULT N'Pending',

	-- Standard base/audit fields.
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_PropEvidenceLink_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_PropEvidenceLink_IsDeleted DEFAULT 0,
	RowVersion            ROWVERSION
);

GO

IF OBJECT_ID(N'POLOXI.IX_Legal_PropEvidenceLink_Prop',N'IX') IS NULL
	CREATE INDEX IX_Legal_PropEvidenceLink_Prop
		ON POLOXI.Legal_PropositionEvidenceLink (TenantId, RetrievedPropositionId) WHERE IsDeleted = 0;

GO

-- ── 6. MediaProcessingRun: a processor invocation (idempotent, retry-safe) + status + outputs. ─────
IF OBJECT_ID(N'POLOXI.Legal_MediaProcessingRun',N'U') IS NULL
CREATE TABLE POLOXI.Legal_MediaProcessingRun
(
	MediaProcessingRunId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MediaProcessingRun PRIMARY KEY DEFAULT NEWID(),

	DecisionMatterId      UNIQUEIDENTIFIER NOT NULL,
	MediaAssetVersionId   UNIQUEIDENTIFIER NOT NULL
		CONSTRAINT FK_Legal_MediaProcessingRun_Version REFERENCES POLOXI.Legal_MediaAssetVersion (MediaAssetVersionId),

	-- PHOTO | AUDIO | VIDEO | GPS | DEVICE_LOG | SYSTEM_LOG processor family.
	ProcessorCode         NVARCHAR(40) NOT NULL,
	ProcessorVersion      NVARCHAR(50) NULL,

	-- Pending | Running | Completed | Failed | CapabilityUnavailable.
	StatusCode            NVARCHAR(30) NOT NULL CONSTRAINT DF_Legal_MediaRun_Status DEFAULT N'Pending',
	-- Disclosed sampling coverage (e.g. frames analyzed of total) so a run never over-claims coverage.
	CoverageJson          NVARCHAR(MAX) NULL,
	ErrorCode             NVARCHAR(100) NULL,
	ErrorMessage          NVARCHAR(2000) NULL,
	-- Idempotency key so a replayed dispatch returns the original run instead of double-processing.
	IdempotencyKey        NVARCHAR(200) NOT NULL,

	StartedAtUtc          DATETIME2 NULL,
	CompletedAtUtc        DATETIME2 NULL,

	-- Standard base/audit fields.
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_MediaRun_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_MediaRun_IsDeleted DEFAULT 0,
	RowVersion            ROWVERSION,

	CONSTRAINT UQ_Legal_MediaProcessingRun_Idem UNIQUE (TenantId, IdempotencyKey)
);

GO

IF OBJECT_ID(N'POLOXI.IX_Legal_MediaProcessingRun_Version',N'IX') IS NULL
	CREATE INDEX IX_Legal_MediaProcessingRun_Version
		ON POLOXI.Legal_MediaProcessingRun (TenantId, MediaAssetVersionId) WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
