SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0459: Disambiguate the subtype-level code column + targeted matter backfill.
--   The pack vocabulary table Legal_DecisionDomainPackMatterType reused the column
--   name "MatterTypeCode" for SUBTYPE machine codes (MOTOR_VEHICLE_ACCIDENT, ...),
--   colliding with the BROAD columns of the same name introduced by 0458
--   (Legal_MatterType.Code, Legal_DecisionMatter.MatterTypeCode,
--   Legal_DecisionDomainPack.MatterTypeCode). Every column gets ONE meaning:
--     * Legal_DecisionDomainPackMatterType.SubtypeCode = pack SUBTYPE machine code (renamed)
--     * All other MatterTypeCode columns              = BROAD matter type code (unchanged)
--   NO new table and NO new vocabulary seed rows: rename only; all FKs/junction rows keep working.
--
--   Backfill (explicit, no fuzzy derivation):
--     A) Two unambiguous PI matters are bound to their pack subtype:
--          Sapini MVA            -> MOTOR_VEHICLE_ACCIDENT
--          Priya Sharma Trucking -> TRUCKING_COMMERCIAL_VEHICLE
--     B) PI/EMPLOYMENT matters carrying bogus random-generator Subtype free text that does not
--        belong to their broad type get Subtype = NULL (fail-open; no invented bindings).
--     C) Matters whose broad type has no domain pack (CONTRACT_DISPUTE, COVERAGE_DISPUTE,
--        BAD_FAITH, CIVIL_LITIGATION, REGULATORY_COMPLIANCE) are intentionally left unbound.
--   Idempotent throughout.
-- ============================================================================
BEGIN TRANSACTION;

-- ---------------------------------------------------------------------------
-- 1) Rename the subtype code column (idempotent).
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackMatterType', N'SubtypeCode') IS NULL
   AND COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackMatterType', N'MatterTypeCode') IS NOT NULL
	EXEC sp_rename N'POLOXI.Legal_DecisionDomainPackMatterType.MatterTypeCode', N'SubtypeCode', N'COLUMN';

COMMIT TRANSACTION;
GO
-- New batch: statements below compile only after the rename is visible.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 2A) Bind unambiguous matters to their pack subtype (explicit map by MatterId).
-- ---------------------------------------------------------------------------
DECLARE @Bind TABLE (DecisionMatterId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, PackCode NVARCHAR(60) NOT NULL, SubtypeCode NVARCHAR(120) NOT NULL);
INSERT @Bind (DecisionMatterId, PackCode, SubtypeCode) VALUES
('83EA6EF4-66CB-4DCB-BFA2-81A90F1C7A9B', N'PERSONAL_INJURY', N'MOTOR_VEHICLE_ACCIDENT'),      -- Sapini, Justin - MVA
('62F4DDC1-5327-4F78-875A-649AC5F7CA8C', N'PERSONAL_INJURY', N'TRUCKING_COMMERCIAL_VEHICLE'); -- Priya Sharma - Trucking Accident

UPDATE m
   SET m.DecisionDomainPackMatterTypeId = mt.DecisionDomainPackMatterTypeId,
	   m.DomainPackCode   = p.PackCode,
	   m.Subtype          = mt.Name,
	   m.ModifiedDateUtc  = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN @Bind b ON b.DecisionMatterId = m.DecisionMatterId
  JOIN POLOXI.Legal_DecisionDomainPack p ON p.PackCode = b.PackCode AND p.IsDeleted = 0
  JOIN POLOXI.Legal_DecisionDomainPackMatterType mt
	ON mt.DecisionDomainPackId = p.DecisionDomainPackId AND mt.SubtypeCode = b.SubtypeCode
   AND mt.IsDeleted = 0 AND mt.IsActive = 1
 WHERE m.IsDeleted = 0
   AND m.DecisionDomainPackMatterTypeId IS NULL;

-- ---------------------------------------------------------------------------
-- 2B) Clear bogus random-generator Subtype free text on matters whose broad type HAS a pack
--     but whose Subtype does not match any subtype Name in that pack (pollutes LLM context).
--     Bound matters (FK set) are never touched.
-- ---------------------------------------------------------------------------
UPDATE m
   SET m.Subtype          = NULL,
	   m.ModifiedDateUtc  = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN POLOXI.Legal_DecisionDomainPack p
	ON p.IsDeleted = 0 AND p.MatterTypeCode = m.MatterTypeCode
 WHERE m.IsDeleted = 0
   AND m.DecisionDomainPackMatterTypeId IS NULL
   AND m.Subtype IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackMatterType mt
					WHERE mt.DecisionDomainPackId = p.DecisionDomainPackId
					  AND mt.IsDeleted = 0
					  AND (mt.Name = m.Subtype OR mt.SubtypeCode = m.Subtype));

COMMIT TRANSACTION;

-- ---------------------------------------------------------------------------
-- 3) Verification.
-- ---------------------------------------------------------------------------
SELECT COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackMatterType', N'SubtypeCode') AS SubtypeCodeExists,
	   COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackMatterType', N'MatterTypeCode') AS OldColumnExists;
SELECT m.MatterTypeCode,
	   COUNT(*)                                                           AS Matters,
	   SUM(CASE WHEN m.DecisionDomainPackMatterTypeId IS NOT NULL THEN 1 ELSE 0 END) AS SubtypeBound,
	   SUM(CASE WHEN m.Subtype IS NOT NULL THEN 1 ELSE 0 END)             AS WithSubtypeText
  FROM POLOXI.Legal_DecisionMatter m
 WHERE m.IsDeleted = 0
 GROUP BY m.MatterTypeCode
 ORDER BY m.MatterTypeCode;
