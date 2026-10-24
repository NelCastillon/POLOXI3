SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0454: Per-matter Blended Score scope-gate threshold.
--   Adds POLOXI.Legal_DecisionMatter.MatterScopeOutThreshold so the Blended Score dial on
--   /legal/personalinjury_decision2 persists its last value PER MATTER (not tenant-global).
--   The /legal/configuration admin page remains the tenant/platform default for all matters;
--   this column, when set, is the per-run override used by THAT matter only. NULL = fall back
--   to the tenant/platform configured Intelligence.SearchWide.MatterScopeOutThreshold.
--   Idempotent and additive. Nothing is deleted.
-- ============================================================================

IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'MatterScopeOutThreshold') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD MatterScopeOutThreshold DECIMAL(9,4) NULL;
END;

COMMIT TRANSACTION;
