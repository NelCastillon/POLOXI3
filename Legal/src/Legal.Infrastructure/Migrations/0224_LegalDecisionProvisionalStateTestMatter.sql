SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — PROVISIONAL_DECISION TERMINAL-STATE TEST MATTER (M5 / S5).
--
-- Purpose: exercise and lock in the corrected terminal-state invariant (§32-34):
--
--     RESEARCH_EXHAUSTED ⇒ no executable, sufficiently valuable research action remains.
--
-- The prior engine wrongly declared RESEARCH_EXHAUSTED whenever candidate entropy was high. This
-- matter reproduces EXACTLY that condition and asserts the CORRECT outcome:
--
--     • Near-tie candidates (composite 0.556 vs 0.544 ⇒ margin 0.012)  → very high entropy (~0.998)
--     • An OPEN frontier branch with high ADV (0.72) and high Information Value (0.75)
--     • A high-impact Next Best Action that CAN still move the decision
--
--   Given ResolveTerminalState(margin, entropy, frontierOpen=true, maxAvailableAdv=0.72) and the
--   seeded exhaustion floor (ThresholdResearchExhaustionAdv = 0.10), the engine MUST classify this
--   as PROVISIONAL_DECISION ("leading outcome identified, high-value research remains") — NOT
--   RESEARCH_EXHAUSTED. The leading answer still composes, but is honestly labeled provisional.
--
-- Idempotent: keyed on deterministic GUIDs (A5000000-*) so re-running never duplicates rows.
-- Tenant-scoped to the development demo tenant. Runs only after the base decision tables exist.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- Deterministic ids ── Matter 5 (Meridian Mutual — coverage / exclusion-clause dispute).
DECLARE @M5   UNIQUEIDENTIFIER = N'A5000000-0000-0000-0000-000000000001';
DECLARE @S5   UNIQUEIDENTIFIER = N'A5000000-0000-0000-0000-000000000010';
DECLARE @S5C1 UNIQUEIDENTIFIER = N'A5000000-0000-0000-0000-000000000021';  -- narrow leader (No Coverage)
DECLARE @S5C2 UNIQUEIDENTIFIER = N'A5000000-0000-0000-0000-000000000022';  -- near-tied rival (Coverage)
DECLARE @S5B1 UNIQUEIDENTIFIER = N'A5000000-0000-0000-0000-000000000031';  -- OPEN high-value frontier
DECLARE @S5B2 UNIQUEIDENTIFIER = N'A5000000-0000-0000-0000-000000000032';  -- resolved branch

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND OBJECT_ID(N'POLOXI.Legal_DecisionSession', N'U') IS NOT NULL
BEGIN
	-- ── Matter ──────────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M5 AND TenantId = @Tenant)
		INSERT POLOXI.Legal_DecisionMatter
			(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
			 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
			 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
			 TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(@M5,
			 N'Meridian Mutual v. Cascade Foods - Coverage / Exclusion Clause Dispute',
			 N'Civil Litigation - Insurance Coverage',
			 N'United States - Washington State - Superior Court - King County - Governing law: Washington',
			 N'Declaratory judgment - Movant: Meridian Mutual - Target: applicability of the pollution exclusion - Requested disposition: declaration of no coverage',
			 N'Type: Civil Litigation (Insurance Coverage).' + NCHAR(10)
			  + N'Jurisdiction: United States, Washington Superior Court, King County; governing law: Washington.' + NCHAR(10)
			  + N'Posture: Cross-motions for summary judgment on coverage. Moving party: Meridian Mutual (insurer). Responding party: Cascade Foods (insured). Requested disposition: a declaration that the pollution exclusion bars coverage for the contamination claim.' + NCHAR(10)
			  + N'Core question: whether the pollution exclusion applies to the ammonia release. The two outcomes are almost evenly matched on the current record; the dispositive, still-open fact is whether the release qualifies as a "pollutant" discharge within the exclusion or an excepted product-handling incident. That open frontier fact carries very high information value, so the decision is provisional - research is NOT exhausted.',
			 N'OPEN',
			 N'Insurance Coverage',
			 N'United States - State',
			 N'Washington',
			 N'Superior Court',
			 N'King County',
			 N'Washington',
			 N'Meridian Mutual',
			 N'Cascade Foods',
			 N'Applicability of the pollution exclusion',
			 N'Declaration of no coverage',
			 @Tenant, DATEADD(DAY, -2, @Now), @User);

	-- ── Session (persisted decision run) ── PROVISIONAL_DECISION with an OPEN high-value frontier ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S5)
		INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
			TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
			DepthReached, LlmCallCount, DurationMs, FinalAnswer, MatterId, NextBestActionText, NextBestActionImpactCode,
			NextBestActionRationale, UseDependencyGraph, ReadinessSatisfied, ReadinessBlockersJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S5,
			N'Does the pollution exclusion bar coverage for the ammonia release at the Cascade Foods facility?',
			N'LEGAL', N'gpt-4.1-mini', 1, N'PROVISIONAL_DECISION', N'PROVISIONAL_DECISION',
			N'Leading outcome identified, but a high-value investigation remains on the frontier', @S5C1,
			0.7400, 0.998000, 0.012000,
			3, 8, 8760,
			N'On the current record this is a PROVISIONAL conclusion: coverage is slightly more likely to be BARRED by the pollution exclusion (No Coverage leads 0.556 to 0.544), but the margin is razor-thin and candidate uncertainty is near maximum. The single dispositive fact - whether the ammonia release is a "pollutant" discharge within the exclusion or an excepted product-handling incident - is still OPEN and carries very high information value. Research is NOT exhausted: resolving the exclusion-clause applicability could flip the outcome, so the recommended next step is to investigate that fact before relying on this decision.',
			@M5, N'Investigate whether the ammonia release qualifies as a "pollutant" discharge within the pollution exclusion (product-handling exception).',
			N'VERY HIGH',
			N'This is the dispositive, still-open fact. It has the highest remaining information value on the frontier (IV 0.75) and a high flip potential (0.85); resolving it could overturn the current narrow leader. Because an executable high-value research action remains, the correct terminal state is PROVISIONAL_DECISION, not RESEARCH_EXHAUSTED.',
			1, 0, N'["Essential exclusion-applicability fact is unverified.","Winner leads by only 0.012 with near-maximum entropy (0.998).","A high-value frontier branch (ADV 0.72) remains open."]',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Candidates (near-tie ⇒ high entropy, but a clear ranked leader exists) ─────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S5C1)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S5C1, @S5, N'C1', N'No coverage - exclusion applies', N'The pollution exclusion bars coverage for the ammonia release.',
			0.5800, 0.5400, 0.5200, 0.5900, 0.5000, 0.4900, 0.5200, 0.5400, 0.556000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S5C2)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S5C2, @S5, N'C2', N'Coverage - product-handling exception applies', N'The release is an excepted product-handling incident, so coverage is owed.',
			0.5600, 0.5300, 0.5100, 0.5700, 0.5200, 0.5000, 0.5000, 0.5200, 0.544000, 2, 0, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Branches: B1 OPEN with high ADV/IV (research remains) + B2 resolved ─────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S5B1)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S5B1, @S5, 1, N'C2.B1', N'Does the ammonia release qualify as a "pollutant" discharge under the exclusion?',
			N'The dispositive, still-open fact: whether the release is a pollutant discharge within the exclusion or an excepted product-handling incident. Highest remaining information value on the frontier.',
			N'ACTIVE', 0.750000, 0.8800, 0.8500, 0.5000, 0.720000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S5B2)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S5B2, @S5, 1, N'C1.B1', N'Was the exclusion properly incorporated into the policy?',
			N'Whether the pollution exclusion endorsement was validly attached and disclosed - resolved in the insurer''s favor on the current record.',
			N'RESOLVED', 0.380000, 0.5400, 0.2600, 0.8000, 0.340000, 0, 2, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Evidence ────────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A5000000-0000-0000-0000-000000000041')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A5000000-0000-0000-0000-000000000041', @S5, @S5B1, N'Quadrant Corp. v. Am. States Ins. Co., 154 Wn.2d 165 (2005)',
			N'Quadrant Corp. v. American States', N'Washington construes pollution exclusions narrowly and asks whether the substance acted as a "pollutant" in the context of the release.',
			0.8200, 0.7600, 0.700000, N'VERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A5000000-0000-0000-0000-000000000042')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A5000000-0000-0000-0000-000000000042', @S5, @S5B1, N'Facility incident report + ammonia release characterization (disputed)',
			N'Facility incident report', N'Whether the ammonia functioned as a pollutant discharge or an excepted product-handling incident is disputed and not yet verified - this is the dispositive open fact.',
			0.5400, 0.7000, 0.500000, N'UNVERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Flip point (resolving the open fact can flip the narrow leader) ────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFlipPoint WHERE DecisionFlipPointId = N'A5000000-0000-0000-0000-000000000051')
		INSERT POLOXI.Legal_DecisionFlipPoint (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description,
			ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A5000000-0000-0000-0000-000000000051', @S5, @S5B1,
			N'If the release is found to be an excepted product-handling incident (not a pollutant discharge), the winner flips from "No Coverage" to "Coverage".',
			0.012000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Dependency (essential, unverified ⇒ keeps the frontier open) ───────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependency WHERE DecisionDependencyId = N'A5000000-0000-0000-0000-000000000061')
		INSERT POLOXI.Legal_DecisionDependency (DecisionDependencyId, DecisionSessionId, DecisionCandidateId, NodeKind, Statement,
			Support, IsEssential, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A5000000-0000-0000-0000-000000000061', @S5, @S5C1, N'ELEMENT',
			N'The ammonia release qualifies as a "pollutant" discharge within the pollution exclusion (essential to the No Coverage outcome).',
			0.5200, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Event-sourced timeline (terminates PROVISIONAL, not RESOLVED/EXHAUSTED) ─────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @S5)
		INSERT POLOXI.Legal_DecisionEvent (DecisionEventId, DecisionSessionId, SequenceNumber, EventType, StageCode, PayloadJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(N'A5000000-0000-0000-0000-000000000071', @S5, 1, N'SESSION_STARTED',     N'DISCOVERY', N'{"query":"pollution exclusion coverage"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A5000000-0000-0000-0000-000000000072', @S5, 2, N'CANDIDATES_PROPOSED', N'DISCOVERY', N'{"count":2}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A5000000-0000-0000-0000-000000000073', @S5, 3, N'CANDIDATES_SCORED',   N'COMPETITION', N'{"margin":0.012,"entropy":0.998}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A5000000-0000-0000-0000-000000000074', @S5, 4, N'EVIDENCE_RETRIEVED',  N'RETRIEVAL', N'{"evidenceCount":2}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A5000000-0000-0000-0000-000000000075', @S5, 5, N'FRONTIER_UPDATED',    N'RANKING',   N'{"frontier":["B1"],"maxAdv":0.72}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A5000000-0000-0000-0000-000000000076', @S5, 6, N'TERMINAL_STATE',      N'CONVERGENCE', N'{"statusCode":"PROVISIONAL_DECISION","reason":"LEADING_OUTCOME_WITH_OPEN_HIGH_VALUE_FRONTIER"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A5000000-0000-0000-0000-000000000077', @S5, 7, N'ANSWER_COMPOSED',     N'ANSWER',    N'{"provisional":true}', @Tenant, DATEADD(DAY, -1, @Now), @User);
END

COMMIT TRANSACTION;
