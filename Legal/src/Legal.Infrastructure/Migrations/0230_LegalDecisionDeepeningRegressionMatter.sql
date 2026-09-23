SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — BOUNDED ADAPTIVE-DEEPENING REGRESSION MATTER (M8 / S8).
--
-- Purpose: exercise and lock in the bounded adaptive-deepening loop end-to-end at the persistence
-- layer. Every other seeded matter (M4-M7) is FLAT: all branches sit at LevelNumber = 1. This matter
-- is the only one that reaches LevelNumber = 2 through a genuine parent → child branch hierarchy, so
-- the cockpit, readback, DepthReached, and BranchCode.Split('.')[0] candidate-attribution logic are
-- all validated against a real deepened tree.
--
-- The tree mirrors what MaterializeBranch(...) produces at runtime for an eligible coarse branch:
--
--     C1.B1        (L1) — coarse "which contract governs?" branch, ON FRONTIER, high flip (0.72)
--       └ C1.B1.B1 (L2) — decisive sub-question the coarse branch deepened into (ParentDecisionBranchId = C1.B1)
--     C2.B1        (L1) — challenger's competing theory, on frontier
--
--   Deepening gate satisfied by C1.B1: onFrontier = 1, FlipPotential 0.72 >= DeepeningFlip 0.40,
--   level 1 < MaxDepth 4, and a child was proposed. C2.B1 has flip 0.30 (< 0.40) so it does NOT
--   deepen — the seed reflects the same asymmetric gate the runtime applies.
--
-- Terminal state: PROVISIONAL_DECISION (leading outcome with an open high-value frontier still under
-- active deepening), consistent with M5's semantics.
--
-- Idempotent: keyed on deterministic GUIDs (A8000000-*) so re-running never duplicates rows.
-- Tenant-scoped to the development demo tenant. Runs only after the base decision tables exist.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- Deterministic ids ── Matter 8 (Northwind Foods — which limitation-of-liability clause governs).
DECLARE @M8     UNIQUEIDENTIFIER = N'A8000000-0000-0000-0000-000000000001';
DECLARE @S8     UNIQUEIDENTIFIER = N'A8000000-0000-0000-0000-000000000010';
DECLARE @S8C1   UNIQUEIDENTIFIER = N'A8000000-0000-0000-0000-000000000021';  -- leader (MSA clause governs)
DECLARE @S8C2   UNIQUEIDENTIFIER = N'A8000000-0000-0000-0000-000000000022';  -- rival (PO clause governs)
DECLARE @S8B1   UNIQUEIDENTIFIER = N'A8000000-0000-0000-0000-000000000031';  -- L1 coarse frontier branch (C1.B1)
DECLARE @S8B1a  UNIQUEIDENTIFIER = N'A8000000-0000-0000-0000-000000000033';  -- L2 deepened child (C1.B1.B1)
DECLARE @S8B2   UNIQUEIDENTIFIER = N'A8000000-0000-0000-0000-000000000032';  -- L1 challenger branch (C2.B1)

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND OBJECT_ID(N'POLOXI.Legal_DecisionSession', N'U') IS NOT NULL
BEGIN
	-- ── Matter ─────────────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M8 AND TenantId = @Tenant)
		INSERT POLOXI.Legal_DecisionMatter
			(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
			 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
			 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
			 TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(@M8,
			 N'Northwind Foods v. Cascade Packaging - Which Limitation-of-Liability Clause Governs',
			 N'Civil Litigation - Contract',
			 N'United States - Washington State - Superior Court - King County - Governing law: Washington',
			 N'Cross-motions on contract interpretation - Movant: Cascade Packaging - Target: applicable limitation-of-liability cap - Requested disposition: enforce the Master Supply Agreement cap',
			 N'Type: Civil Litigation (Contract).' + NCHAR(10)
			  + N'Jurisdiction: United States, Washington Superior Court, King County; governing law: Washington.' + NCHAR(10)
			  + N'Posture: The parties dispute which of two overlapping documents supplies the controlling limitation-of-liability cap - the negotiated Master Supply Agreement (MSA) or the pre-printed terms on a later purchase order (PO).' + NCHAR(10)
			  + N'Core question: which clause governs. The leading answer (the MSA cap controls under its integration and order-of-precedence clause) separates, but a genuine sub-question remains open and is being actively deepened: whether the later PO''s conflicting terms were incorporated by a course-of-dealing exception. Because that decisive sub-question is still on the frontier, the decision is PROVISIONAL, not ready.',
			 N'OPEN',
			 N'Contract',
			 N'United States - State',
			 N'Washington',
			 N'Superior Court',
			 N'King County',
			 N'Washington',
			 N'Cascade Packaging',
			 N'Northwind Foods',
			 N'Applicable limitation-of-liability cap',
			 N'Enforce the Master Supply Agreement cap',
			 @Tenant, DATEADD(DAY, -2, @Now), @User);

	-- ── Session (persisted decision run) ── PROVISIONAL_DECISION, open frontier, DepthReached = 2 ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S8)
		INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
			TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
			DepthReached, LlmCallCount, DurationMs, FinalAnswer, MatterId, NextBestActionText, NextBestActionImpactCode,
			NextBestActionRationale, UseDependencyGraph, ReadinessSatisfied, ReadinessBlockersJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S8,
			N'Which limitation-of-liability clause governs - the Master Supply Agreement or the later purchase order?',
			N'LEGAL', N'gpt-4.1-mini', 1, N'PROVISIONAL_DECISION', N'PROVISIONAL_DECISION',
			N'Leading outcome with open high-value frontier still under active deepening', @S8C1,
			1.0000, 0.940000, 0.150000,
			2, 8, 7340,
			N'This is a PROVISIONAL decision. The Master Supply Agreement cap currently leads (0.640 to 0.490), because the MSA''s integration and order-of-precedence clause ordinarily overrides pre-printed purchase-order terms. However, a decisive sub-question was opened by deepening the governing-document analysis: whether the later PO''s conflicting terms were incorporated through a course-of-dealing exception. That sub-question remains on the frontier and, if resolved for the buyer, could flip the controlling cap. The recommended next action targets exactly that open sub-question.',
			@M8, N'Resolve the deepened sub-question: pull the parties'' prior transaction history to test whether the PO''s conflicting cap was incorporated by course of dealing.',
			N'HIGH',
			N'The governing-document branch was deepened into a decisive sub-question (course-of-dealing incorporation of the PO cap). It is on the frontier with high flip potential, so resolving it is the single highest-value action and can change the controlling cap. Until it is resolved the decision stays PROVISIONAL.',
			1, 0, N'["Open high-value frontier sub-question: course-of-dealing incorporation of the PO cap"]',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Candidates (narrow separation ⇒ high entropy, leader still ahead) ─────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S8C1)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S8C1, @S8, N'C1', N'Master Supply Agreement cap governs', N'The MSA''s integration and order-of-precedence clause controls, so its limitation-of-liability cap applies over the later purchase order.',
			0.6600, 0.6200, 0.6000, 0.6800, 0.6400, 0.3600, 0.5800, 0.6200, 0.640000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S8C2)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S8C2, @S8, N'C2', N'Purchase-order cap governs', N'The later purchase order''s conflicting cap was incorporated by course of dealing and supplies the controlling limitation.',
			0.5000, 0.4900, 0.4800, 0.4900, 0.5000, 0.5000, 0.4700, 0.4900, 0.490000, 2, 0, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Branches: a REAL two-level tree (L1 coarse frontier branch deepened into an L2 sub-branch) ──
	-- L1 (C1.B1): coarse governing-document branch, ON FRONTIER, flip 0.72 >= DeepeningFlip 0.40 ⇒ deepens.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S8B1)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, ParentDecisionBranchId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S8B1, @S8, NULL, 1, N'C1.B1', N'Which document governs the limitation-of-liability cap?',
			N'Coarse governing-document question. The MSA''s order-of-precedence clause ordinarily controls, but the question is high-flip because a later conflicting PO exists - so the branch is deepened into the decisive incorporation sub-question below.',
			N'ACTIVE', 0.652500, 0.8000, 0.7200, 0.5500, 0.375840, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- L2 (C1.B1.B1): the decisive sub-question the coarse branch deepened into. ParentDecisionBranchId = C1.B1.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S8B1a)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, ParentDecisionBranchId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S8B1a, @S8, @S8B1, 2, N'C1.B1.B1', N'Was the PO''s conflicting cap incorporated by course of dealing?',
			N'The deepened, decisive sub-question. If the parties'' prior transaction history incorporated the PO''s conflicting cap despite the MSA''s precedence clause, the controlling cap flips. On the frontier and unresolved - this is why the overall decision is provisional.',
			N'ACTIVE', 0.647500, 0.7800, 0.7000, 0.4500, 0.353535, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- L1 (C2.B1): challenger branch. Flip 0.30 < DeepeningFlip 0.40 ⇒ does NOT deepen (asymmetric gate).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S8B2)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, ParentDecisionBranchId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S8B2, @S8, NULL, 1, N'C2.B1', N'Does the PO''s pre-printed cap control on its own terms?',
			N'The challenger''s standalone theory that the PO cap controls without a course-of-dealing bridge. Low flip - the MSA precedence clause defeats it absent incorporation - so this branch is not worth deepening.',
			N'ACTIVE', 0.470000, 0.5000, 0.3000, 0.6000, 0.070500, 1, 2, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Evidence (attached to the deepened L2 sub-question - the decisive open element) ───────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A8000000-0000-0000-0000-000000000041')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A8000000-0000-0000-0000-000000000041', @S8, @S8B1, N'Master Supply Agreement §14.2 (integration and order-of-precedence)',
			N'MSA §14.2', N'In the event of conflict between this Agreement and any purchase order, the terms of this Agreement control.',
			0.9000, 0.8800, 0.900000, N'VERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A8000000-0000-0000-0000-000000000042')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A8000000-0000-0000-0000-000000000042', @S8, @S8B1a, N'RCW 62A.2-207 and prior-course-of-dealing transaction file (partial, unverified)',
			N'UCC 2-207 / course of dealing', N'Whether repeated acceptance of PO terms over prior orders established a course of dealing that incorporates the conflicting cap - the transaction history is incomplete and not yet verified.',
			0.6500, 0.7200, 0.400000, N'UNVERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Flip point (on the deepened L2 sub-question ⇒ winner-changing) ───────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFlipPoint WHERE DecisionFlipPointId = N'A8000000-0000-0000-0000-000000000051')
		INSERT POLOXI.Legal_DecisionFlipPoint (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description,
			ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A8000000-0000-0000-0000-000000000051', @S8, @S8B1a,
			N'If course of dealing is found to incorporate the PO''s conflicting cap, the controlling limitation flips from the MSA cap (C1) to the PO cap (C2).',
			0.150000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Dependency (essential, still open ⇒ drives the provisional state) ─────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependency WHERE DecisionDependencyId = N'A8000000-0000-0000-0000-000000000061')
		INSERT POLOXI.Legal_DecisionDependency (DecisionDependencyId, DecisionSessionId, DecisionCandidateId, NodeKind, Statement,
			Support, IsEssential, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A8000000-0000-0000-0000-000000000061', @S8, @S8C1, N'ELEMENT',
			N'The MSA''s order-of-precedence clause controls only if the PO''s conflicting cap was NOT incorporated by course of dealing (essential to the MSA-cap outcome; not yet verified).',
			0.4000, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Event-sourced timeline (records the deepening step; terminates PROVISIONAL_DECISION) ──────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @S8)
		INSERT POLOXI.Legal_DecisionEvent (DecisionEventId, DecisionSessionId, SequenceNumber, EventType, StageCode, PayloadJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(N'A8000000-0000-0000-0000-000000000071', @S8, 1, N'SESSION_STARTED',     N'DISCOVERY',   N'{"query":"which limitation-of-liability clause governs"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A8000000-0000-0000-0000-000000000072', @S8, 2, N'CANDIDATES_PROPOSED', N'DISCOVERY',   N'{"count":2}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A8000000-0000-0000-0000-000000000073', @S8, 3, N'CANDIDATES_SCORED',   N'COMPETITION', N'{"margin":0.150,"entropy":0.940}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A8000000-0000-0000-0000-000000000074', @S8, 4, N'BRANCH_DEEPENED',     N'COMPETITION', N'{"depth":2,"deepenedBranchCount":1,"deepeningFlip":0.40,"maxDepth":4}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A8000000-0000-0000-0000-000000000075', @S8, 5, N'EVIDENCE_RETRIEVED',  N'RETRIEVAL',   N'{"evidenceCount":2,"verified":1,"unverified":1}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A8000000-0000-0000-0000-000000000076', @S8, 6, N'FRONTIER_UPDATED',    N'RANKING',     N'{"frontier":["C1.B1","C1.B1.B1","C2.B1"],"maxAdv":0.376}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A8000000-0000-0000-0000-000000000077', @S8, 7, N'TERMINAL_STATE',      N'CONVERGENCE', N'{"statusCode":"PROVISIONAL_DECISION","reason":"LEADING_OUTCOME_WITH_OPEN_HIGH_VALUE_FRONTIER"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A8000000-0000-0000-0000-000000000078', @S8, 8, N'ANSWER_COMPOSED',     N'ANSWER',      N'{"provisional":true}', @Tenant, DATEADD(DAY, -1, @Now), @User);
END

COMMIT TRANSACTION;
