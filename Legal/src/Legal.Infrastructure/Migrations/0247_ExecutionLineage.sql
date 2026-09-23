SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Execution lineage (freeze §10/§11).
--
-- A customer intelligence execution (for example legal.decision) may internally
-- invoke other meaningful, independently-managed operations (internal legal
-- research, verification). Those child runs are recorded as child executions
-- linked through ParentExecutionId, giving clean lineage without pretending the
-- children are separately customer-billed. Individual LLM calls stay as usage/
-- telemetry events and do NOT get their own execution row.
--
-- Idempotent, additive column — safe to re-run and safe on databases that have
-- already applied 0244.
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'SaaS.Platform_IntelligenceExecution', N'U') IS NOT NULL
   AND COL_LENGTH(N'SaaS.Platform_IntelligenceExecution', N'ParentExecutionId') IS NULL
	EXEC(N'ALTER TABLE SaaS.Platform_IntelligenceExecution ADD ParentExecutionId UNIQUEIDENTIFIER NULL;');

IF OBJECT_ID(N'FK_Platform_IntelligenceExecution_Parent', N'F') IS NULL
   AND COL_LENGTH(N'SaaS.Platform_IntelligenceExecution', N'ParentExecutionId') IS NOT NULL
	EXEC(N'ALTER TABLE SaaS.Platform_IntelligenceExecution
		ADD CONSTRAINT FK_Platform_IntelligenceExecution_Parent
		FOREIGN KEY (ParentExecutionId) REFERENCES SaaS.Platform_IntelligenceExecution (ExecutionId);');

IF OBJECT_ID(N'IX_Platform_IntelligenceExecution_Parent', N'IX') IS NULL
   AND COL_LENGTH(N'SaaS.Platform_IntelligenceExecution', N'ParentExecutionId') IS NOT NULL
	EXEC(N'CREATE INDEX IX_Platform_IntelligenceExecution_Parent
		ON SaaS.Platform_IntelligenceExecution (ParentExecutionId)
		WHERE IsDeleted = 0 AND ParentExecutionId IS NOT NULL;');

COMMIT TRANSACTION;
