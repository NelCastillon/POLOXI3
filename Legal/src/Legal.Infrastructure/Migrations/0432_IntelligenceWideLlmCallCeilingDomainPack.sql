-- 0432_IntelligenceWideLlmCallCeilingDomainPack
-- Raise the Wide LLM-call circuit breaker (Intelligence.SearchWide.MaximumTotalLlmCalls) from 30 to
-- 400. Migration 0142 sized 30 for an LLM-proposed L2 of ~5-25 branches. Use Domain Pack mode makes L2
-- deterministic and DB-backed (100+ Concept/EvidenceType factors per matter); the L3 proposal now
-- issues one focused hierarchy-step call PER surviving L2 parent so every factor is guaranteed
-- matter-specific atomic propositions, and the sibling-coverage pass backfills any parent still skipped.
-- At 30 the ceiling cut the pass off after a handful of parents, leaving ~90% of L2 factors without
-- atomic propositions (9 L3 under 128 L2). 400 covers the per-parent L3 pass (~130), backfill, deeper
-- levels, and the information/answer stages. Raise-only; the repositories clamp the value to [1,600].
-- DB remains the source of truth for the circuit breaker.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=CASE WHEN TRY_CONVERT(INT,SettingValue)<400 THEN N'400' ELSE SettingValue END,
		DefaultValue=CASE WHEN TRY_CONVERT(INT,DefaultValue)<400 THEN N'400' ELSE DefaultValue END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'Intelligence.SearchWide.MaximumTotalLlmCalls'
	  AND IsDeleted=0;
END;
