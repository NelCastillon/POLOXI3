-- 0143: Raise the INTELLIGENCE_WIDE_ANSWER output token budget.
-- The wide answer now returns one interpretive result set per surviving hierarchy path
-- (up to 25 sets across Level 1 and Level 2), which exceeds the previous 2000-token
-- completion limit and caused truncated JSON responses from Azure OpenAI.
IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.FeaturePolicy
	SET MaximumOutputTokens=8000,
		MaximumInputTokens=CASE WHEN MaximumInputTokens<14000 THEN 14000 ELSE MaximumInputTokens END,
		TimeoutSeconds=CASE WHEN TimeoutSeconds<90 THEN 90 ELSE TimeoutSeconds END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode=N'INTELLIGENCE_WIDE_ANSWER'
	  AND IsDeleted=0
	  AND MaximumOutputTokens<8000;
END;
