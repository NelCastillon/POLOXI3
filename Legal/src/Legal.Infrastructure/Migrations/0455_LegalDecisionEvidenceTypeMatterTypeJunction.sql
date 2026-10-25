SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0455: Normalize Evidence Type <-> Matter Type into a real relational model.
--   Replaces the denormalized comma-separated Legal_DecisionDomainPackEvidenceType.MatterTypeCodes
--   (0433/0437) as the authoritative linkage. The string column is DEPRECATED but kept in sync
--   for one release (rollback safety); readers must migrate to the junction table.
--
--   Schema added:
--     1) POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType — M:M junction with GUID FKs to
--        Legal_DecisionDomainPackEvidenceType and Legal_DecisionDomainPackMatterType.
--        ABSENCE OF ROWS = evidence type applies to EVERY matter type (fail-open, mirrors the
--        incident-family and 0414 NULL/'*' conventions). Tagging is a restriction, never a bonus.
--     2) POLOXI.Legal_DecisionMatter.DecisionDomainPackMatterTypeId — authoritative FK binding the
--        matter to its pack matter-type vocabulary row (free-text MatterTypeCode kept for display).
--
--   Backfill strategy (best-value seeding, DB-backed only):
--     A) Exact-token pass: split existing comma MatterTypeCodes and join each token to the owning
--        pack's Legal_DecisionDomainPackMatterType.MatterTypeCode (preserves curated 0437-0439 data).
--     B) Pattern re-derivation pass: re-apply the 0437 EvidenceTypeCode->MatterType LIKE map directly
--        into the junction so rows the string update missed (NULL / 400-char truncation) are complete.
--     C) Untagged rows remain untagged = universally applicable. No blanket tagging.
--     D) Matter FK resolution: exact match on MatterTypeCode, then Subtype, then ClaimTypeCode within
--        the matter's own DomainPackCode vocabulary; ambiguous/unmatched matters stay NULL = no filter.
--   Idempotent: table/column guards + NOT EXISTS inserts; safe to re-run.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 1) Junction table.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType
	(
		DecisionDomainPackEvidenceTypeMatterTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackEvidenceTypeMatterType PRIMARY KEY DEFAULT NEWID(),
		DecisionDomainPackEvidenceTypeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DDPEvidenceTypeMatterType_EvidenceType REFERENCES POLOXI.Legal_DecisionDomainPackEvidenceType (DecisionDomainPackEvidenceTypeId),
		DecisionDomainPackMatterTypeId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DDPEvidenceTypeMatterType_MatterType   REFERENCES POLOXI.Legal_DecisionDomainPackMatterType (DecisionDomainPackMatterTypeId),
		TenantId          UNIQUEIDENTIFIER NULL,
		CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Legal_DDPEvidenceTypeMatterType_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId   UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc   DATETIME2 NULL,
		ModifiedByUserId  UNIQUEIDENTIFIER NULL,
		IsDeleted         BIT NOT NULL CONSTRAINT DF_Legal_DDPEvidenceTypeMatterType_IsDeleted DEFAULT 0,
		CONSTRAINT UQ_Legal_DDPEvidenceTypeMatterType UNIQUE (DecisionDomainPackEvidenceTypeId, DecisionDomainPackMatterTypeId)
	);

	CREATE INDEX IX_Legal_DDPEvidenceTypeMatterType_MatterType
		ON POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType (DecisionDomainPackMatterTypeId)
		INCLUDE (DecisionDomainPackEvidenceTypeId) WHERE IsDeleted = 0;
END;

GO

-- ---------------------------------------------------------------------------
-- 2) Matter FK to the authoritative pack matter-type vocabulary row.
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'DecisionDomainPackMatterTypeId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter
		ADD DecisionDomainPackMatterTypeId UNIQUEIDENTIFIER NULL
			CONSTRAINT FK_Legal_DecisionMatter_DomainPackMatterType REFERENCES POLOXI.Legal_DecisionDomainPackMatterType (DecisionDomainPackMatterTypeId);

GO

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 3A) Backfill pass A — exact tokens from the existing comma-separated MatterTypeCodes column,
--     joined to the OWNING pack's matter-type vocabulary (case-insensitive exact match).
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackEvidenceType', N'MatterTypeCodes') IS NOT NULL
BEGIN
	INSERT INTO POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType
		(DecisionDomainPackEvidenceTypeId, DecisionDomainPackMatterTypeId, TenantId, CreatedDateUtc, CreatedByUserId)
	SELECT DISTINCT e.DecisionDomainPackEvidenceTypeId, mt.DecisionDomainPackMatterTypeId, e.TenantId, @Now, @SystemUserId
	FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
	CROSS APPLY STRING_SPLIT(e.MatterTypeCodes, N',') s
	JOIN POLOXI.Legal_DecisionDomainPackMatterType mt
	  ON mt.DecisionDomainPackId = e.DecisionDomainPackId
	 AND mt.IsDeleted = 0 AND mt.IsActive = 1
	 AND LOWER(LTRIM(RTRIM(mt.MatterTypeCode))) = LOWER(LTRIM(RTRIM(s.value)))
	WHERE e.IsDeleted = 0
	  AND NULLIF(LTRIM(RTRIM(e.MatterTypeCodes)), N'') IS NOT NULL
	  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j
						WHERE j.DecisionDomainPackEvidenceTypeId = e.DecisionDomainPackEvidenceTypeId
						  AND j.DecisionDomainPackMatterTypeId = mt.DecisionDomainPackMatterTypeId);
END;

-- ---------------------------------------------------------------------------
-- 3B) Backfill pass B — re-derive subtype-specific tags from the 0437 EvidenceTypeCode->MatterType
--     LIKE pattern map, directly into the junction (covers rows the 400-char string missed).
--     Only unambiguously subtype-specific evidence codes are tagged; everything else stays
--     untagged = applies to every matter type.
-- ---------------------------------------------------------------------------
DECLARE @Narrow TABLE (Pattern NVARCHAR(120) NOT NULL, MatterTypeLike NVARCHAR(200) NOT NULL);
INSERT @Narrow (Pattern, MatterTypeLike) VALUES
(N'%VEHICLE%',           N'%Vehicle%'),(N'%VEHICLE%',N'%Truck%'),(N'%VEHICLE%',N'%Motorcycle%'),(N'%VEHICLE%',N'%Rideshare%'),(N'%VEHICLE%',N'%Pedestrian%'),(N'%VEHICLE%',N'%Bicycle%'),
(N'DMV_%',               N'%Vehicle%'),(N'DMV_%',N'%Truck%'),(N'DMV_%',N'%Motorcycle%'),(N'DMV_%',N'%Rideshare%'),(N'DMV_%',N'%Pedestrian%'),(N'DMV_%',N'%Bicycle%'),
(N'EVENT_DATA_RECORDER', N'%Vehicle%'),(N'EVENT_DATA_RECORDER',N'%Truck%'),(N'EVENT_DATA_RECORDER',N'%Rideshare%'),
(N'DELTA_V_ANALYSIS',    N'%Vehicle%'),(N'DELTA_V_ANALYSIS',N'%Truck%'),(N'DELTA_V_ANALYSIS',N'%Motorcycle%'),
(N'DOT_%',               N'%Truck%'),(N'DOT_%',N'%Commercial Vehicle%'),
(N'%PRODUCT%',           N'%Product Liability%'),(N'%PRODUCT%',N'%Mass Tort%'),
(N'RECALL_NOTICE',       N'%Product Liability%'),(N'RECALL_NOTICE',N'%Mass Tort%'),
(N'DESIGN_SPECIFICATION',N'%Product Liability%'),
(N'WARNING_LABEL_INSTRUCTIONS',N'%Product Liability%'),
(N'%PREMISES%',          N'%Premises%'),(N'%PREMISES%',N'%Slip and Fall%'),(N'%PREMISES%',N'%Negligent Security%'),
(N'SWEEP_INSPECTION_LOG',N'%Premises%'),(N'SWEEP_INSPECTION_LOG',N'%Slip and Fall%'),
(N'OPEN_OBVIOUS_PHOTO',  N'%Premises%'),(N'OPEN_OBVIOUS_PHOTO',N'%Slip and Fall%'),
(N'FOOTWEAR_ATTIRE_PHOTO',N'%Premises%'),(N'FOOTWEAR_ATTIRE_PHOTO',N'%Slip and Fall%'),
(N'WARNING_SIGN_PHOTO',  N'%Premises%'),(N'WARNING_SIGN_PHOTO',N'%Slip and Fall%'),
(N'SECURITY_STAFFING_RECORD',N'%Negligent Security%'),(N'SECURITY_STAFFING_RECORD',N'%Premises%'),
(N'AUTOPSY_REPORT',      N'%Wrongful Death%'),(N'DEATH_CERTIFICATE',N'%Wrongful Death%'),(N'SUCCESSOR_IN_INTEREST_DECLARATION',N'%Wrongful Death%'),
(N'%OSHA%',              N'%Construction%'),(N'%OSHA%',N'%Workplace%'),
(N'WORKERS_COMP_LIEN_NOTICE',N'%Construction%'),(N'WORKERS_COMP_LIEN_NOTICE',N'%Workplace%'),
(N'%EXPOSURE%',          N'%Toxic Tort%'),(N'%EXPOSURE%',N'%Mass Tort%'),
(N'%NURSING%',           N'%Nursing Home%'),(N'%ELDER%',N'%Nursing Home%'),
(N'%DRAM%',              N'%Dram Shop%'),(N'%ALCOHOL%',N'%Dram Shop%'),(N'%LIQUOR%',N'%Dram Shop%'),
(N'%ANIMAL%',            N'%Dog Bite%'),(N'%DOG%',N'%Dog Bite%'),
(N'%MALPRACTICE%',       N'%Medical Malpractice%'),(N'%MALPRACTICE%',N'%Mass Tort%');

INSERT INTO POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType
	(DecisionDomainPackEvidenceTypeId, DecisionDomainPackMatterTypeId, TenantId, CreatedDateUtc, CreatedByUserId)
SELECT DISTINCT e.DecisionDomainPackEvidenceTypeId, mt.DecisionDomainPackMatterTypeId, e.TenantId, @Now, @SystemUserId
FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
JOIN @Narrow n ON e.EvidenceTypeCode LIKE n.Pattern
JOIN POLOXI.Legal_DecisionDomainPackMatterType mt
  ON mt.DecisionDomainPackId = e.DecisionDomainPackId
 AND mt.IsDeleted = 0 AND mt.IsActive = 1
 AND mt.MatterTypeCode LIKE n.MatterTypeLike
WHERE e.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j
					WHERE j.DecisionDomainPackEvidenceTypeId = e.DecisionDomainPackEvidenceTypeId
					  AND j.DecisionDomainPackMatterTypeId = mt.DecisionDomainPackMatterTypeId);

-- ---------------------------------------------------------------------------
-- 3C) Keep the DEPRECATED string column synchronized FROM the junction (junction is now the
--     source of truth). Rows with no junction tags get NULL (= universal).
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackEvidenceType', N'MatterTypeCodes') IS NOT NULL
BEGIN
	;WITH agg AS
	(
		SELECT j.DecisionDomainPackEvidenceTypeId,
			   STRING_AGG(mt.MatterTypeCode, N',') WITHIN GROUP (ORDER BY mt.SortOrder, mt.MatterTypeCode) AS Codes
		FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j
		JOIN POLOXI.Legal_DecisionDomainPackMatterType mt ON mt.DecisionDomainPackMatterTypeId = j.DecisionDomainPackMatterTypeId
		WHERE j.IsDeleted = 0 AND mt.IsDeleted = 0
		GROUP BY j.DecisionDomainPackEvidenceTypeId
	)
	UPDATE e
	   SET e.MatterTypeCodes = LEFT(a.Codes, 400),
		   e.ModifiedDateUtc = @Now,
		   e.ModifiedByUserId = @SystemUserId
	  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
	  JOIN agg a ON a.DecisionDomainPackEvidenceTypeId = e.DecisionDomainPackEvidenceTypeId
	 WHERE ISNULL(e.MatterTypeCodes, N'') <> LEFT(a.Codes, 400);
END;

-- ---------------------------------------------------------------------------
-- 3D) Matter FK resolution: exact match MatterTypeCode -> Subtype -> ClaimTypeCode within the
--     matter's own DomainPackCode vocabulary. Unresolved/ambiguous matters stay NULL (no filter).
-- ---------------------------------------------------------------------------
;WITH candidates AS
(
	SELECT m.DecisionMatterId,
		   mt.DecisionDomainPackMatterTypeId,
		   ROW_NUMBER() OVER (PARTITION BY m.DecisionMatterId ORDER BY
				CASE WHEN LOWER(LTRIM(RTRIM(m.MatterTypeCode))) = LOWER(mt.MatterTypeCode) THEN 0
					 WHEN LOWER(LTRIM(RTRIM(m.Subtype)))        = LOWER(mt.MatterTypeCode) THEN 1
					 WHEN LOWER(LTRIM(RTRIM(m.ClaimTypeCode)))  = LOWER(mt.MatterTypeCode) THEN 2
					 ELSE 3 END, mt.SortOrder) AS rn,
		   CASE WHEN LOWER(LTRIM(RTRIM(m.MatterTypeCode))) = LOWER(mt.MatterTypeCode)
				  OR LOWER(LTRIM(RTRIM(m.Subtype)))        = LOWER(mt.MatterTypeCode)
				  OR LOWER(LTRIM(RTRIM(m.ClaimTypeCode)))  = LOWER(mt.MatterTypeCode)
				THEN 1 ELSE 0 END AS IsMatch
	FROM POLOXI.Legal_DecisionMatter m
	JOIN POLOXI.Legal_DecisionDomainPack p
	  ON p.PackCode = m.DomainPackCode AND p.IsDeleted = 0
	JOIN POLOXI.Legal_DecisionDomainPackMatterType mt
	  ON mt.DecisionDomainPackId = p.DecisionDomainPackId AND mt.IsDeleted = 0 AND mt.IsActive = 1
	WHERE m.IsDeleted = 0
	  AND m.DecisionDomainPackMatterTypeId IS NULL
	  AND m.DomainPackCode IS NOT NULL
)
UPDATE m
   SET m.DecisionDomainPackMatterTypeId = c.DecisionDomainPackMatterTypeId,
	   m.ModifiedDateUtc = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN candidates c ON c.DecisionMatterId = m.DecisionMatterId AND c.rn = 1 AND c.IsMatch = 1;

GO

COMMIT TRANSACTION;
