-- ============================================================================
-- 0378: Reasoning-model timeout + completion headroom for INTELLIGENCE_WIDE_ANSWER.
--
-- WHY: With the hierarchy-step budget fixed (0377), the Wide pipeline now reaches
-- the final-answer stage, which fails on reasoning-family overrides (e.g.
-- gpt-6-astra) with:
--   System.Threading.Tasks.TaskCanceledException / SocketException(995)
--   "The I/O operation has been aborted ... because of ... an application request"
-- i.e. the per-call timeout CTS fired and aborted the HTTP request mid-generation,
-- the route was marked failed, and the operation fell over to the next route.
--
-- Reasoning models spend most of their latency on hidden reasoning tokens. The
-- INTELLIGENCE_WIDE_ANSWER policy still carries the standard-model 90s timeout
-- (seeded in 0180). AzureOpenAiProvider already triples the timeout for reasoning
-- models (90 -> 270s), but migration 0167 measured a reasoning ANSWER call at 308s,
-- which exceeds that ceiling and cancels the call. A 16000-token budget can also
-- force the reasoning truncation-retry (doubling) cycle, compounding the latency.
--
-- WHAT: Raise INTELLIGENCE_WIDE_ANSWER to the same headroom granted to the
-- landscape composer in 0376 -- MaximumOutputTokens 32000 (eliminates the
-- truncation-retry doubling) and TimeoutSeconds 180 (x3 reasoning multiplier =
-- 540s) -- so a reasoning override completes in a single generation.
--
-- Raise-only: never lowers a tenant-tuned higher value and never shrinks a
-- timeout an operator already raised. Idempotent and safe to re-run. Standard
-- models are unaffected (the budget is a cap, not a spend).
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.Legal_FeaturePolicy
	SET MaximumOutputTokens=CASE WHEN MaximumOutputTokens>32000 THEN MaximumOutputTokens ELSE 32000 END,
		TimeoutSeconds=CASE WHEN TimeoutSeconds>180 THEN TimeoutSeconds ELSE 180 END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode=N'INTELLIGENCE_WIDE_ANSWER'
	  AND IsDeleted=0
	  AND (MaximumOutputTokens<32000 OR TimeoutSeconds<180);
END;

COMMIT TRANSACTION;
GO
