SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0456: Complete the 0455 matter FK backfill for matters with NULL DomainPackCode.
--   0455 pass D only resolved matters whose DomainPackCode was populated; in practice every
--   existing matter has DomainPackCode NULL, so nothing bound and the deterministic pre-L2
--   matter-type filter never activated. This pass mirrors the application resolver
--   (LegalDecisionRepository.ResolveDomainPackMatterTypeIdAsync): when no pack is bound,
--   resolve the exact code across ALL active packs.
--   Safety (fail-open preserved):
--     * Exact case-insensitive match only on MatterTypeCode -> Subtype -> ClaimTypeCode.
--     * Codes that exist in MORE THAN ONE pack are skipped as ambiguous (stay NULL = no filter).
--     * Only global (TenantId NULL) or the matter's own tenant vocabulary rows are considered.
--   Idempotent: only touches rows where DecisionDomainPackMatterTypeId IS NULL.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

;WITH vocab AS
(
	SELECT mt.DecisionDomainPackMatterTypeId,
		   mt.DecisionDomainPackId,
		   LOWER(LTRIM(RTRIM(mt.MatterTypeCode))) AS Code,
		   mt.TenantId,
		   -- Ambiguity guard: same code offered by more than one pack cannot be resolved blindly.
		   COUNT(*) OVER (PARTITION BY LOWER(LTRIM(RTRIM(mt.MatterTypeCode)))) AS PackCount
	FROM POLOXI.Legal_DecisionDomainPackMatterType mt
	JOIN POLOXI.Legal_DecisionDomainPack p
	  ON p.DecisionDomainPackId = mt.DecisionDomainPackId AND p.IsDeleted = 0 AND p.IsActive = 1
	WHERE mt.IsDeleted = 0 AND mt.IsActive = 1
),
candidates AS
(
	SELECT m.DecisionMatterId,
		   v.DecisionDomainPackMatterTypeId,
		   ROW_NUMBER() OVER (PARTITION BY m.DecisionMatterId ORDER BY
				CASE WHEN LOWER(LTRIM(RTRIM(m.MatterTypeCode))) = v.Code THEN 0
					 WHEN LOWER(LTRIM(RTRIM(m.Subtype)))        = v.Code THEN 1
					 WHEN LOWER(LTRIM(RTRIM(m.ClaimTypeCode)))  = v.Code THEN 2
					 ELSE 3 END) AS rn,
		   CASE WHEN LOWER(LTRIM(RTRIM(m.MatterTypeCode))) = v.Code
				  OR LOWER(LTRIM(RTRIM(m.Subtype)))        = v.Code
				  OR LOWER(LTRIM(RTRIM(m.ClaimTypeCode)))  = v.Code
				THEN 1 ELSE 0 END AS IsMatch
	FROM POLOXI.Legal_DecisionMatter m
	JOIN vocab v
	  ON v.PackCount = 1
	 AND (v.TenantId IS NULL OR v.TenantId = m.TenantId)
	WHERE m.IsDeleted = 0
	  AND m.DecisionDomainPackMatterTypeId IS NULL
	  AND m.DomainPackCode IS NULL
)
UPDATE m
   SET m.DecisionDomainPackMatterTypeId = c.DecisionDomainPackMatterTypeId,
	   m.ModifiedDateUtc = @Now,
	   m.ModifiedByUserId = @SystemUserId
  FROM POLOXI.Legal_DecisionMatter m
  JOIN candidates c ON c.DecisionMatterId = m.DecisionMatterId AND c.rn = 1 AND c.IsMatch = 1;

COMMIT TRANSACTION;
