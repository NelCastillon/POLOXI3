SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0479: Apply the 0477 Search Quality rubric rebalance to TENANT-LEVEL overrides.
--
-- 0477 merged the redundant "Answer Confidence" (FINAL_CONFIDENCE) factor into a
-- single "Confidence" factor and introduced an independent Ranking Stability
-- factor, but it only touched platform defaults (TenantId IS NULL). Any tenant
-- that had customized its own Legal_SearchQualityFactor rows still shows the old
-- split "Decision Confidence" + "Answer Confidence" bars (always identical, since
-- both derive from the same DecisionConfidence value).
--
-- This migration performs the SAME transformation for every tenant override row:
--   1) Retire the grounding-dependent and redundant confidence factors.
--   2) Upsert an independent Ranking Stability factor (WinnerStability).
--   3) Merge both confidence signals into a single "Confidence" factor and
--      reweight each tenant's active rubric to: Confidence 0.45 +
--      Ranking Stability 0.30 + Analytical Depth 0.25.
-- Idempotent; safe to re-run. Only affects tenant-specific rows (TenantId IS NOT NULL).
-- ============================================================================

IF OBJECT_ID(N'POLOXI.Legal_SearchQualityFactor',N'U') IS NOT NULL
BEGIN
	-- 1) Retire the grounding-dependent factors and the redundant confidence factor (tenant rows only).
	UPDATE POLOXI.Legal_SearchQualityFactor
	SET IsDeleted = 1,
		IsActive = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE TenantId IS NOT NULL
	  AND IsDeleted = 0
	  AND FactorCode IN (N'EVIDENCE_COVERAGE', N'ENTERPRISE_EVIDENCE', N'VERIFIED_ANSWER', N'FINAL_CONFIDENCE');

	-- 2) Upsert the independent Ranking Stability factor per tenant that has a Confidence rubric but no
	--    stability factor yet. Reactivate/realign any previously retired tenant WINNER_STABILITY rows first.
	UPDATE POLOXI.Legal_SearchQualityFactor
	SET DisplayName = N'Ranking Stability',
		Description = N'How consistently the same leading answer held across reasoning rounds.',
		MetricCode = N'WinnerStability',
		Weight = CAST(0.30 AS DECIMAL(9,6)),
		MinValue = CAST(0 AS DECIMAL(18,6)), MaxValue = CAST(1 AS DECIMAL(18,6)),
		HigherIsBetter = 1, IsActive = 1, IsDeleted = 0, SortOrder = 20,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE TenantId IS NOT NULL AND FactorCode = N'WINNER_STABILITY';

	-- Insert a Ranking Stability row for any tenant that still owns a DECISION_CONFIDENCE factor but is
	-- missing a WINNER_STABILITY factor entirely.
	INSERT POLOXI.Legal_SearchQualityFactor
		(SearchQualityFactorId,TenantId,FactorCode,DisplayName,Description,MetricCode,Weight,MinValue,MaxValue,HigherIsBetter,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
	SELECT NEWID(), dc.TenantId, N'WINNER_STABILITY', N'Ranking Stability',
		N'How consistently the same leading answer held across reasoning rounds.', N'WinnerStability',
		CAST(0.30 AS DECIMAL(9,6)), CAST(0 AS DECIMAL(18,6)), CAST(1 AS DECIMAL(18,6)), 1, 1, 20, SYSUTCDATETIME(), 0
	FROM POLOXI.Legal_SearchQualityFactor dc
	WHERE dc.TenantId IS NOT NULL
	  AND dc.IsDeleted = 0
	  AND dc.FactorCode = N'DECISION_CONFIDENCE'
	  AND NOT EXISTS
	  (
		SELECT 1 FROM POLOXI.Legal_SearchQualityFactor ws
		WHERE ws.TenantId = dc.TenantId AND ws.FactorCode = N'WINNER_STABILITY'
	  );

	-- 3) Merge both confidence signals into a single "Confidence" factor and reweight to sum to 1.0.
	UPDATE POLOXI.Legal_SearchQualityFactor
	SET DisplayName = N'Confidence',
		Description = N'Combined confidence in the final answer (decision commitment and overall answer confidence).',
		Weight = CAST(0.45 AS DECIMAL(9,6)), ModifiedDateUtc = SYSUTCDATETIME()
	WHERE TenantId IS NOT NULL AND IsDeleted = 0 AND FactorCode = N'DECISION_CONFIDENCE';

	UPDATE POLOXI.Legal_SearchQualityFactor
	SET Weight = CAST(0.25 AS DECIMAL(9,6)), ModifiedDateUtc = SYSUTCDATETIME()
	WHERE TenantId IS NOT NULL AND IsDeleted = 0 AND FactorCode = N'CANDIDATE_DEPTH';
END;

COMMIT TRANSACTION;
