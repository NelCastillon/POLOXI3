-- 0145: Raise the INTELLIGENCE_WIDE_ANSWER input token budget.
-- Live external grounding snippets are now appended to the answer prompt, which can
-- exceed the previous 14000-token input limit and trip the AI safety input guard.
IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.FeaturePolicy
	SET MaximumInputTokens=CASE WHEN MaximumInputTokens<20000 THEN 20000 ELSE MaximumInputTokens END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode=N'INTELLIGENCE_WIDE_ANSWER'
	  AND IsDeleted=0
	  AND MaximumInputTokens<20000;
END;
