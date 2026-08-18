-- 0148: Raise the INTELLIGENCE_WIDE_HIERARCHY_STEP and INTELLIGENCE_WIDE_INTENT output token budgets.
-- V2.1 keeps SECONDARY branches narrowing and demotes evidence-void branches to DORMANT instead of
-- pruning them, so more parents survive per level and the structured next-level JSON (children for
-- every surviving parent) exceeded the previous 1500-token completion limit, causing truncated
-- Azure OpenAI responses and AiProviderUnavailableException.
IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.FeaturePolicy
	SET MaximumOutputTokens=6000,
		MaximumInputTokens=CASE WHEN MaximumInputTokens<14000 THEN 14000 ELSE MaximumInputTokens END,
		TimeoutSeconds=CASE WHEN TimeoutSeconds<90 THEN 90 ELSE TimeoutSeconds END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode IN(N'INTELLIGENCE_WIDE_HIERARCHY_STEP',N'INTELLIGENCE_WIDE_INTENT')
	  AND IsDeleted=0
	  AND MaximumOutputTokens<6000;
END;
