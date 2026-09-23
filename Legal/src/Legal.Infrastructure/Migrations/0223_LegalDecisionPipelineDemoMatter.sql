SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal V2.1 — END-TO-END PIPELINE DEMONSTRATION MATTER (M4 / S4).
--
-- This migration seeds ONE fully self-consistent matter whose sole purpose is to demonstrate and
-- TRACE the entire POLOXI Legal logic pipeline in a single click from the cockpit:
--
--   1. Decision run state         → Session + Candidates (winner + challenger) + Branches + Evidence
--   2. Typed dependency graph      → Nodes + Edges WITH EXPLICIT LINEAGE back to POLOXI branches/candidates
--   3. Closed loop (V2.1)          → an essential, UNVERIFIED edge sits on the decision frontier and is
--                                     lineage-linked to the WINNER's branch/candidate, so clicking
--                                     "Invalidate" (or "Mark verified") on that edge drives:
--                                        DependencyPropagation → ImpactMapper → Recompetition →
--                                        FrontierSnapshot → ResearchNeed → persisted readback.
--
-- The margin between the winner (C1) and challenger (C2) is deliberately NARROW (composite 0.612 vs
-- 0.588) and the dispositive fact chain runs THROUGH the essential UNVERIFIED edge, so invalidating
-- that edge measurably weakens C1 — proving the graph can change what POLOXI investigates next.
--
-- Idempotent: keyed on deterministic GUIDs (A4000000-*) so re-running never duplicates rows.
-- Tenant-scoped to the development demo tenant. Runs only after the base decision tables exist.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- Deterministic ids ── Matter 4 (Halcyon v. Orion — Preliminary Injunction, non-compete).
DECLARE @M4   UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-000000000001';
DECLARE @S4   UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-000000000010';
DECLARE @S4C1 UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-000000000021';  -- winner  (grant PI)
DECLARE @S4C2 UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-000000000022';  -- challenger (deny PI)
DECLARE @S4B1 UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-000000000031';  -- frontier branch
DECLARE @S4B2 UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-000000000032';

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND OBJECT_ID(N'POLOXI.Legal_DecisionSession', N'U') IS NOT NULL
BEGIN
	-- ── Matter ──────────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M4 AND TenantId = @Tenant)
		INSERT POLOXI.Legal_DecisionMatter
			(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
			 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
			 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
			 TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(@M4,
			 N'Halcyon Labs v. Orion Dynamics - Preliminary Injunction',
			 N'Civil Litigation - Employment / Non-Compete Enforcement',
			 N'United States - Delaware State - Court of Chancery - New Castle County - Governing law: Delaware',
			 N'Preliminary Injunction - Movant: Halcyon Labs - Target: enforcement of the non-compete covenant - Requested disposition: injunction pending trial',
			 N'Type: Civil Litigation (Employment / Non-Compete Enforcement).' + NCHAR(10)
			  + N'Jurisdiction: United States, Delaware Court of Chancery, New Castle County; governing law: Delaware.' + NCHAR(10)
			  + N'Posture: Motion for a preliminary injunction. Moving party: Halcyon Labs. Responding party: Orion Dynamics and the departed engineer. Requested disposition: injunction enforcing the non-compete pending trial.' + NCHAR(10)
			  + N'Core question: whether Halcyon shows a reasonable probability of success on the enforceability of the non-compete and a likelihood of irreparable harm from the engineer''s work at Orion. The dispositive fact on the frontier is whether the engineer actually began competitive work within the restricted field.',
			 N'OPEN',
			 N'Employment / Non-Compete Enforcement',
			 N'United States - State',
			 N'Delaware',
			 N'Court of Chancery',
			 N'New Castle County',
			 N'Delaware',
			 N'Halcyon Labs',
			 N'Orion Dynamics',
			 N'Enforcement of the non-compete covenant',
			 N'Preliminary injunction pending trial',
			 @Tenant, DATEADD(DAY, -2, @Now), @User);

	-- ── Session (persisted decision run + Next Best Action) ──────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S4)
		INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
			TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
			DepthReached, LlmCallCount, DurationMs, FinalAnswer, MatterId, NextBestActionText, NextBestActionImpactCode,
			NextBestActionRationale, UseDependencyGraph, ReadinessSatisfied, ReadinessBlockersJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S4,
			N'Should the Court of Chancery grant a preliminary injunction enforcing the non-compete against the departed engineer?',
			N'LEGAL', N'gpt-4.1-mini', 1, N'COMPLETED', N'RESOLVED', N'Decision margin exceeded threshold', @S4C1,
			0.8200, 0.598000, 0.024000,
			3, 8, 9120,
			N'On the current record, a preliminary injunction is likely to be GRANTED, but only narrowly. Halcyon shows a reasonable probability of success on enforceability, and the balance of harms favors an injunction IF the engineer has begun competitive work in the restricted field. That single frontier fact is essential and remains UNVERIFIED - if it fails, the likelihood-of-success chain collapses and the outcome flips to denial.',
			@M4, N'Verify whether the engineer has begun competitive work in the restricted field (source-control commits, project assignments). This is the essential, unverified edge on the decision frontier.',
			N'HIGH',
			N'This is the dispositive fact chain: the winner (grant) depends on it through an essential SATISFIES edge. Confirming it hardens the grant; invalidating it flips the decision to denial and reopens the frontier.',
			1, 0, N'["Essential dependency (competitive-use fact) is unverified.","Winner survives challenger by a narrow margin (0.024)."]',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Candidates (winner + challenger; NARROW margin so the loop can flip it) ────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S4C1)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S4C1, @S4, N'C1', N'Grant preliminary injunction', N'Reasonable probability of success and irreparable harm if competitive work has begun.',
			0.7800, 0.6600, 0.6400, 0.7400, 0.5600, 0.4000, 0.6600, 0.6200, 0.612000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S4C2)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S4C2, @S4, N'C2', N'Deny preliminary injunction', N'Absent proof of competitive work, no irreparable harm and the covenant''s scope is questionable.',
			0.6800, 0.6200, 0.5800, 0.6600, 0.6000, 0.4200, 0.6000, 0.5800, 0.588000, 2, 0, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Branches (frontier + resolved) ────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S4B1)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S4B1, @S4, 1, N'C2.B1', N'Has the engineer begun competitive work in the restricted field?',
			N'The dispositive fact: likelihood of success and irreparable harm both hinge on whether competitive work has actually begun.',
			N'ACTIVE', 0.780000, 0.8600, 0.7400, 0.4500, 0.700000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S4B2)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S4B2, @S4, 1, N'C1.B1', N'Is the non-compete reasonable in scope and duration?',
			N'Whether the covenant''s geographic scope and 12-month duration are enforceable under Delaware law.',
			N'RESOLVED', 0.420000, 0.5800, 0.3000, 0.7600, 0.380000, 0, 2, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Evidence ────────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A4000000-0000-0000-0000-000000000041')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A4000000-0000-0000-0000-000000000041', @S4, @S4B1, N'Kodiak Building Partners v. Adams, 2022 WL 5240507 (Del. Ch.)',
			N'Kodiak Building Partners v. Adams', N'Delaware courts narrowly enforce non-competes and require a legitimate economic interest actually threatened by competitive activity.',
			0.8600, 0.7800, 0.720000, N'VERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A4000000-0000-0000-0000-000000000042')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A4000000-0000-0000-0000-000000000042', @S4, @S4B1, N'Orion onboarding record + source-control access logs (disputed)',
			N'Orion onboarding record', N'Whether the engineer was assigned to a competing product line and committed code in the restricted field is disputed and not yet verified.',
			0.5200, 0.6800, 0.640000, N'UNVERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Flip point ──────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFlipPoint WHERE DecisionFlipPointId = N'A4000000-0000-0000-0000-000000000051')
		INSERT POLOXI.Legal_DecisionFlipPoint (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description,
			ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A4000000-0000-0000-0000-000000000051', @S4, @S4B1,
			N'If the competitive-work fact is invalidated, the likelihood-of-success chain collapses and the winner flips from "Grant" to "Deny".',
			0.024000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Dependency ──────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependency WHERE DecisionDependencyId = N'A4000000-0000-0000-0000-000000000061')
		INSERT POLOXI.Legal_DecisionDependency (DecisionDependencyId, DecisionSessionId, DecisionCandidateId, NodeKind, Statement,
			Support, IsEssential, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A4000000-0000-0000-0000-000000000061', @S4, @S4C1, N'ELEMENT',
			N'The engineer began competitive work in the restricted field (essential to likelihood of success and irreparable harm).',
			0.6400, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Event-sourced timeline ────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @S4)
		INSERT POLOXI.Legal_DecisionEvent (DecisionEventId, DecisionSessionId, SequenceNumber, EventType, StageCode, PayloadJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(N'A4000000-0000-0000-0000-000000000071', @S4, 1, N'SESSION_STARTED',     N'DISCOVERY', N'{"query":"non-compete preliminary injunction"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A4000000-0000-0000-0000-000000000072', @S4, 2, N'CANDIDATES_PROPOSED', N'DISCOVERY', N'{"count":2}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A4000000-0000-0000-0000-000000000073', @S4, 3, N'FRONTIER_UPDATED',    N'RANKING',   N'{"frontier":["B1"]}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A4000000-0000-0000-0000-000000000074', @S4, 4, N'DECISION_RESOLVED',   N'ANSWER',    N'{"winner":"C1","margin":0.024}', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Typed dependency graph (nodes + edges) WITH LINEAGE so the closed loop can trace back ──
	IF OBJECT_ID(N'POLOXI.Legal_DecisionGraphEdge', N'U') IS NOT NULL
	BEGIN
		DECLARE @Fact4  UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-0000000000F1';  -- competitive-work fact (frontier)
		DECLARE @Prop4  UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-0000000000A1';  -- non-compete enforceable proposition
		DECLARE @Elem4  UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-0000000000E1';  -- likelihood-of-success element
		DECLARE @Strat4 UNIQUEIDENTIFIER = N'A4000000-0000-0000-0000-0000000000E5';  -- grant-PI strategy

		-- Fact proposition (the frontier fact; UNVERIFIED). Lineage → winner branch/candidate.
		IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFactProposition WHERE DecisionFactPropositionId = @Fact4)
			INSERT POLOXI.Legal_DecisionFactProposition (DecisionFactPropositionId, DecisionSessionId, NodeCode, Statement, Support, IsMaterial, IsDisputed, VerificationStatus, SortOrder, SourceBranchId, SourceCandidateId, MatterId, TenantId, CreatedByUserId)
			VALUES (@Fact4, @S4, N'F1', N'The engineer began competitive work in the restricted field after joining Orion.', 0.5200, 1, 1, N'UNVERIFIED', 0, @S4B1, @S4C1, @M4, @Tenant, @User);

		IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionLegalProposition WHERE DecisionLegalPropositionId = @Prop4)
			INSERT POLOXI.Legal_DecisionLegalProposition (DecisionLegalPropositionId, DecisionSessionId, NodeCode, Statement, AuthorityRef, Support, IsMaterial, VerificationStatus, SortOrder, SourceBranchId, SourceCandidateId, MatterId, TenantId, CreatedByUserId)
			VALUES (@Prop4, @S4, N'P1', N'The non-compete protects a legitimate economic interest and is reasonable in scope under Delaware law.', N'Kodiak Building Partners v. Adams, 2022 WL 5240507 (Del. Ch.)', 0.7800, 1, N'VERIFIED', 0, @S4B2, @S4C1, @M4, @Tenant, @User);

		IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionLegalElement WHERE DecisionLegalElementId = @Elem4)
			INSERT POLOXI.Legal_DecisionLegalElement (DecisionLegalElementId, DecisionSessionId, NodeCode, DisplayName, Statement, IsEssential, IsSatisfied, Support, VerificationStatus, SortOrder, SourceBranchId, SourceCandidateId, MatterId, TenantId, CreatedByUserId)
			VALUES (@Elem4, @S4, N'E1', N'Reasonable probability of success on the merits', N'Movant must show a reasonable probability of success, which requires a protectable interest actually threatened by competitive work.', 1, 0, 0.5200, N'UNVERIFIED', 0, @S4B1, @S4C1, @M4, @Tenant, @User);

		IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionReasoningStrategy WHERE DecisionReasoningStrategyId = @Strat4)
			INSERT POLOXI.Legal_DecisionReasoningStrategy (DecisionReasoningStrategyId, DecisionSessionId, DecisionCandidateId, NodeCode, DisplayName, Rationale, Support, IsViable, VerificationStatus, SortOrder, SourceBranchId, SourceCandidateId, MatterId, TenantId, CreatedByUserId)
			VALUES (@Strat4, @S4, @S4C1, N'S1', N'Grant PI - protectable interest threatened by competitive work', N'If competitive work has begun, Halcyon shows both probability of success and irreparable harm, warranting an injunction.', 0.6200, 1, N'VERIFIED', 0, @S4B1, @S4C1, @M4, @Tenant, @User);

		-- Typed edges. The ESSENTIAL SATISFIES edge (Fact → Element) is UNVERIFIED and is the frontier;
		-- its lineage points at the WINNER's branch/candidate, so acting on it drives the closed loop.
		IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A4000000-0000-0000-0000-0000000000D1')
			INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, SourceBranchId, SourceCandidateId, MatterId, TenantId, CreatedByUserId)
			VALUES (N'A4000000-0000-0000-0000-0000000000D1', @S4, N'SATISFIES', N'FACT', @Fact4, N'ELEMENT', @Elem4, 0.5200, 0.9500, 1, 1, N'UNVERIFIED', @S4B1, @S4C1, @M4, @Tenant, @User);

		IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A4000000-0000-0000-0000-0000000000D2')
			INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, SourceBranchId, SourceCandidateId, MatterId, TenantId, CreatedByUserId)
			VALUES (N'A4000000-0000-0000-0000-0000000000D2', @S4, N'REQUIRES', N'PROPOSITION', @Prop4, N'ELEMENT', @Elem4, 0.7800, 0.9000, 1, 0, N'VERIFIED', @S4B2, @S4C1, @M4, @Tenant, @User);

		IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A4000000-0000-0000-0000-0000000000D3')
			INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, SourceBranchId, SourceCandidateId, MatterId, TenantId, CreatedByUserId)
			VALUES (N'A4000000-0000-0000-0000-0000000000D3', @S4, N'DEPENDS_ON', N'ELEMENT', @Elem4, N'STRATEGY', @Strat4, 0.6200, 0.8500, 1, 0, N'VERIFIED', @S4B1, @S4C1, @M4, @Tenant, @User);

		IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A4000000-0000-0000-0000-0000000000D4')
			INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, SourceBranchId, SourceCandidateId, MatterId, TenantId, CreatedByUserId)
			VALUES (N'A4000000-0000-0000-0000-0000000000D4', @S4, N'ESTABLISHES', N'STRATEGY', @Strat4, N'CANDIDATE', @S4C1, 0.6200, 0.9000, 1, 1, N'VERIFIED', @S4B1, @S4C1, @M4, @Tenant, @User);
	END

	-- ── Losing-side test (winner narrowly survives the strongest opposing case) ────────────────
	IF OBJECT_ID(N'POLOXI.Legal_DecisionLosingSideTest', N'U') IS NOT NULL
	   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionLosingSideTest WHERE DecisionLosingSideTestId = N'A4000000-0000-0000-0000-0000000000AA')
		INSERT POLOXI.Legal_DecisionLosingSideTest (DecisionLosingSideTestId, DecisionSessionId, WinnerCandidateId, ChallengerCandidateId, StrongestCaseSummary, ChallengerStrength, WinnerStrength, WinnerSurvived, TenantId, CreatedByUserId)
		VALUES (N'A4000000-0000-0000-0000-0000000000AA', @S4, @S4C1, @S4C2,
			N'Strongest case for denial: absent verified competitive work, Halcyon shows no irreparable harm and the covenant''s reach is doubtful. The winner leads only because the onboarding record suggests competitive assignment - a fact that is unverified and dispositive.',
			0.5880, 0.6120, 1, @Tenant, @User);
END

COMMIT TRANSACTION;
