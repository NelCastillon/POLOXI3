-- 0199: Raise the Wide-search feature-policy timeout budget for reasoning models.
-- The Wide pipeline runs the shared INTELLIGENCE_WIDE_* feature policies and passes the
-- selected model (e.g. gpt-6-astra) as a model override. Reasoning-family models spend
-- extended time on hidden reasoning tokens, and AzureOpenAiProvider.CreateTimeout triples the
-- route timeout for them (capped at 900s). The prior 90s base (x3 = 270s effective) was too low
-- for gpt-6-astra and caused TaskCanceledException on the WIDE_ANSWER step. Raising the base
-- floor to 180s (x3 = 540s effective) gives reasoning models enough headroom while leaving
-- higher, already-configured timeouts untouched. Idempotent: only raises policies below the floor.
IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.Legal_FeaturePolicy
	SET TimeoutSeconds=180,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode LIKE N'INTELLIGENCE_WIDE[_]%'
	  AND IsDeleted=0
	  AND TimeoutSeconds<180;
END;
