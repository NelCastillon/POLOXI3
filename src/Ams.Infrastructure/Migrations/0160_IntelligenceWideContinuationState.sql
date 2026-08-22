-- V3.4 Server-Side Continuation State + Evidence Reuse.
-- 1) ParentWideExecutionId links a clarification continuation to the execution that asked the
--    question. The epistemic chain (round, prior intent entropy, answer kind, clarification target,
--    original query text) is derived server-side from the parent row - a tampered or buggy client
--    can no longer corrupt it. Client-carried fields remain only as legacy fallbacks.
-- 2) ExternalKnowledge.WideExecutionId stamps which execution retrieved each snippet, so a
--    continuation can reuse the parent run's evidence pool and only fetch the clarification-driven
--    delta instead of re-grounding everything from scratch.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.WideExecution',N'ParentWideExecutionId') IS NULL
BEGIN
	ALTER TABLE POLOXI.WideExecution ADD ParentWideExecutionId UNIQUEIDENTIFIER NULL
		CONSTRAINT FK_PoloxiWideExecution_Parent REFERENCES POLOXI.WideExecution(WideExecutionId);
END;

IF COL_LENGTH(N'POLOXI.ExternalKnowledge',N'WideExecutionId') IS NULL
BEGIN
	ALTER TABLE POLOXI.ExternalKnowledge ADD WideExecutionId UNIQUEIDENTIFIER NULL;
END;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_PoloxiExternalKnowledge_Execution' AND object_id=OBJECT_ID(N'POLOXI.ExternalKnowledge'))
BEGIN
	EXEC(N'CREATE INDEX IX_PoloxiExternalKnowledge_Execution ON POLOXI.ExternalKnowledge(WideExecutionId) WHERE IsDeleted=0 AND WideExecutionId IS NOT NULL');
END;

COMMIT TRANSACTION;
