-- ============================================================================
-- 0398: Factual-predicate gate for canonical Decision Outcome candidates.
--
-- WHY THIS EXISTS:
--   Migrations 0396/0397 seed the PERSONAL_INJURY canonical outcome pool (C1-C9).
--   Every active outcome was injected UNCONDITIONALLY into the legal EVALUATE
--   candidate universe, with no check against the matter facts. For outcomes whose
--   very identity requires a specific fact to be true (e.g. C9 Default judgment
--   requires the defendant to have FAILED TO APPEAR OR DEFEND), that meant an
--   unsupported outcome could enter competition and, because support is scored
--   RELATIVE to the surviving field, surface as the apparent leader even when the
--   matter record explicitly contradicts it.
--
-- CORRECTION (data-driven, DB is source of truth):
--   Add two columns so an outcome can declare, per row, that it is only eligible
--   when the matter facts satisfy a factual predicate:
--
--     RequiresFactualPredicate BIT      -- 1 => this outcome must satisfy the
--                                          predicate below before it can enter the
--                                          candidate universe. 0 => always eligible
--                                          (unchanged behavior for C1-C8).
--     FactualPredicateKeywords NVARCHAR  -- '|'-delimited keyword/phrase set. The
--                                          outcome is eligible only when the matter
--                                          context text contains at least one entry.
--
--   This keeps the gate GENERIC: any current/future outcome can opt in by setting
--   its own predicate data; no outcome name is hardcoded in application code.
--
--   C9 Default judgment is the first row to opt in: it is eligible only when the
--   matter actually references a default / failure to appear / failure to defend.
--
-- Depends on the table created by migration 0396. Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ── Add the predicate columns (idempotent) ──────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NOT NULL
	AND NOT EXISTS (SELECT 1 FROM sys.columns
		WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate')
		  AND name = N'RequiresFactualPredicate')
	ALTER TABLE POLOXI.Legal_DecisionDomainPackOutcomeCandidate
		ADD RequiresFactualPredicate BIT NOT NULL
			CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidate_ReqPredicate DEFAULT 0;

IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NOT NULL
	AND NOT EXISTS (SELECT 1 FROM sys.columns
		WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate')
		  AND name = N'FactualPredicateKeywords')
	ALTER TABLE POLOXI.Legal_DecisionDomainPackOutcomeCandidate
		ADD FactualPredicateKeywords NVARCHAR(600) NULL;

GO

-- ── Seed C9 Default judgment predicate (global default rows) ─────────────────
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
	AND OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_DecisionDomainPackOutcomeCandidate
	SET RequiresFactualPredicate = 1,
		FactualPredicateKeywords = N'default judgment|default|failed to appear|failure to appear|failed to defend|failure to defend|did not appear|did not respond|no appearance|no response|non-appearance|defaulted',
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE DecisionDomainPackId = @PiPackId
	  AND OutcomeCode = N'C9'
	  AND IsDeleted = 0;
END

GO

COMMIT TRANSACTION;
GO
