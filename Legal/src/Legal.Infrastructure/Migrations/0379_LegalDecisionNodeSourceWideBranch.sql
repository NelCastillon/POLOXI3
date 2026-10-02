SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Attorney Decision Input (ADI) · Wide-branch provenance on decision nodes
-- Adds a nullable SourceWideBranchId to POLOXI.Legal_DecisionNode so an attorney who adds a proposition
-- from the live POLOXI hierarchy (the Hierarchy tab's surviving Wide branches) can be materialized once
-- into a canonical decision node and then reused idempotently. POLOXI remains the single authoritative
-- evaluator; this column is pure provenance and never affects scoring.
--
-- The filtered unique index guarantees one materialized decision node per (Tenant, Matter, WideBranch):
-- repeated "Add proposition from this branch" actions resolve to the same node rather than duplicating
-- the hierarchy. Safe to re-run (idempotent guards).
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionNode', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_DecisionNode', N'SourceWideBranchId') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionNode
		ADD SourceWideBranchId UNIQUEIDENTIFIER NULL;
END
GO

IF OBJECT_ID(N'POLOXI.Legal_DecisionNode', N'U') IS NOT NULL
   AND NOT EXISTS (
		SELECT 1 FROM sys.indexes
		WHERE name = N'UX_Legal_DecisionNode_SourceWideBranch'
		  AND object_id = OBJECT_ID(N'POLOXI.Legal_DecisionNode'))
BEGIN
	CREATE UNIQUE INDEX UX_Legal_DecisionNode_SourceWideBranch
		ON POLOXI.Legal_DecisionNode (TenantId, MatterId, SourceWideBranchId)
		WHERE SourceWideBranchId IS NOT NULL AND IsDeleted = 0;
END
GO

COMMIT TRANSACTION;
GO
