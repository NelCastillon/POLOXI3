SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0496: Raise the INTELLIGENCE_WIDE_INTENT completion budget so the first call
--   succeeds without truncation.
--   Symptom (production warning):
--     AzureOpenAiProvider: "AI call #N truncated: feature INTELLIGENCE_WIDE_INTENT
--     retrying once with doubled completion budget."
--   Root cause: INTELLIGENCE_WIDE_INTENT runs on the mechanical/override model,
--   which can be a reasoning-family model (gpt-5*, o-series). Reasoning models
--   burn HIDDEN reasoning tokens against max_completion_tokens, so the prior
--   16000-token budget was exhausted before the visible JSON completed
--   (finish_reason=length). The provider then self-heals by RE-RUNNING the whole
--   generation with a doubled budget - a wasted first call plus a second, larger
--   call, i.e. a guaranteed per-run latency + token tax on this feature.
--   Fix: grant the full budget up front so the first generation finishes in a
--   single call and the truncation retry never fires.
--   Raise-only: never lowers a tenant-tuned higher value. The budget is a cap,
--   not a spend, so standard (non-reasoning) models are unaffected.
-- ============================================================================

IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.Legal_FeaturePolicy
	SET MaximumOutputTokens=32000,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode=N'INTELLIGENCE_WIDE_INTENT'
	  AND IsDeleted=0
	  AND MaximumOutputTokens<32000;
END;

COMMIT TRANSACTION;
