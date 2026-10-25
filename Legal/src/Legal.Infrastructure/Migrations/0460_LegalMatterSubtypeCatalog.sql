SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0460: Canonical MatterType -> Subtype catalog (Legal_MatterSubtype).
--   Why: subtypes previously existed ONLY inside a domain pack vocabulary
--   (Legal_DecisionDomainPackMatterType), so a broad matter type without a pack
--   (CONTRACT_DISPUTE, COVERAGE_DISPUTE, BAD_FAITH, ...) could never have subtypes.
--   This migration introduces a first-class catalog owned by the broad type:
--     * Legal_MatterSubtype.MatterTypeId  = FK to Legal_MatterType (broad)
--     * Legal_MatterSubtype.Code          = SUBTYPE machine code (unique per broad type)
--     * Legal_MatterSubtype.Name          = display label
--   Links:
--     * Legal_DecisionDomainPackMatterType.MatterSubtypeId -> catalog row (pack vocabulary
--       becomes a pack-scoped projection of the canonical subtype; L2 gating unchanged)
--     * Legal_DecisionMatter.MatterSubtypeId -> catalog row (direct binding that also works
--       for packless broad types)
--   Seeding:
--     1) Every existing pack subtype row is lifted into the catalog under its pack's broad
--        type (pack -> Legal_DecisionDomainPack.MatterTypeCode -> Legal_MatterType).
--     2) Starter subtype sets are seeded for packless broad types so the UI cascade works
--        for every broad type (these can be managed/extended later).
--   Backfill: matters with a bound pack subtype get MatterSubtypeId via the link.
--   Idempotent throughout.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 1) Create the catalog table.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_MatterSubtype') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MatterSubtype
	(
		MatterSubtypeId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterSubtype PRIMARY KEY DEFAULT NEWID(),
		MatterTypeId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterSubtype_MatterType REFERENCES POLOXI.Legal_MatterType (MatterTypeId),
		Code              NVARCHAR(120)    NOT NULL,
		Name              NVARCHAR(200)    NOT NULL,
		Description       NVARCHAR(400)    NULL,
		SortOrder         INT              NOT NULL CONSTRAINT DF_Legal_MatterSubtype_SortOrder DEFAULT 0,
		IsActive          BIT              NOT NULL CONSTRAINT DF_Legal_MatterSubtype_IsActive DEFAULT 1,
		TenantId          UNIQUEIDENTIFIER NULL,
		CreatedDateUtc    DATETIME2        NOT NULL CONSTRAINT DF_Legal_MatterSubtype_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId   UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc   DATETIME2        NULL,
		ModifiedByUserId  UNIQUEIDENTIFIER NULL,
		IsDeleted         BIT              NOT NULL CONSTRAINT DF_Legal_MatterSubtype_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_MatterSubtype_TypeCode ON POLOXI.Legal_MatterSubtype (MatterTypeId, Code) WHERE IsDeleted = 0;
	CREATE INDEX IX_Legal_MatterSubtype_Type ON POLOXI.Legal_MatterSubtype (MatterTypeId) WHERE IsDeleted = 0;
END;

-- ---------------------------------------------------------------------------
-- 2) Link columns.
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackMatterType', N'MatterSubtypeId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDomainPackMatterType ADD MatterSubtypeId UNIQUEIDENTIFIER NULL
		CONSTRAINT FK_Legal_DDPMatterType_MatterSubtype REFERENCES POLOXI.Legal_MatterSubtype (MatterSubtypeId);

IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'MatterSubtypeId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD MatterSubtypeId UNIQUEIDENTIFIER NULL
		CONSTRAINT FK_Legal_DecisionMatter_MatterSubtype REFERENCES POLOXI.Legal_MatterSubtype (MatterSubtypeId);

COMMIT TRANSACTION;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 3) Seed pass 1: lift every existing pack subtype into the catalog under its
--    pack's broad type (pack.MatterTypeCode -> Legal_MatterType.Code).
-- ---------------------------------------------------------------------------
INSERT POLOXI.Legal_MatterSubtype (MatterSubtypeId, MatterTypeId, Code, Name, Description, SortOrder, IsActive, TenantId, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), t.MatterTypeId, mt.SubtypeCode, mt.Name, mt.Description, mt.SortOrder, mt.IsActive, mt.TenantId, @Now, @SystemUserId, 0
FROM POLOXI.Legal_DecisionDomainPackMatterType mt
JOIN POLOXI.Legal_DecisionDomainPack p ON p.DecisionDomainPackId = mt.DecisionDomainPackId AND p.IsDeleted = 0
JOIN POLOXI.Legal_MatterType t ON t.Code = p.MatterTypeCode AND t.IsDeleted = 0
WHERE mt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterSubtype s
				   WHERE s.MatterTypeId = t.MatterTypeId AND s.Code = mt.SubtypeCode AND s.IsDeleted = 0);

-- Link pack vocabulary rows to their canonical catalog row.
UPDATE mt
   SET mt.MatterSubtypeId  = s.MatterSubtypeId,
	   mt.ModifiedDateUtc  = @Now,
	   mt.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionDomainPackMatterType mt
  JOIN POLOXI.Legal_DecisionDomainPack p ON p.DecisionDomainPackId = mt.DecisionDomainPackId AND p.IsDeleted = 0
  JOIN POLOXI.Legal_MatterType t ON t.Code = p.MatterTypeCode AND t.IsDeleted = 0
  JOIN POLOXI.Legal_MatterSubtype s ON s.MatterTypeId = t.MatterTypeId AND s.Code = mt.SubtypeCode AND s.IsDeleted = 0
 WHERE mt.IsDeleted = 0
   AND mt.MatterSubtypeId IS NULL;

-- ---------------------------------------------------------------------------
-- 4) Seed pass 2: starter subtype sets for packless broad types so every broad
--    type can cascade. Managed data; extend via admin tooling later.
-- ---------------------------------------------------------------------------
;WITH seed AS
(
	SELECT * FROM (VALUES
		(N'CONTRACT_DISPUTE', N'BREACH_OF_CONTRACT',        N'Breach of Contract',          10),
		(N'CONTRACT_DISPUTE', N'WARRANTY_DISPUTE',          N'Warranty Dispute',            20),
		(N'CONTRACT_DISPUTE', N'LIMITATION_OF_LIABILITY',   N'Limitation of Liability',     30),
		(N'CONTRACT_DISPUTE', N'INDEMNIFICATION_DISPUTE',   N'Indemnification Dispute',     40),
		(N'CONTRACT_DISPUTE', N'COMMERCIAL_AGREEMENT',      N'Commercial Agreement Dispute',50),
		(N'COVERAGE_DISPUTE', N'DUTY_TO_DEFEND',            N'Duty to Defend',              10),
		(N'COVERAGE_DISPUTE', N'DUTY_TO_INDEMNIFY',         N'Duty to Indemnify',           20),
		(N'COVERAGE_DISPUTE', N'POLICY_EXCLUSION',          N'Policy Exclusion',            30),
		(N'COVERAGE_DISPUTE', N'RESCISSION_MISREPRESENTATION', N'Rescission / Misrepresentation', 40),
		(N'BAD_FAITH',        N'FAILURE_TO_SETTLE',         N'Failure to Settle',           10),
		(N'BAD_FAITH',        N'CLAIM_DELAY_DENIAL',        N'Claim Delay / Denial',        20),
		(N'BAD_FAITH',        N'UNFAIR_CLAIMS_PRACTICES',   N'Unfair Claims Practices',     30),
		(N'INTELLECTUAL_PROPERTY', N'COPYRIGHT_INFRINGEMENT',  N'Copyright Infringement',   10),
		(N'INTELLECTUAL_PROPERTY', N'TRADEMARK_INFRINGEMENT',  N'Trademark Infringement',   20),
		(N'INTELLECTUAL_PROPERTY', N'PATENT_INFRINGEMENT',     N'Patent Infringement',      30),
		(N'INTELLECTUAL_PROPERTY', N'TRADE_SECRET_MISAPPROPRIATION', N'Trade Secret Misappropriation', 40),
		(N'REGULATORY_COMPLIANCE', N'AGENCY_ENFORCEMENT',      N'Agency Enforcement Action',10),
		(N'REGULATORY_COMPLIANCE', N'LICENSING_DISPUTE',       N'Licensing Dispute',        20),
		(N'REGULATORY_COMPLIANCE', N'ADMINISTRATIVE_APPEAL',   N'Administrative Appeal',    30),
		(N'REAL_PROPERTY',    N'QUIET_TITLE',               N'Quiet Title',                 10),
		(N'REAL_PROPERTY',    N'BOUNDARY_EASEMENT',         N'Boundary / Easement Dispute', 20),
		(N'REAL_PROPERTY',    N'LANDLORD_TENANT',           N'Landlord / Tenant Dispute',   30),
		(N'REAL_PROPERTY',    N'CONSTRUCTION_DEFECT',       N'Construction Defect',         40),
		(N'CIVIL_LITIGATION', N'GENERAL_TORT',              N'General Tort',                10),
		(N'CIVIL_LITIGATION', N'BUSINESS_TORT',             N'Business Tort',               20),
		(N'CIVIL_LITIGATION', N'FIDUCIARY_DUTY',            N'Breach of Fiduciary Duty',    30),
		(N'CIVIL_LITIGATION', N'DECLARATORY_JUDGMENT',      N'Declaratory Judgment',        40)
	) v(TypeCode, Code, Name, SortOrder)
)
INSERT POLOXI.Legal_MatterSubtype (MatterSubtypeId, MatterTypeId, Code, Name, Description, SortOrder, IsActive, TenantId, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), t.MatterTypeId, s.Code, s.Name, NULL, s.SortOrder, 1, NULL, @Now, @SystemUserId, 0
FROM seed s
JOIN POLOXI.Legal_MatterType t ON t.Code = s.TypeCode AND t.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterSubtype x
				   WHERE x.MatterTypeId = t.MatterTypeId AND x.Code = s.Code AND x.IsDeleted = 0);

-- ---------------------------------------------------------------------------
-- 5) Backfill matters: bound pack subtype -> canonical MatterSubtypeId.
-- ---------------------------------------------------------------------------
UPDATE m
   SET m.MatterSubtypeId  = mt.MatterSubtypeId,
	   m.ModifiedDateUtc  = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN POLOXI.Legal_DecisionDomainPackMatterType mt
	ON mt.DecisionDomainPackMatterTypeId = m.DecisionDomainPackMatterTypeId AND mt.IsDeleted = 0
 WHERE m.IsDeleted = 0
   AND m.MatterSubtypeId IS NULL
   AND mt.MatterSubtypeId IS NOT NULL;

COMMIT TRANSACTION;

-- ---------------------------------------------------------------------------
-- 6) Verification.
-- ---------------------------------------------------------------------------
SELECT t.Code AS MatterType, COUNT(s.MatterSubtypeId) AS Subtypes
  FROM POLOXI.Legal_MatterType t
  LEFT JOIN POLOXI.Legal_MatterSubtype s ON s.MatterTypeId = t.MatterTypeId AND s.IsDeleted = 0
 WHERE t.IsDeleted = 0
 GROUP BY t.Code, t.SortOrder ORDER BY t.SortOrder;
SELECT COUNT(*) AS PackRowsLinked FROM POLOXI.Legal_DecisionDomainPackMatterType WHERE IsDeleted = 0 AND MatterSubtypeId IS NOT NULL;
SELECT COUNT(*) AS MattersWithSubtype FROM POLOXI.Legal_DecisionMatter WHERE IsDeleted = 0 AND MatterSubtypeId IS NOT NULL;
