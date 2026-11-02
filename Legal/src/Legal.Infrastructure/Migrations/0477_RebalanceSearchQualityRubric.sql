SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0477: Rebalance the Search Quality Indicator rubric.
--
-- Two problems with the original 0475 rubric:
--
-- 1) Grounding bias: Evidence Coverage (0.25) + Enterprise Grounding (0.15) +
--    Verification (0.05) = 0.45 of the total weight. Interpretive / ungrounded
--    answers (for example fast-model lookups with no enterprise evidence) always
--    score 0 on all three, mathematically capping their composite at ~0.55 and
--    pinning them to a permanent "D" regardless of actual answer quality.
--
-- 2) Double-counted confidence: Answer Confidence (FinalConfidence) is derived
--    from the same deterministic DecisionConfidence value in the decision path,
--    so weighting both effectively counted one signal twice.
--
-- Fix: retire the three grounding factors AND the redundant Answer Confidence
-- factor, merge both confidence signals into a single "Confidence" factor
-- (DecisionConfidence already falls back to FinalConfidence), introduce an
-- independent Ranking Stability factor (WinnerStability - how consistently the
-- same leading answer held across reasoning rounds), and reweight the active
-- rubric to sum to 1.0:
--    Confidence 0.45 + Ranking Stability 0.30 + Analytical Depth 0.25.
-- Idempotent and tenant-null (platform defaults).
-- ============================================================================

IF OBJECT_ID(N'POLOXI.Legal_SearchQualityFactor',N'U') IS NOT NULL
BEGIN
	-- 1) Retire the grounding-dependent factors and the redundant confidence factor (platform rows only).
	UPDATE POLOXI.Legal_SearchQualityFactor
	SET IsDeleted = 1,
		IsActive = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND IsDeleted = 0
	  AND FactorCode IN (N'EVIDENCE_COVERAGE', N'ENTERPRISE_EVIDENCE', N'VERIFIED_ANSWER', N'FINAL_CONFIDENCE');

	-- 2) Upsert the independent Ranking Stability factor (idempotent; reactivates if previously retired).
	IF EXISTS (SELECT 1 FROM POLOXI.Legal_SearchQualityFactor WHERE TenantId IS NULL AND FactorCode = N'WINNER_STABILITY')
		UPDATE POLOXI.Legal_SearchQualityFactor
		SET DisplayName = N'Ranking Stability',
			Description = N'How consistently the same leading answer held across reasoning rounds.',
			MetricCode = N'WinnerStability',
			Weight = CAST(0.30 AS DECIMAL(9,6)),
			MinValue = CAST(0 AS DECIMAL(18,6)), MaxValue = CAST(1 AS DECIMAL(18,6)),
			HigherIsBetter = 1, IsActive = 1, IsDeleted = 0, SortOrder = 20,
			ModifiedDateUtc = SYSUTCDATETIME()
		WHERE TenantId IS NULL AND FactorCode = N'WINNER_STABILITY';
	ELSE
		INSERT POLOXI.Legal_SearchQualityFactor
			(SearchQualityFactorId,TenantId,FactorCode,DisplayName,Description,MetricCode,Weight,MinValue,MaxValue,HigherIsBetter,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
		VALUES
			(NEWID(),NULL,N'WINNER_STABILITY',N'Ranking Stability',N'How consistently the same leading answer held across reasoning rounds.',N'WinnerStability',
			 CAST(0.30 AS DECIMAL(9,6)),CAST(0 AS DECIMAL(18,6)),CAST(1 AS DECIMAL(18,6)),1,1,20,SYSUTCDATETIME(),0);

	-- 3) Merge both confidence signals into a single "Confidence" factor and reweight to sum to 1.0.
	UPDATE POLOXI.Legal_SearchQualityFactor
	SET DisplayName = N'Confidence',
		Description = N'Combined confidence in the final answer (decision commitment and overall answer confidence).',
		Weight = CAST(0.45 AS DECIMAL(9,6)), ModifiedDateUtc = SYSUTCDATETIME()
	WHERE TenantId IS NULL AND IsDeleted = 0 AND FactorCode = N'DECISION_CONFIDENCE';

	UPDATE POLOXI.Legal_SearchQualityFactor
	SET Weight = CAST(0.25 AS DECIMAL(9,6)), ModifiedDateUtc = SYSUTCDATETIME()
	WHERE TenantId IS NULL AND IsDeleted = 0 AND FactorCode = N'CANDIDATE_DEPTH';
END;

COMMIT TRANSACTION;
