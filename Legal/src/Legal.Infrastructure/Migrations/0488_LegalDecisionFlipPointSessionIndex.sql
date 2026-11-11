SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- Matter-list performance — index the critical-flip-point COUNT subquery.
--
-- The matter list (ILegalDecisionRepository.GetMattersAsync, GET api/legal_decision/matters) runs a
-- correlated subquery FOR EVERY matter row:
--     (SELECT COUNT(1) FROM POLOXI.Legal_DecisionFlipPoint fp
--         WHERE fp.IsDeleted = 0 AND fp.DecisionSessionId = s.DecisionSessionId AND fp.WinnerChanges = 1)
-- Legal_DecisionFlipPoint had only its PRIMARY KEY (DecisionFlipPointId) and an FK constraint on
-- DecisionSessionId — and an FK constraint does NOT create a supporting index in SQL Server. With no
-- index on DecisionSessionId, SQL Server scans the whole flip-point table once per matter, so the
-- matter list degrades as flip-point/matter volume grows (the reported "takes forever to load").
--
-- This filtered, covering index lets the per-row COUNT become a cheap seek: it keys on the join/filter
-- columns and only materializes non-deleted rows, matching the subquery predicate exactly. Idempotent
-- and re-runnable.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionFlipPoint', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Legal_DecisionFlipPoint_Session'
				   AND object_id = OBJECT_ID(N'POLOXI.Legal_DecisionFlipPoint'))
BEGIN
	EXEC(N'CREATE INDEX IX_Legal_DecisionFlipPoint_Session
			ON POLOXI.Legal_DecisionFlipPoint (DecisionSessionId, WinnerChanges)
			WHERE IsDeleted = 0;');
END

COMMIT TRANSACTION;
