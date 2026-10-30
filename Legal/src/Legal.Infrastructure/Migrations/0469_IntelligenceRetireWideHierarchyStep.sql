SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0469: Retire the WIDE_HIERARCHY_STEP prompt.
--
-- Scope: /legal/personalinjury_decision2 (Wide2 pipeline, IntelligenceWide2Service).
-- The iterative LLM narrowing driven by WIDE_HIERARCHY_STEP has been removed from the
-- Wide2 service: the reasoning hierarchy is now built from the DB-backed levels only
-- (deterministic L1/L2), and the loop stops before any WIDE_HIERARCHY_STEP-dependent
-- path (ProposeNextLevelAsync, GenerateDomainPackLevel3Async, the batch and
-- sibling-coverage passes). The prompt is therefore no longer invoked at runtime.
--
-- This migration soft-retires every WIDE_HIERARCHY_STEP prompt version (including v3.22
-- from 0436 and v3.23 from 0468) so no row is resolvable as the active approved prompt:
--   * sets StatusCode = RETIRED,
--   * closes EffectiveToUtc for any still-open row.
-- Rows are preserved (never deleted) for audit/history; WIDE_INTENT and WIDE_ANSWER are
-- untouched. Idempotent: re-running only re-stamps already-retired rows.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @NowUtc DATETIME2=SYSUTCDATETIME();
	DECLARE @SystemUserId UNIQUEIDENTIFIER=N'00000000-0000-0000-0000-000000000000';

	UPDATE prompt
	SET StatusCode=N'RETIRED',
		EffectiveToUtc=COALESCE(prompt.EffectiveToUtc,@NowUtc),
		ModifiedDateUtc=@NowUtc,
		ModifiedByUserId=@SystemUserId
	FROM AI.Legal_PromptDefinition prompt
	WHERE prompt.TenantId IS NULL
	  AND prompt.PromptCode=N'WIDE_HIERARCHY_STEP'
	  AND prompt.IsDeleted=0
	  AND (prompt.StatusCode<>N'RETIRED' OR prompt.EffectiveToUtc IS NULL);
END;

COMMIT TRANSACTION;
GO
