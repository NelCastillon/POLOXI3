SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0422: Per-source last-run tracking for POLOXI.Legal_AuthoritySource.
--
-- WHY: An administrator managing the authority-source registry needs to see, at
-- a glance, that a configured provider is not only Enabled but has actually run
-- before (and with what outcome). The existing IsEnabled flag only expresses
-- intent (should this source participate?); it says nothing about whether the
-- source was ever exercised or whether the last run succeeded.
--
-- WHAT: Adds two nullable, runtime-populated columns:
--   * LastRunDateUtc  — UTC timestamp of the most recent retrieval attempt
--                       against this source (stamped by the retriever).
--   * LastRunStatus   — the outcome code of that most recent attempt
--                       (e.g. RESULTS_FOUND, NO_RESULTS, ACCESS_DENIED,
--                        PROVIDER_FAILURE, EXTRACTION_EMPTY).
-- Both are NULL for sources that have never run, which the UI renders as
-- "Never run". Existing HTML/curated/JSON rows are unaffected.
--
-- DDL ONLY. Idempotent: guarded by COL_LENGTH so re-runs are no-ops.
-- ============================================================================

BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource', N'LastRunDateUtc') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySource ADD LastRunDateUtc DATETIME2(3) NULL;

IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource', N'LastRunStatus') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySource ADD LastRunStatus NVARCHAR(60) NULL;

COMMIT TRANSACTION;
