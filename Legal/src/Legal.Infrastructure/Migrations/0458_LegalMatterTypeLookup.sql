SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0458: Broad six-factor Matter Type lookup table (Legal_MatterType).
--   0457 left Legal_DecisionMatter.MatterTypeCode holding MIXED levels: subtype codes
--   (PREMISES_LIABILITY) on bound matters and legacy free text ("Civil Litigation - Contract")
--   on unbound matters. This migration gives every column one meaning:
--     * Legal_MatterType.Code            = BROAD matter type machine code (PERSONAL_INJURY, EMPLOYMENT, ...)
--     * Legal_MatterType.Name            = display label
--     * Legal_DecisionMatter.MatterTypeId / MatterTypeCode = broad type (FK + synchronized code)
--     * Legal_DecisionMatter.DecisionDomainPackMatterTypeId / Subtype = pack vocabulary subtype
--     * Legal_DecisionDomainPack.MatterTypeCode = pack -> broad type mapping (drives the UI cascade)
--   Backfill is EXPLICIT mapping only (no fuzzy derivation); compound legacy strings
--   ("Civil Litigation - Employment / Wage & Hour Dispute") also bind the embedded pack subtype.
--   Fail-open preserved: unmapped values would stay NULL (all current values are mapped).
--   Idempotent throughout.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 1) Create the lookup table.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_MatterType') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MatterType
	(
		MatterTypeId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterType PRIMARY KEY DEFAULT NEWID(),
		Code              NVARCHAR(80)     NOT NULL,
		Name              NVARCHAR(200)    NOT NULL,
		Description       NVARCHAR(400)    NULL,
		SortOrder         INT              NOT NULL CONSTRAINT DF_Legal_MatterType_SortOrder DEFAULT 0,
		IsActive          BIT              NOT NULL CONSTRAINT DF_Legal_MatterType_IsActive DEFAULT 1,
		TenantId          UNIQUEIDENTIFIER NULL,
		CreatedDateUtc    DATETIME2        NOT NULL CONSTRAINT DF_Legal_MatterType_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId   UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc   DATETIME2        NULL,
		ModifiedByUserId  UNIQUEIDENTIFIER NULL,
		IsDeleted         BIT              NOT NULL CONSTRAINT DF_Legal_MatterType_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_MatterType_Code ON POLOXI.Legal_MatterType (Code) WHERE IsDeleted = 0;
END;

-- ---------------------------------------------------------------------------
-- 2) Seed broad matter types (global rows, TenantId NULL).
-- ---------------------------------------------------------------------------
;WITH seed AS
(
	SELECT * FROM (VALUES
		(N'PERSONAL_INJURY',       N'Personal Injury',         N'Bodily injury and tort claims (motor vehicle, premises, malpractice, product, wrongful death).', 10),
		(N'EMPLOYMENT',            N'Employment',              N'Employment disputes (termination, discrimination, wage & hour, non-compete, trade secret).',     20),
		(N'CONTRACT_DISPUTE',      N'Contract Dispute',        N'Breach of contract and commercial agreement disputes.',                                          30),
		(N'COVERAGE_DISPUTE',      N'Coverage Dispute',        N'Insurance coverage disputes between insured and carrier.',                                       40),
		(N'BAD_FAITH',             N'Bad Faith',               N'Insurer bad-faith claim handling disputes.',                                                      50),
		(N'INTELLECTUAL_PROPERTY', N'Intellectual Property',   N'Copyright, trademark, patent, and trade secret disputes outside the employment context.',         60),
		(N'REGULATORY_COMPLIANCE', N'Regulatory / Compliance', N'Regulatory enforcement and compliance matters.',                                                  70),
		(N'REAL_PROPERTY',         N'Real Property',           N'Real estate and property disputes.',                                                              80),
		(N'CIVIL_LITIGATION',      N'Civil Litigation',        N'General civil litigation not covered by a more specific matter type.',                            90)
	) v(Code, Name, Description, SortOrder)
)
INSERT POLOXI.Legal_MatterType (MatterTypeId, Code, Name, Description, SortOrder, IsActive, TenantId, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), s.Code, s.Name, s.Description, s.SortOrder, 1, NULL, @Now, @SystemUserId, 0
FROM seed s
WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterType x WHERE x.Code = s.Code AND x.IsDeleted = 0);

-- ---------------------------------------------------------------------------
-- 3) Matter FK column + pack -> broad type mapping column.
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'MatterTypeId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD MatterTypeId UNIQUEIDENTIFIER NULL
		CONSTRAINT FK_Legal_DecisionMatter_MatterType REFERENCES POLOXI.Legal_MatterType (MatterTypeId);

IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPack', N'MatterTypeCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDomainPack ADD MatterTypeCode NVARCHAR(80) NULL;

-- Pack mapping: PracticeAreaCode already equals the broad type code for both live packs.
UPDATE p
   SET p.MatterTypeCode = t.Code,
	   p.ModifiedDateUtc = @Now,
	   p.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionDomainPack p
  JOIN POLOXI.Legal_MatterType t ON t.Code = p.PracticeAreaCode AND t.IsDeleted = 0
 WHERE p.IsDeleted = 0
   AND ISNULL(p.MatterTypeCode, N'') COLLATE Latin1_General_CS_AS <> t.Code;

COMMIT TRANSACTION;
BEGIN TRANSACTION;

-- ---------------------------------------------------------------------------
-- 4) Explicit backfill map: legacy MatterTypeCode text -> broad type code
--    (+ optional embedded pack subtype code to bind the normalized FK).
--    Covers every live value observed; unmapped future values stay NULL (fail-open).
-- ---------------------------------------------------------------------------
DECLARE @MatterMap TABLE (OldText NVARCHAR(200) NOT NULL PRIMARY KEY, TypeCode NVARCHAR(80) NOT NULL, SubtypeCode NVARCHAR(120) NULL);
INSERT @MatterMap (OldText, TypeCode, SubtypeCode) VALUES
-- Bound matters (0457 wrote subtype-level codes into the broad column; lift them back up).
(N'PREMISES_LIABILITY',     N'PERSONAL_INJURY', NULL),
(N'WRONGFUL_DEATH',         N'PERSONAL_INJURY', NULL),
(N'MOTOR_VEHICLE_ACCIDENT', N'PERSONAL_INJURY', NULL),
(N'PEDESTRIAN_ACCIDENT',    N'PERSONAL_INJURY', NULL),
(N'MEDICAL_MALPRACTICE',    N'PERSONAL_INJURY', NULL),
-- Unbound legacy free text.
(N'Personal Injury',                                          N'PERSONAL_INJURY',       NULL),
(N'Civil Litigation - Employment / Wage & Hour Dispute',      N'EMPLOYMENT',            N'WAGE_HOUR_DISPUTE'),
(N'Civil Litigation - Employment / Non-Compete Enforcement',  N'EMPLOYMENT',            N'NON_COMPETE_ENFORCEMENT'),
(N'Civil Litigation - Employment / Trade Secret Dispute',     N'EMPLOYMENT',            N'TRADE_SECRET_DISPUTE'),
(N'Civil Litigation - Contract',                              N'CONTRACT_DISPUTE',      NULL),
(N'Civil Litigation - Contract / Commercial Dispute',         N'CONTRACT_DISPUTE',      NULL),
(N'Civil Litigation - Insurance Coverage',                    N'COVERAGE_DISPUTE',      NULL),
(N'Coverage Dispute',                                         N'COVERAGE_DISPUTE',      NULL),
(N'Bad Faith',                                                N'BAD_FAITH',             NULL),
(N'Regulatory / Compliance',                                  N'REGULATORY_COMPLIANCE', NULL),
-- Posture-like legacy values: generic civil litigation; posture fields keep the motion detail.
(N'Motion to Dismiss',                                        N'CIVIL_LITIGATION',      NULL),
(N'Summary Judgment',                                         N'CIVIL_LITIGATION',      NULL);

-- 4a) Broad type FK + synchronized code on every mapped matter.
UPDATE m
   SET m.MatterTypeId = t.MatterTypeId,
	   m.MatterTypeCode = t.Code,
	   m.ModifiedDateUtc = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN @MatterMap mm ON LOWER(LTRIM(RTRIM(m.MatterTypeCode))) = LOWER(mm.OldText)
  JOIN POLOXI.Legal_MatterType t ON t.Code = mm.TypeCode AND t.IsDeleted = 0
 WHERE m.IsDeleted = 0
   AND (m.MatterTypeId IS NULL OR ISNULL(m.MatterTypeCode, N'') COLLATE Latin1_General_CS_AS <> t.Code);

-- 4b) Bind embedded subtypes extracted from compound legacy strings (only where still unbound).
--     Resolve the subtype inside the pack mapped to the broad type; global or own-tenant vocabulary.
UPDATE m
   SET m.DecisionDomainPackMatterTypeId = mt.DecisionDomainPackMatterTypeId,
	   m.DomainPackCode = p.PackCode,
	   m.Subtype = mt.Name,
	   m.ModifiedDateUtc = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN POLOXI.Legal_MatterType t ON t.MatterTypeId = m.MatterTypeId
  JOIN @MatterMap mm ON mm.TypeCode = t.Code AND mm.SubtypeCode IS NOT NULL
	 AND LOWER(LTRIM(RTRIM(ISNULL(m.Subtype, N'')))) = LOWER(N'Employment / ' + REPLACE(REPLACE(LOWER(mm.SubtypeCode), N'_', N' '), N'  ', N' '))
  JOIN POLOXI.Legal_DecisionDomainPack p ON p.MatterTypeCode = t.Code AND p.IsDeleted = 0 AND p.IsActive = 1
  JOIN POLOXI.Legal_DecisionDomainPackMatterType mt
	ON mt.DecisionDomainPackId = p.DecisionDomainPackId AND mt.IsDeleted = 0 AND mt.IsActive = 1
   AND mt.MatterTypeCode = mm.SubtypeCode
   AND (mt.TenantId IS NULL OR mt.TenantId = m.TenantId)
 WHERE m.IsDeleted = 0
   AND m.DecisionDomainPackMatterTypeId IS NULL;

-- 4b-fallback) The subtype-string equality above is fragile; bind directly by map row where the
-- matter's ORIGINAL compound subtype text ends with the vocabulary display name (exact suffix).
UPDATE m
   SET m.DecisionDomainPackMatterTypeId = mt.DecisionDomainPackMatterTypeId,
	   m.DomainPackCode = p.PackCode,
	   m.Subtype = mt.Name,
	   m.ModifiedDateUtc = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN POLOXI.Legal_MatterType t ON t.MatterTypeId = m.MatterTypeId
  JOIN POLOXI.Legal_DecisionDomainPack p ON p.MatterTypeCode = t.Code AND p.IsDeleted = 0 AND p.IsActive = 1
  JOIN POLOXI.Legal_DecisionDomainPackMatterType mt
	ON mt.DecisionDomainPackId = p.DecisionDomainPackId AND mt.IsDeleted = 0 AND mt.IsActive = 1
   AND m.Subtype IS NOT NULL
   AND LOWER(LTRIM(RTRIM(m.Subtype))) LIKE N'%' + LOWER(mt.Name)
 WHERE m.IsDeleted = 0
   AND m.DecisionDomainPackMatterTypeId IS NULL;

-- 4c) Already-bound matters: make DomainPackCode consistent with the owning pack (safety resync).
UPDATE m
   SET m.DomainPackCode = p.PackCode,
	   m.ModifiedDateUtc = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN POLOXI.Legal_DecisionDomainPackMatterType mt ON mt.DecisionDomainPackMatterTypeId = m.DecisionDomainPackMatterTypeId
  JOIN POLOXI.Legal_DecisionDomainPack p ON p.DecisionDomainPackId = mt.DecisionDomainPackId
 WHERE m.IsDeleted = 0
   AND ISNULL(m.DomainPackCode, N'') COLLATE Latin1_General_CS_AS <> p.PackCode;

COMMIT TRANSACTION;
