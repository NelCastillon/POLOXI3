SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0465: Persist the Next Best Action panel alongside the captured Wide run.
--
--   Opening a saved Decision2 run from Analysis History ("View Original
--   Complete Run") was re-invoking the NBA synthesis LLM (gpt-6-astra,
--   INTELLIGENCE_WIDE_ANSWER) purely to re-render the advisory multi-action
--   panel -- a 29-30s cost for a read-only history view whose underlying Wide
--   result is already fully persisted in DetailJson.
--
--   This column stores the NextBestActionResult as JSON once, at execution
--   time, keyed by the Wide run row. The read-only history view then serves the
--   complete NBA panel verbatim from the database with NO fresh LLM call.
--   Nullable so legacy runs recorded before this column simply fall back to the
--   empty/advisory state without error.
-- ============================================================================

IF OBJECT_ID(N'POLOXI.Legal_WideRunResult', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_WideRunResult', N'NextBestActionJson') IS NULL
	ALTER TABLE POLOXI.Legal_WideRunResult
		ADD NextBestActionJson NVARCHAR(MAX) NULL;

COMMIT TRANSACTION;
