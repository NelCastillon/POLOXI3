SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — DECISION_READY TERMINAL-STATE TEST MATTER (M6 / S6).
--
-- Purpose: the positive counterpart to matter M5 (PROVISIONAL_DECISION). This matter exercises and
-- locks in the CONVERGED terminal state (§32-34):
--
--     DECISION_READY ⇔ a leading outcome clearly separates, uncertainty is contained,
--                       and no high-value research action remains on the frontier.
--
-- It is deliberately constructed so the cockpit can honestly show a STRONG verdict:
--
--     • Decisive candidates (composite 0.780 vs 0.340 ⇒ margin 0.440)  → contained entropy (~0.550)
--     • NO open frontier branch — both branches are RESOLVED (maxAdv 0.28 < seeded ADV floor 0.10? no —
--       the point is nothing is ON the frontier, so no executable high-value research remains)
--     • Verified supporting evidence on the dispositive element
--     • A LOW-impact Next Best Action (optional corroboration only — cannot move the decision)
--
--   Given ResolveTerminalState(margin=0.44, entropy=0.55, frontierOpen=false, maxAvailableAdv=0.28)
--   the engine MUST classify this as DECISION_READY. Because the status is genuinely DECISION_READY,
--   the cockpit verdict may render the "strong" treatment; all readiness items pass; and the single
--   flip point is non-winner-changing (RankDelta 0) — consistent with the corrected projection logic.
--
-- Idempotent: keyed on deterministic GUIDs (A6000000-*) so re-running never duplicates rows.
-- Tenant-scoped to the development demo tenant. Runs only after the base decision tables exist.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- Deterministic ids ── Matter 6 (Riverside Logistics — statute-of-limitations dismissal).
DECLARE @M6   UNIQUEIDENTIFIER = N'A6000000-0000-0000-0000-000000000001';
DECLARE @S6   UNIQUEIDENTIFIER = N'A6000000-0000-0000-0000-000000000010';
DECLARE @S6C1 UNIQUEIDENTIFIER = N'A6000000-0000-0000-0000-000000000021';  -- decisive leader (Time-barred)
DECLARE @S6C2 UNIQUEIDENTIFIER = N'A6000000-0000-0000-0000-000000000022';  -- weak rival (Not time-barred)
DECLARE @S6B1 UNIQUEIDENTIFIER = N'A6000000-0000-0000-0000-000000000031';  -- resolved dispositive branch
DECLARE @S6B2 UNIQUEIDENTIFIER = N'A6000000-0000-0000-0000-000000000032';  -- resolved secondary branch

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND OBJECT_ID(N'POLOXI.Legal_DecisionSession', N'U') IS NOT NULL
BEGIN
	-- ── Matter ──────────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M6 AND TenantId = @Tenant)
		INSERT POLOXI.Legal_DecisionMatter
			(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
			 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
			 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
			 TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(@M6,
			 N'Riverside Logistics v. Delta Warehousing - Statute of Limitations Dismissal',
			 N'Civil Litigation - Contract',
			 N'United States - Oregon State - Circuit Court - Multnomah County - Governing law: Oregon',
			 N'Motion to dismiss - Movant: Delta Warehousing - Target: breach-of-contract claim as time-barred - Requested disposition: dismissal with prejudice',
			 N'Type: Civil Litigation (Contract).' + NCHAR(10)
			  + N'Jurisdiction: United States, Oregon Circuit Court, Multnomah County; governing law: Oregon.' + NCHAR(10)
			  + N'Posture: Motion to dismiss on statute-of-limitations grounds. Moving party: Delta Warehousing (defendant). Responding party: Riverside Logistics (plaintiff). Requested disposition: dismissal with prejudice because the claim was filed outside the limitations period.' + NCHAR(10)
			  + N'Core question: whether the six-year written-contract limitations period (ORS 12.080) had run before filing. The accrual date and filing date are undisputed on the record and place the claim well outside the period; no tolling doctrine applies. The leading outcome (time-barred) separates decisively, uncertainty is contained, and no high-value research remains - the decision is READY, not provisional.',
			 N'OPEN',
			 N'Contract',
			 N'United States - State',
			 N'Oregon',
			 N'Circuit Court',
			 N'Multnomah County',
			 N'Oregon',
			 N'Delta Warehousing',
			 N'Riverside Logistics',
			 N'Breach-of-contract claim as time-barred',
			 N'Dismissal with prejudice',
			 @Tenant, DATEADD(DAY, -2, @Now), @User);

	-- ── Session (persisted decision run) ── DECISION_READY, no open frontier ──────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S6)
		INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
			TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
			DepthReached, LlmCallCount, DurationMs, FinalAnswer, MatterId, NextBestActionText, NextBestActionImpactCode,
			NextBestActionRationale, UseDependencyGraph, ReadinessSatisfied, ReadinessBlockersJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S6,
			N'Is Riverside Logistics'' breach-of-contract claim barred by the statute of limitations?',
			N'LEGAL', N'gpt-4.1-mini', 1, N'DECISION_READY', N'DECISION_READY',
			N'Leading outcome clearly separates, uncertainty contained, no high-value research remains', @S6C1,
			1.0000, 0.550000, 0.440000,
			3, 7, 6120,
			N'This is a READY decision: the breach-of-contract claim is TIME-BARRED (Time-barred leads decisively, 0.780 to 0.340). The accrual date and filing date are undisputed and place the filing well beyond the six-year written-contract limitations period (ORS 12.080), and no tolling doctrine applies on the record. Candidate uncertainty is contained and no high-value investigation remains open, so the recommended disposition - dismissal with prejudice - can be relied upon.',
			@M6, N'Optionally corroborate the undisputed accrual date against the executed contract''s delivery schedule.',
			N'LOW',
			N'This step is optional corroboration only. The accrual and filing dates are already undisputed and verified on the record, so this action cannot move the decision; it merely strengthens the file. Because no executable high-value research action remains, the correct terminal state is DECISION_READY.',
			1, 1, N'[]',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Candidates (decisive separation ⇒ contained entropy, clear ranked leader) ──────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S6C1)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S6C1, @S6, N'C1', N'Time-barred - claim dismissed', N'The breach-of-contract claim is barred by the six-year statute of limitations and is dismissed with prejudice.',
			0.8200, 0.8000, 0.7800, 0.8100, 0.8500, 0.2000, 0.8000, 0.8200, 0.780000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S6C2)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S6C2, @S6, N'C2', N'Not time-barred - claim proceeds', N'A tolling or later-accrual theory saves the claim, so it may proceed on the merits.',
			0.3600, 0.3200, 0.3300, 0.3500, 0.3400, 0.6000, 0.3200, 0.3400, 0.340000, 2, 0, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Branches: both RESOLVED ⇒ nothing on the frontier (no executable high-value research) ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S6B1)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S6B1, @S6, 1, N'C1.B1', N'Had the six-year limitations period run before filing?',
			N'The dispositive question: whether the claim was filed within ORS 12.080. Undisputed accrual and filing dates place it outside the period - resolved against the plaintiff.',
			N'RESOLVED', 0.280000, 0.9000, 0.1200, 0.9000, 0.280000, 0, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S6B2)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S6B2, @S6, 1, N'C2.B1', N'Does any tolling doctrine apply?',
			N'Whether discovery-rule, fraudulent-concealment, or equitable tolling could extend the period - none is supported on the record; resolved against the plaintiff.',
			N'RESOLVED', 0.220000, 0.6000, 0.1000, 0.8500, 0.220000, 0, 2, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Evidence (verified, supporting the dispositive resolved element) ────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A6000000-0000-0000-0000-000000000041')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A6000000-0000-0000-0000-000000000041', @S6, @S6B1, N'ORS 12.080 (Oregon six-year limitations - written contracts)',
			N'ORS 12.080', N'Actions upon a contract or liability, express or implied, must be commenced within six years.',
			0.9000, 0.8800, 0.900000, N'VERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A6000000-0000-0000-0000-000000000042')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A6000000-0000-0000-0000-000000000042', @S6, @S6B1, N'Undisputed accrual date (breach) and filing-date stamp from the docket',
			N'Accrual and filing dates', N'The breach accrued on 2016-03-14 and the complaint was filed 2023-08-02 - roughly seven years and five months later, outside the six-year period. Both dates are undisputed.',
			0.8600, 0.8500, 0.880000, N'VERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Flip point (non-winner-changing ⇒ RankDelta 0, consistent with corrected projection) ────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFlipPoint WHERE DecisionFlipPointId = N'A6000000-0000-0000-0000-000000000051')
		INSERT POLOXI.Legal_DecisionFlipPoint (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description,
			ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A6000000-0000-0000-0000-000000000051', @S6, @S6B2,
			N'Even if a tolling theory were entertained, it lacks record support and would not overtake the decisive limitations bar - the winner does not change.',
			0.440000, 0, 0, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Dependency (essential AND satisfied ⇒ no open requirement) ──────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependency WHERE DecisionDependencyId = N'A6000000-0000-0000-0000-000000000061')
		INSERT POLOXI.Legal_DecisionDependency (DecisionDependencyId, DecisionSessionId, DecisionCandidateId, NodeKind, Statement,
			Support, IsEssential, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A6000000-0000-0000-0000-000000000061', @S6, @S6C1, N'ELEMENT',
			N'The claim was filed after the six-year limitations period expired (essential to, and verified for, the Time-barred outcome).',
			0.8800, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Event-sourced timeline (terminates DECISION_READY / converged) ──────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @S6)
		INSERT POLOXI.Legal_DecisionEvent (DecisionEventId, DecisionSessionId, SequenceNumber, EventType, StageCode, PayloadJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(N'A6000000-0000-0000-0000-000000000071', @S6, 1, N'SESSION_STARTED',     N'DISCOVERY',   N'{"query":"statute of limitations breach of contract"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A6000000-0000-0000-0000-000000000072', @S6, 2, N'CANDIDATES_PROPOSED', N'DISCOVERY',   N'{"count":2}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A6000000-0000-0000-0000-000000000073', @S6, 3, N'CANDIDATES_SCORED',   N'COMPETITION', N'{"margin":0.440,"entropy":0.550}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A6000000-0000-0000-0000-000000000074', @S6, 4, N'EVIDENCE_RETRIEVED',  N'RETRIEVAL',   N'{"evidenceCount":2,"verified":2}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A6000000-0000-0000-0000-000000000075', @S6, 5, N'FRONTIER_UPDATED',    N'RANKING',     N'{"frontier":[],"maxAdv":0.28}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A6000000-0000-0000-0000-000000000076', @S6, 6, N'TERMINAL_STATE',      N'CONVERGENCE', N'{"statusCode":"DECISION_READY","reason":"LEADING_OUTCOME_SEPARATES_NO_OPEN_FRONTIER"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A6000000-0000-0000-0000-000000000077', @S6, 7, N'ANSWER_COMPOSED',     N'ANSWER',      N'{"provisional":false}', @Tenant, DATEADD(DAY, -1, @Now), @User);
END

COMMIT TRANSACTION;
