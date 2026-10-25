SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0457: Make Legal_DecisionDomainPackMatterType a REAL lookup table.
--   MatterTypeCode becomes a stable SCREAMING_SNAKE machine code; Name remains the display
--   label. Previously code = name ("Premises Liability" in both columns) and
--   Legal_DecisionMatter.MatterTypeCode carried arbitrary display text in a code column.
--
--   Scope:
--     1) Explicit name->code rewrite of all vocabulary rows (both packs). No fuzzy derivation.
--     2) Unique index (DecisionDomainPackId, MatterTypeCode) on live rows.
--     3) Resync the DEPRECATED Legal_DecisionDomainPackEvidenceType.MatterTypeCodes string
--        from the junction (now emits codes).
--     4) Bound matters: MatterTypeCode = vocabulary CODE (from FK), DomainPackCode = owning
--        PackCode. Unbound legacy matters keep their free text (fail-open, cleaned on next edit).
--   The evidence-type junction and matter FK are GUID-based and unaffected by the rename.
--   Idempotent: the rewrite maps old names; already-coded rows simply match nothing.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 1) Explicit vocabulary code normalization (Name stays the display label).
-- ---------------------------------------------------------------------------
DECLARE @CodeMap TABLE (OldCode NVARCHAR(200) NOT NULL PRIMARY KEY, NewCode NVARCHAR(120) NOT NULL);
INSERT @CodeMap (OldCode, NewCode) VALUES
-- PERSONAL_INJURY
(N'Motor Vehicle Accident',          N'MOTOR_VEHICLE_ACCIDENT'),
(N'Truck Accident',                  N'TRUCK_ACCIDENT'),
(N'Motorcycle Accident',             N'MOTORCYCLE_ACCIDENT'),
(N'Pedestrian Accident',             N'PEDESTRIAN_ACCIDENT'),
(N'Bicycle Accident',                N'BICYCLE_ACCIDENT'),
(N'Premises Liability',              N'PREMISES_LIABILITY'),
(N'Product Liability',               N'PRODUCT_LIABILITY'),
(N'Medical Malpractice',             N'MEDICAL_MALPRACTICE'),
(N'Wrongful Death',                  N'WRONGFUL_DEATH'),
(N'Dog Bite / Animal Injury',        N'DOG_BITE_ANIMAL_INJURY'),
(N'Slip and Fall',                   N'SLIP_AND_FALL'),
(N'Other Negligence',                N'OTHER_NEGLIGENCE'),
(N'Trucking / Commercial Vehicle',   N'TRUCKING_COMMERCIAL_VEHICLE'),
(N'Pedestrian / Bicycle Accident',   N'PEDESTRIAN_BICYCLE_ACCIDENT'),
(N'Rideshare (TNC) Accident',        N'RIDESHARE_TNC_ACCIDENT'),
(N'Negligent Security',              N'NEGLIGENT_SECURITY'),
(N'Nursing Home / Elder Abuse',      N'NURSING_HOME_ELDER_ABUSE'),
(N'Construction / Workplace Injury', N'CONSTRUCTION_WORKPLACE_INJURY'),
(N'Toxic Tort / Exposure',           N'TOXIC_TORT_EXPOSURE'),
(N'Dram Shop / Liquor Liability',    N'DRAM_SHOP_LIQUOR_LIABILITY'),
(N'Aviation / Maritime',             N'AVIATION_MARITIME'),
(N'Mass Tort / Pharmaceutical',      N'MASS_TORT_PHARMACEUTICAL'),
(N'Traumatic Brain Injury (TBI)',    N'TRAUMATIC_BRAIN_INJURY'),
(N'Spinal Cord Injury',              N'SPINAL_CORD_INJURY'),
-- EMPLOYMENT
(N'Wrongful Termination',                  N'WRONGFUL_TERMINATION'),
(N'Discrimination',                        N'DISCRIMINATION'),
(N'Retaliation',                           N'RETALIATION'),
(N'Harassment / Hostile Work Environment', N'HARASSMENT_HOSTILE_WORK_ENVIRONMENT'),
(N'Wage & Hour Dispute',                   N'WAGE_HOUR_DISPUTE'),
(N'Non-Compete Enforcement',               N'NON_COMPETE_ENFORCEMENT'),
(N'Trade Secret Dispute',                  N'TRADE_SECRET_DISPUTE'),
(N'Leave / Accommodation',                 N'LEAVE_ACCOMMODATION'),
(N'Whistleblower',                         N'WHISTLEBLOWER'),
(N'Other Employment',                      N'OTHER_EMPLOYMENT');

UPDATE mt
   SET mt.MatterTypeCode = cm.NewCode,
	   mt.ModifiedDateUtc = @Now,
	   mt.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionDomainPackMatterType mt
  JOIN @CodeMap cm ON LOWER(LTRIM(RTRIM(mt.MatterTypeCode))) = LOWER(cm.OldCode)
 -- Case-SENSITIVE guard: single-word names (Discrimination) differ from codes (DISCRIMINATION)
 -- only by case, which a CI collation would treat as equal and skip.
 WHERE mt.MatterTypeCode COLLATE Latin1_General_CS_AS <> cm.NewCode;

-- ---------------------------------------------------------------------------
-- 2) Unique code per pack on live rows.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Legal_DDPMatterType_PackCode'
				 AND object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackMatterType'))
	CREATE UNIQUE INDEX UX_Legal_DDPMatterType_PackCode
		ON POLOXI.Legal_DecisionDomainPackMatterType (DecisionDomainPackId, MatterTypeCode)
		WHERE IsDeleted = 0;

-- ---------------------------------------------------------------------------
-- 3) Resync the DEPRECATED comma string from the junction (now codes).
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
	 WHERE ISNULL(e.MatterTypeCodes, N'') COLLATE Latin1_General_CS_AS <> LEFT(a.Codes, 400);
END;

-- ---------------------------------------------------------------------------
-- 4) Bound matters: MatterTypeCode = vocabulary CODE, DomainPackCode = owning pack.
--    Unbound matters are untouched (fail-open legacy free text).
-- ---------------------------------------------------------------------------
UPDATE m
   SET m.MatterTypeCode = mt.MatterTypeCode,
	   m.DomainPackCode = p.PackCode,
	   m.ModifiedDateUtc = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN POLOXI.Legal_DecisionDomainPackMatterType mt ON mt.DecisionDomainPackMatterTypeId = m.DecisionDomainPackMatterTypeId
  JOIN POLOXI.Legal_DecisionDomainPack p ON p.DecisionDomainPackId = mt.DecisionDomainPackId
 WHERE m.IsDeleted = 0
   AND (ISNULL(m.MatterTypeCode, N'') COLLATE Latin1_General_CS_AS <> mt.MatterTypeCode
     OR ISNULL(m.DomainPackCode, N'') COLLATE Latin1_General_CS_AS <> p.PackCode);

COMMIT TRANSACTION;
