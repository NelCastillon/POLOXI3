-- ============================================================================
-- 0450: Disposition-archetype classification for canonical Decision Outcome candidates.
--
-- WHY THIS EXISTS:
--   The Wide2 Candidate x Branch competition scores each competing legal
--   disposition against the decision-factor (legal element) axes. A flat
--   element-weighted average is the WRONG aggregation for a legal outcome: a
--   negligence claim where causation sits below the preponderance threshold
--   must make the DEFENSE outcome score high and the PLAINTIFF outcome score
--   low, yet an average of all elements produces the same mushy number for
--   both. The correct model gates over the element vector by the outcome's
--   LEGAL ARCHETYPE:
--
--     PLAINTIFF_MERITS -- conjunctive (noisy-AND): plaintiff must prove EVERY
--                         element, so the weakest established element caps it.
--     DEFENSE_MERITS   -- disjunctive over element FAILURES: the defense wins if
--                         ANY element fails (= 1 - plaintiff conjunction).
--     SETTLEMENT       -- tension/uncertainty: peaks when the elements are
--                         genuinely MIXED (near the burden threshold).
--     NEUTRAL          -- membership-weighted average (procedural/other).
--
-- CORRECTION (data-driven, DB is source of truth):
--   Add ArchetypeCode so each outcome row declares its own gate archetype. The
--   application reads this column instead of inferring the archetype from the
--   outcome name with keyword heuristics; keywords remain only as a fail-soft
--   fallback when a row has no ArchetypeCode. No outcome name is hardcoded in
--   application code.
--
-- Depends on the table created by migration 0396. Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ── Add the archetype column (idempotent) ──────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NOT NULL
	AND NOT EXISTS (SELECT 1 FROM sys.columns
		WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate')
		  AND name = N'ArchetypeCode')
	ALTER TABLE POLOXI.Legal_DecisionDomainPackOutcomeCandidate
		ADD ArchetypeCode NVARCHAR(40) NULL;  -- PLAINTIFF_MERITS | DEFENSE_MERITS | SETTLEMENT | NEUTRAL

GO

-- ── Seed PERSONAL_INJURY C1-C9 archetypes (global default rows) ─────────────
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
	AND OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NOT NULL
BEGIN
	DECLARE @Archetypes TABLE (OutcomeCode NVARCHAR(60), ArchetypeCode NVARCHAR(40));
	INSERT INTO @Archetypes (OutcomeCode, ArchetypeCode) VALUES
		(N'C1', N'SETTLEMENT'),        -- Confidential negotiated settlement
		(N'C2', N'SETTLEMENT'),        -- Continued negotiation or mediation
		(N'C3', N'PLAINTIFF_MERITS'),  -- Plaintiff-favorable adjudication or verdict
		(N'C4', N'DEFENSE_MERITS'),    -- Defense-favorable judgment or dismissal
		(N'C5', N'SETTLEMENT'),        -- Recorded settlement disbursement
		(N'C6', N'SETTLEMENT'),        -- Arbitration or ADR award
		(N'C7', N'DEFENSE_MERITS'),    -- Procedural or jurisdictional bar (defeats the claim)
		(N'C8', N'NEUTRAL'),           -- Voluntary dismissal or withdrawal (plaintiff-initiated, non-merits)
		(N'C9', N'PLAINTIFF_MERITS');  -- Default judgment (liability follows against the defendant)

	UPDATE oc
	SET oc.ArchetypeCode = a.ArchetypeCode,
		oc.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_DecisionDomainPackOutcomeCandidate oc
	INNER JOIN @Archetypes a ON a.OutcomeCode = oc.OutcomeCode
	WHERE oc.DecisionDomainPackId = @PiPackId
	  AND oc.TenantId IS NULL
	  AND oc.IsDeleted = 0
	  AND oc.ArchetypeCode IS NULL;
END

GO

COMMIT TRANSACTION;
GO
