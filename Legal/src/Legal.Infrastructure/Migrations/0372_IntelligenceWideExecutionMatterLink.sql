SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- Make the Wide execution store matter-addressable.
-- The Overview (/legal/personalinjury_decision2) renders its live C1/C2/C3
-- candidates from a Wide execution persisted in POLOXI.Legal_WideExecution +
-- Legal_WideCandidate + Legal_WideBranch. The Candidate Full Analysis drill-down
-- needs to rehydrate THAT SAME result by matter, but Legal_WideExecution had no
-- MatterId, so it was not discoverable per matter. This adds a nullable MatterId
-- plus a covering index so the latest completed Wide run for a matter can be
-- located deterministically. Idempotent; safe to re-run.
-- ============================================================================

IF COL_LENGTH(N'POLOXI.Legal_WideExecution',N'MatterId') IS NULL
	ALTER TABLE POLOXI.Legal_WideExecution ADD MatterId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_PoloxiWideExecution_Matter' AND object_id=OBJECT_ID(N'POLOXI.Legal_WideExecution'))
	CREATE INDEX IX_Legal_PoloxiWideExecution_Matter ON POLOXI.Legal_WideExecution(TenantId,MatterId,CreatedDateUtc) WHERE IsDeleted=0;

COMMIT TRANSACTION;
