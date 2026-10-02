-- ============================================================================
-- 0380: Raise the INTELLIGENCE_WIDE_INFORMATION_VALUE completion budget so the
--       legal-authority proposal step is not truncated on reasoning models.
--
-- WHY: INTELLIGENCE_WIDE_INFORMATION_VALUE backs ProposeLegalAuthoritiesAsync,
-- the step that proposes primary legal authorities (up to 8, each with a name +
-- relevance) used as retrieval LEADS. Those leads drive external grounding, and
-- external grounding is what produces the "Admitted evidence" the cockpit shows.
--
-- The feature policy seeded in 0149 set MaximumOutputTokens=4000 / TimeoutSeconds=45,
-- which is correct for a standard model (gpt-4.1-mini): it spends no hidden
-- reasoning tokens, so 4000 completion tokens comfortably hold the proposal JSON.
-- A reasoning model (gpt-6-astra) burns hidden reasoning tokens against the SAME
-- max_completion_tokens ceiling, truncates the proposal JSON (finish_reason=length),
-- and ProposeLegalAuthoritiesAsync fail-softs to an EMPTY authority list ->
-- zero retrieval leads -> zero admitted evidence. This is exactly why admitted
-- evidence appears on gpt-4.1-mini but not on gpt-6-astra.
--
-- AzureOpenAiProvider.ResolveInitialOutputBudget already EXEMPTS INFORMATION_VALUE
-- from the small mechanical cap and grants the full configured budget, but the
-- configured budget itself was still 4000 in the DB. Raising it here gives the
-- reasoning model enough headroom to emit the full proposal after its hidden
-- reasoning, restoring parity with gpt-4.1-mini.
--
-- WHAT: Raise MaximumOutputTokens to 16000 and TimeoutSeconds to 120 (and ensure
-- at least 14000 input tokens). Raise-only: never lowers a tenant-tuned higher
-- value and never shrinks an operator-raised timeout. Idempotent and safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.Legal_FeaturePolicy
	SET MaximumOutputTokens=CASE WHEN MaximumOutputTokens>16000 THEN MaximumOutputTokens ELSE 16000 END,
		MaximumInputTokens=CASE WHEN MaximumInputTokens<14000 THEN 14000 ELSE MaximumInputTokens END,
		TimeoutSeconds=CASE WHEN TimeoutSeconds>120 THEN TimeoutSeconds ELSE 120 END,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode=N'INTELLIGENCE_WIDE_INFORMATION_VALUE'
	  AND IsDeleted=0
	  AND (MaximumOutputTokens<16000 OR TimeoutSeconds<120 OR MaximumInputTokens<14000);
END;

COMMIT TRANSACTION;
GO
