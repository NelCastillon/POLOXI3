SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0463: Curated backfill of PI-generic Evidence Type -> Matter Subtype tags.
--   Audit basis (2026-10-25): of 238 untagged PERSONAL_INJURY evidence types,
--   237 have no IncidentFamilyCodes restriction - they are case-process
--   evidence (wage docs, economist reports, mitigation records, settlement
--   valuation, procedure, resolution, etc.) applicable to every PI subtype.
--   The 1 family-scoped row, SOCIAL_SECURITY_EARNINGS_STATEMENT, carries bogus
--   IncidentFamilyCodes ('NEGLIGENT_SECURITY,PREMISES' - keyword mis-match on
--   the word 'security'); an SSA earnings statement is generic wage evidence.
--   This migration:
--     1) Clears the erroneous IncidentFamilyCodes on
--        SOCIAL_SECURITY_EARNINGS_STATEMENT.
--     2) Tags every still-untagged active PERSONAL_INJURY evidence type to all
--        active PI subtypes (238 EVDs x 24 subtypes).
--   Curated subtype-specific tags (the 103 liability-side EVDs) are untouched.
--   Idempotent: only missing (evidence type, subtype) pairs insert.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- 1) Fix the mis-scoped SSA earnings statement row.
UPDATE e
   SET e.IncidentFamilyCodes = NULL,
	   e.ModifiedDateUtc = @Now,
	   e.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
  JOIN POLOXI.Legal_DecisionDomainPack p ON p.DecisionDomainPackId = e.DecisionDomainPackId
 WHERE p.PackCode = 'PERSONAL_INJURY'
   AND e.EvidenceTypeCode = 'SOCIAL_SECURITY_EARNINGS_STATEMENT'
   AND e.IsDeleted = 0
   AND e.IncidentFamilyCodes IS NOT NULL;

-- 2) Tag all untagged active PI evidence types to every active PI subtype.
INSERT INTO POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType
	(DecisionDomainPackEvidenceTypeMatterTypeId, DecisionDomainPackEvidenceTypeId, MatterSubtypeId,
	 TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
SELECT NEWID(), e.DecisionDomainPackEvidenceTypeId, s.MatterSubtypeId,
	   e.TenantId, @Now, @SystemUserId, @Now, @SystemUserId, 0
FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
JOIN POLOXI.Legal_DecisionDomainPack p
  ON p.DecisionDomainPackId = e.DecisionDomainPackId AND p.IsDeleted = 0 AND p.PackCode = 'PERSONAL_INJURY'
JOIN POLOXI.Legal_MatterType t
  ON t.IsDeleted = 0 AND t.Code = 'PERSONAL_INJURY'
JOIN POLOXI.Legal_MatterSubtype s
  ON s.MatterTypeId = t.MatterTypeId AND s.IsDeleted = 0 AND s.IsActive = 1
WHERE e.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j0
				  WHERE j0.IsDeleted = 0 AND j0.DecisionDomainPackEvidenceTypeId = e.DecisionDomainPackEvidenceTypeId)
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j1
				  WHERE j1.DecisionDomainPackEvidenceTypeId = e.DecisionDomainPackEvidenceTypeId
					AND j1.MatterSubtypeId = s.MatterSubtypeId AND j1.IsDeleted = 0);

DECLARE @Inserted INT = @@ROWCOUNT;
PRINT CONCAT('0463: inserted ', @Inserted, ' PI evidence-type subtype tag rows.');

-- 3) Tag untagged EMPLOYMENT evidence types to every active EMPLOYMENT subtype
--    (same generic-process rationale: COMMUNICATIONS, PERSONNEL_FILE, WITNESS_STATEMENTS).
INSERT INTO POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType
	(DecisionDomainPackEvidenceTypeMatterTypeId, DecisionDomainPackEvidenceTypeId, MatterSubtypeId,
	 TenantId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
SELECT NEWID(), e.DecisionDomainPackEvidenceTypeId, s.MatterSubtypeId,
	   e.TenantId, @Now, @SystemUserId, @Now, @SystemUserId, 0
FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
JOIN POLOXI.Legal_DecisionDomainPack p
  ON p.DecisionDomainPackId = e.DecisionDomainPackId AND p.IsDeleted = 0 AND p.PackCode = 'EMPLOYMENT'
JOIN POLOXI.Legal_MatterType t
  ON t.IsDeleted = 0 AND t.Code = 'EMPLOYMENT'
JOIN POLOXI.Legal_MatterSubtype s
  ON s.MatterTypeId = t.MatterTypeId AND s.IsDeleted = 0 AND s.IsActive = 1
WHERE e.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j0
				  WHERE j0.IsDeleted = 0 AND j0.DecisionDomainPackEvidenceTypeId = e.DecisionDomainPackEvidenceTypeId);

-- 4) Keep the denormalized MatterTypeCodes column on the EVD table in sync with
--    the junction tags (DB source of truth is the junction; this column is the
--    visible projection). Widen first: 24 PI codes exceed the old column size.
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackEvidenceType', N'MatterTypeCodes') < 2000
	ALTER TABLE POLOXI.Legal_DecisionDomainPackEvidenceType ALTER COLUMN MatterTypeCodes NVARCHAR(1000) NULL;

UPDATE e
   SET e.MatterTypeCodes  = x.Codes,
	   e.ModifiedDateUtc  = @Now,
	   e.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
 CROSS APPLY (SELECT STRING_AGG(s.Code, ',') WITHIN GROUP (ORDER BY s.SortOrder) AS Codes
			  FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j
			  JOIN POLOXI.Legal_MatterSubtype s ON s.MatterSubtypeId = j.MatterSubtypeId AND s.IsDeleted = 0
			  WHERE j.IsDeleted = 0 AND j.DecisionDomainPackEvidenceTypeId = e.DecisionDomainPackEvidenceTypeId) x
 WHERE e.IsDeleted = 0 AND x.Codes IS NOT NULL AND ISNULL(e.MatterTypeCodes, '') <> x.Codes;

-- Verify: no active PI evidence type may remain untagged.
IF EXISTS (SELECT 1
		   FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
		   JOIN POLOXI.Legal_DecisionDomainPack p
			 ON p.DecisionDomainPackId = e.DecisionDomainPackId AND p.IsDeleted = 0 AND p.PackCode = 'PERSONAL_INJURY'
		   WHERE e.IsDeleted = 0
			 AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j
							 WHERE j.IsDeleted = 0 AND j.DecisionDomainPackEvidenceTypeId = e.DecisionDomainPackEvidenceTypeId))
BEGIN
	ROLLBACK TRANSACTION;
	THROW 50463, N'0463 aborted: active PERSONAL_INJURY evidence types remain untagged.', 1;
END;

COMMIT TRANSACTION;
