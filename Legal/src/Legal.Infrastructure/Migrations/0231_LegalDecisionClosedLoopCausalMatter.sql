SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal V2.1 — CLOSED-LOOP CAUSAL PROOF MATTER (M9 / S9).
--
-- Purpose: this is the ONE seeded matter that captures a complete, auditable before/after of the
-- V2.1 closed loop as a persisted record, so the cockpit can demonstrate the causal chain WITHOUT
-- re-running the LLM:
--
--     Verification change  →  Graph propagation  →  Candidate recompetition  →  Frontier change  →  NBA
--
-- Concretely, an ESSENTIAL, DISPOSITIVE authority edge (Prop "warranty disclaimer is conspicuous")
-- was INVALIDATED by the independent verifier. Deterministic Core propagation WEAKENED the strategy
-- that established the leader, the Candidate×Branch recompetition re-ranked the candidates, the
-- winner FLIPPED, entropy rose, the margin collapsed, the open frontier changed, and a new
-- outcome-directed ResearchNeed was generated from the new highest-IV frontier branch.
--
-- Every value here is chosen to be internally consistent with the recompetition record so the
-- Closed-loop panel, the recompetition KPIs, and the frontier snapshots all agree:
--
--     BEFORE:  winner = C1 (disclaimer bars claim), margin 0.150, entropy 0.902, NBA = B_ambig
--     EVENT:   edge E5 (PROPOSITION SUPPORTS STRATEGY, essential+dispositive)  VERIFIED → INVALIDATED
--     AFTER:   winner = C2 (buyer's implied-warranty claim survives), margin 0.040, entropy 0.981,
--              frontier top = B_uccexc, NBA = "UCC conspicuousness exception" research need
--
-- This matter is deliberately DECIDED-then-reopened so it exercises the readback path in
-- GetSessionResultAsync + HydrateClosedLoopAsync (LastRecompetition + PendingResearchNeed).
--
-- Idempotent: keyed on deterministic GUIDs (A9000000-*) so re-running never duplicates rows.
-- Tenant-scoped to the development demo tenant. Runs only after the base + closed-loop tables exist.
-- ───────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- Deterministic ids ── Matter 9 (Redwood Instruments — does the warranty disclaimer bar the claim?).
DECLARE @M9    UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-000000000001';
DECLARE @S9    UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-000000000010';
DECLARE @S9C1  UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-000000000021';  -- was leader (disclaimer bars claim) → now loser
DECLARE @S9C2  UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-000000000022';  -- was challenger (claim survives) → now winner

-- Branches
DECLARE @S9Bdisc  UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-000000000031';  -- conspicuousness of the disclaimer (was frontier top, now WEAKENED/resolved-against)
DECLARE @S9Bambig UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-000000000032';  -- ambiguity in the disclaimer language (pre-flip NBA)
DECLARE @S9Buccx  UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-000000000033';  -- UCC 2-316 conspicuousness exception (post-flip NBA / new frontier top)

-- Graph nodes
DECLARE @Fact9 UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000F1';  -- buyer never initialed the disclaimer box (disputed)
DECLARE @Prop9 UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000A1';  -- the disclaimer is conspicuous under UCC 2-316 (INVALIDATED)
DECLARE @Elem9 UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000E1';  -- element: conspicuous exclusion of implied warranties
DECLARE @Strat9 UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000E5';  -- strategy: bar the claim via a conspicuous disclaimer (WEAKENED)

-- Closed-loop records
DECLARE @Evt9   UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000B1';  -- dependency event (edge invalidation)
DECLARE @Recomp UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000C1';  -- recompetition record
DECLARE @SnapB  UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000D1';  -- frontier snapshot BEFORE
DECLARE @SnapA  UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000D2';  -- frontier snapshot AFTER
DECLARE @Need   UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000E9';  -- new research need (post-flip NBA)
DECLARE @Edge5  UNIQUEIDENTIFIER = N'A9000000-0000-0000-0000-0000000000D5';  -- the invalidated essential+dispositive edge

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND OBJECT_ID(N'POLOXI.Legal_DecisionSession', N'U') IS NOT NULL
   AND OBJECT_ID(N'POLOXI.Legal_DecisionRecompetition', N'U') IS NOT NULL
BEGIN
	-- ── Matter ───────────────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M9 AND TenantId = @Tenant)
		INSERT POLOXI.Legal_DecisionMatter
			(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
			 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
			 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
			 TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(@M9,
			 N'Redwood Instruments v. Basalt Components - Does the Warranty Disclaimer Bar the Implied-Warranty Claim',
			 N'Civil Litigation - Contract',
			 N'United States - Oregon - Circuit Court - Multnomah County - Governing law: Oregon (UCC Article 2)',
			 N'Motion to dismiss the implied-warranty claim - Movant: Basalt Components - Target: implied-warranty of merchantability claim - Disposition sought: dismiss (conspicuous disclaimer).',
			 N'Type: Civil Litigation (Contract / UCC Article 2).' + NCHAR(10)
			  + N'Jurisdiction: United States, Oregon Circuit Court, Multnomah County; governing law: Oregon UCC.' + NCHAR(10)
			  + N'Posture: Basalt moves to dismiss the buyer''s implied-warranty-of-merchantability claim, arguing the sales contract''s written disclaimer conspicuously excluded implied warranties under UCC 2-316.' + NCHAR(10)
			  + N'Closed-loop demonstration: the decision was initially provisional in favor of the seller (the disclaimer bars the claim) because a proposition - that the disclaimer is conspicuous - was accepted. The independent verifier then INVALIDATED that proposition''s supporting edge. Deterministic propagation weakened the seller''s strategy, the candidates recompeted, the winner FLIPPED to the buyer, the margin collapsed, the frontier moved to the UCC 2-316 conspicuousness exception, and a new research need was generated. This matter persists that full before/after so the causal chain is auditable without re-running the model.',
			 N'OPEN',
			 N'Contract',
			 N'United States - State',
			 N'Oregon',
			 N'Circuit Court',
			 N'Multnomah County',
			 N'Oregon (UCC Article 2)',
			 N'Basalt Components',
			 N'Redwood Instruments',
			 N'Implied-warranty of merchantability claim',
			 N'Dismiss the implied-warranty claim',
			 @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Session (post-flip AFTER state: winner = C2, provisional, research remains) ────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S9)
		INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
			TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
			DepthReached, LlmCallCount, DurationMs, FinalAnswer, MatterId, NextBestActionText, NextBestActionImpactCode,
			NextBestActionRationale, UseDependencyGraph, ReadinessSatisfied, ReadinessBlockersJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S9,
			N'Does the contract''s written warranty disclaimer bar Redwood''s implied-warranty-of-merchantability claim?',
			N'LEGAL', N'gpt-4.1-mini', 1, N'PROVISIONAL_DECISION', N'PROVISIONAL_DECISION',
			N'Winner flipped after an essential authority edge was invalidated; a decisive UCC 2-316 exception remains on the frontier',
			@S9C2,
			1.0000, 0.981000, 0.040000,
			1, 9, 10120,
			N'This is a PROVISIONAL decision, and it changed. Initially the seller''s position led (the disclaimer bars the claim) because the disclaimer was treated as conspicuous under UCC 2-316. That supporting proposition was then invalidated on verification, which weakened the seller''s strategy. After recompetition the BUYER''s implied-warranty claim now narrowly leads (0.590 to 0.550): absent a conspicuous disclaimer, UCC 2-316''s exclusion requirements are not satisfied on the current record. The margin is thin (0.040) and entropy is high (0.981), so the decision is not ready. The single highest-value open question is now whether a UCC 2-316(3) conspicuousness exception (course of dealing / trade usage / examination) nonetheless validates the exclusion.',
			@M9,
			N'Resolve the UCC 2-316(3) conspicuousness exception: determine whether course of dealing, trade usage, or the buyer''s pre-contract examination cured the non-conspicuous disclaimer.',
			N'HIGH',
			N'After the disclaimer-conspicuousness proposition was invalidated, the exclusion''s validity now hinges on a UCC 2-316(3) exception. That branch has the highest information value on the new frontier and can flip the controlling outcome again, so it is the single highest-value next action. Until it resolves, the decision stays PROVISIONAL.',
			1, 0, N'["4 high-impact frontier item(s) still open.","No material authority has been established yet."]',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Candidates (persisted AFTER state: C2 now leads C1) ────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S9C1)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S9C1, @S9, N'C1', N'Disclaimer bars the claim (dismiss)',
			N'A conspicuous written disclaimer excludes implied warranties under UCC 2-316, so the implied-warranty claim fails.',
			0.5200, 0.5400, 0.5000, 0.5500, 0.3600, 0.4600, 0.5400, 0.5200, 0.550000, 2, 0, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S9C2)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S9C2, @S9, N'C2', N'Implied-warranty claim survives (deny dismissal)',
			N'Absent a conspicuous disclaimer, UCC 2-316''s exclusion requirements are not met, so the implied-warranty claim survives dismissal.',
			0.6000, 0.5800, 0.5600, 0.5400, 0.5800, 0.4200, 0.5800, 0.5900, 0.590000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Branches (AFTER state: disclaimer branch weakened/resolved-against; UCC exception now frontier top) ──
	-- B_disc: conspicuousness of the disclaimer. Was the pre-flip frontier top; now resolved AGAINST the seller
	-- (edge invalidated), so it is no longer on the frontier.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S9Bdisc)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, ParentDecisionBranchId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S9Bdisc, @S9, NULL, 1, N'C1.B1', N'Is the written disclaimer conspicuous under UCC 2-316(2)?',
			N'Resolved against the seller on verification: the disclaimer was NOT shown to be conspicuous, so it no longer supports the exclusion. Off the frontier now that the supporting proposition was invalidated.',
			N'RESOLVED', 0.320000, 0.5000, 0.2000, 0.6000, 0.064000, 0, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- B_ambig: ambiguity in the disclaimer language. This was the PRE-FLIP NBA (highest IV before the event).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S9Bambig)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, ParentDecisionBranchId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S9Bambig, @S9, NULL, 1, N'C2.B1', N'Is the disclaimer language ambiguous as to which warranties it excludes?',
			N'Still active. Ambiguity is construed against the drafter (the seller). Was the highest-IV branch before the invalidation; after the flip it remains relevant but is no longer the single highest-IV branch.',
			N'ACTIVE', 0.560000, 0.7000, 0.5200, 0.5800, 0.324800, 1, 2, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- B_uccx: UCC 2-316(3) conspicuousness exception. This is the POST-FLIP NBA / new highest-IV frontier top.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S9Buccx)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, ParentDecisionBranchId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S9Buccx, @S9, NULL, 1, N'C1.B2', N'Does a UCC 2-316(3) exception validate the exclusion despite non-conspicuousness?',
			N'The decisive open question after the flip: course of dealing, trade usage, or the buyer''s pre-contract examination under UCC 2-316(3) could still exclude implied warranties even without a conspicuous 2-316(2) disclaimer. Highest IV on the new frontier - resolving it can flip the controlling outcome again.',
			N'ACTIVE', 0.680000, 0.8200, 0.7000, 0.5000, 0.394400, 1, 3, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Typed graph nodes ──────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFactProposition WHERE DecisionFactPropositionId = @Fact9)
		INSERT POLOXI.Legal_DecisionFactProposition (DecisionFactPropositionId, DecisionSessionId, NodeCode, Statement, Support, IsMaterial, IsDisputed, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Fact9, @S9, N'F1', N'The disclaimer appeared in the same small font as the surrounding boilerplate and the buyer did not separately initial it.', 0.6000, 1, 1, N'VERIFIED', 0, @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionLegalProposition WHERE DecisionLegalPropositionId = @Prop9)
		INSERT POLOXI.Legal_DecisionLegalProposition (DecisionLegalPropositionId, DecisionSessionId, NodeCode, Statement, AuthorityRef, Support, IsMaterial, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Prop9, @S9, N'P1', N'The warranty disclaimer is conspicuous under UCC 2-316(2).', N'UCC 2-316(2); ORS 72.3160', 0.3000, 1, N'INVALIDATED', 0, @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionLegalElement WHERE DecisionLegalElementId = @Elem9)
		INSERT POLOXI.Legal_DecisionLegalElement (DecisionLegalElementId, DecisionSessionId, NodeCode, DisplayName, Statement, IsEssential, IsSatisfied, Support, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Elem9, @S9, N'E1', N'Conspicuous exclusion of implied warranties', N'To bar the claim the seller must show a conspicuous exclusion of implied warranties under UCC 2-316.', 1, 0, 0.3200, N'UNVERIFIED', 0, @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionReasoningStrategy WHERE DecisionReasoningStrategyId = @Strat9)
		INSERT POLOXI.Legal_DecisionReasoningStrategy (DecisionReasoningStrategyId, DecisionSessionId, DecisionCandidateId, NodeCode, DisplayName, Rationale, Support, IsViable, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Strat9, @S9, @S9C1, N'S1', N'Bar the claim via a conspicuous disclaimer', N'If the disclaimer is conspicuous, UCC 2-316 excludes implied warranties and the claim fails. Weakened after the supporting proposition was invalidated.', 0.3600, 1, N'VERIFIED', 0, @Tenant, @User);

	-- ── Typed edges. E5 is the ESSENTIAL + DISPOSITIVE edge that was INVALIDATED (the causal trigger). ─
	-- E1: Prop1 REQUIRES Elem1 (essential, verified relationship).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A9000000-0000-0000-0000-0000000000E1')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, TenantId, CreatedByUserId)
		VALUES (N'A9000000-0000-0000-0000-0000000000E1', @S9, N'REQUIRES', N'PROPOSITION', @Prop9, N'ELEMENT', @Elem9, 0.8000, 0.9000, 1, 0, N'VERIFIED', @Tenant, @User);

	-- E2: Fact1 CONTRADICTS Proposition (the fact undercuts conspicuousness) — INVALIDATED_SOURCE downstream marker.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A9000000-0000-0000-0000-0000000000E2')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, PropagatedStateCode, TenantId, CreatedByUserId)
		VALUES (N'A9000000-0000-0000-0000-0000000000E2', @S9, N'CONTRADICTS', N'FACT', @Fact9, N'PROPOSITION', @Prop9, 0.6000, 0.8500, 0, 0, N'VERIFIED', N'INVALIDATED_SOURCE', @Tenant, @User);

	-- E3: Element DEPENDS_ON Strategy (verified relationship; target support recomputed downstream).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A9000000-0000-0000-0000-0000000000E3')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, PropagatedStateCode, TenantId, CreatedByUserId)
		VALUES (N'A9000000-0000-0000-0000-0000000000E3', @S9, N'DEPENDS_ON', N'ELEMENT', @Elem9, N'STRATEGY', @Strat9, 0.3600, 0.8000, 1, 0, N'VERIFIED', N'WEAKENED', @Tenant, @User);

	-- E4: Strategy ESTABLISHES C1 (was the winning path; now weakened so C1 lost the lead).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A9000000-0000-0000-0000-0000000000E4')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, PropagatedStateCode, TenantId, CreatedByUserId)
		VALUES (N'A9000000-0000-0000-0000-0000000000E4', @S9, N'ESTABLISHES', N'STRATEGY', @Strat9, N'CANDIDATE', @S9C1, 0.3600, 0.8500, 1, 1, N'VERIFIED', N'WEAKENED', @Tenant, @User);

	-- E5: Proposition SUPPORTS Strategy (essential + dispositive) — THE INVALIDATED EDGE (causal trigger).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = @Edge5)
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, VerificationNotes, PropagatedStateCode, TenantId, CreatedByUserId)
		VALUES (@Edge5, @S9, N'SUPPORTS', N'PROPOSITION', @Prop9, N'STRATEGY', @Strat9, 0.3000, 0.9500, 1, 1, N'INVALIDATED', N'Independent verifier: the disclaimer was not conspicuous (same font as boilerplate; not separately signed), so it does not support the exclusion strategy.', N'INVALIDATED_SOURCE', @Tenant, @User);

	-- ── Evidence (attached to the new frontier top — the UCC 2-316(3) exception) ────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A9000000-0000-0000-0000-000000000041')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A9000000-0000-0000-0000-000000000041', @S9, @S9Buccx, N'UCC 2-316(3)(c) / ORS 72.3160(3) - course of dealing & trade usage',
			N'UCC 2-316(3)', N'An implied warranty can also be excluded or modified by course of dealing, course of performance, or usage of trade - independent of the 2-316(2) conspicuousness requirement.',
			0.7000, 0.7500, 0.450000, N'UNVERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A9000000-0000-0000-0000-000000000042')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A9000000-0000-0000-0000-000000000042', @S9, @S9Bdisc, N'Sales order form (exhibit A) - disclaimer typography',
			N'Exhibit A', N'The disclaimer appears in the same 7-point font as the surrounding terms and is not set off, capitalized, or separately signed - the basis for finding it non-conspicuous.',
			0.8000, 0.8200, 0.820000, N'VERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Flip point (on the new frontier top ⇒ winner-changing) ─────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFlipPoint WHERE DecisionFlipPointId = N'A9000000-0000-0000-0000-000000000051')
		INSERT POLOXI.Legal_DecisionFlipPoint (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description,
			ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A9000000-0000-0000-0000-000000000051', @S9, @S9Buccx,
			N'If a UCC 2-316(3) exception (course of dealing, trade usage, or examination) is established, the exclusion is valid despite non-conspicuousness and the controlling outcome flips back from the buyer (C2) to the seller (C1).',
			0.040000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Dependency (essential, open ⇒ keeps the decision provisional) ───────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependency WHERE DecisionDependencyId = N'A9000000-0000-0000-0000-000000000061')
		INSERT POLOXI.Legal_DecisionDependency (DecisionDependencyId, DecisionSessionId, DecisionCandidateId, NodeKind, Statement,
			Support, IsEssential, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A9000000-0000-0000-0000-000000000061', @S9, @S9C1, N'ELEMENT',
			N'The seller''s exclusion depends on either a conspicuous 2-316(2) disclaimer (invalidated) or a 2-316(3) exception (open, not yet verified).',
			0.3200, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ═══ CLOSED-LOOP AUDIT RECORDS (the point of this matter) ═══════════════════════════════════════

	-- 1) DependencyEvent: the authoritative verification change that started the loop (E5 VERIFIED → INVALIDATED).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependencyEvent WHERE DecisionDependencyEventId = @Evt9)
		INSERT POLOXI.Legal_DecisionDependencyEvent (DecisionDependencyEventId, DecisionSessionId, MatterId, DecisionGraphEdgeId, IdempotencyKey,
			PreviousStatus, NewStatus, ImpactJson, AffectedBranchCount, AffectedCandidateCount, RecompetitionTriggered,
			TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@Evt9, @S9, @M9, @Edge5, LOWER(CONVERT(NVARCHAR(36), @Edge5)) + N':INVALIDATED',
			N'VERIFIED', N'INVALIDATED',
			N'{"propagation":[{"node":"P1","state":"INVALIDATED_SOURCE"},{"edge":"E3","state":"WEAKENED"},{"edge":"E4","state":"WEAKENED"},{"strategy":"S1","state":"WEAKENED"},{"candidate":"C1","effect":"lost-lead"}]}',
			2, 2, 1,
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- 2) Recompetition: the before/after re-ranking the loop produced. This is what the cockpit's
	--    "Closed-loop result (V2.1) — Last verification impact" panel reads back.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionRecompetition WHERE DecisionRecompetitionId = @Recomp)
		INSERT POLOXI.Legal_DecisionRecompetition (DecisionRecompetitionId, DecisionSessionId, DecisionDependencyEventId,
			PreviousWinnerCandidateId, CurrentWinnerCandidateId, WinnerChanged,
			PreviousEntropy, CurrentEntropy, PreviousMargin, CurrentMargin, ReopenedBranchCount,
			PreviousRankingJson, CurrentRankingJson, ReasonCode, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@Recomp, @S9, @Evt9,
			@S9C1, @S9C2, 1,
			0.902000, 0.981000, 0.150000, 0.040000, 1,
			N'[{"candidateCode":"C1","displayName":"Disclaimer bars the claim (dismiss)","compositeScore":0.700,"rankOrder":1},{"candidateCode":"C2","displayName":"Implied-warranty claim survives (deny dismissal)","compositeScore":0.550,"rankOrder":2}]',
			N'[{"candidateCode":"C2","displayName":"Implied-warranty claim survives (deny dismissal)","compositeScore":0.590,"rankOrder":1},{"candidateCode":"C1","displayName":"Disclaimer bars the claim (dismiss)","compositeScore":0.550,"rankOrder":2}]',
			N'ESSENTIAL_EDGE_INVALIDATED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- 3) Frontier snapshots: BEFORE (top = ambiguity branch) and AFTER (top = UCC 2-316(3) exception).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFrontierSnapshot WHERE DecisionFrontierSnapshotId = @SnapB)
		INSERT POLOXI.Legal_DecisionFrontierSnapshot (DecisionFrontierSnapshotId, DecisionSessionId, DecisionRecompetitionId,
			Entropy, Margin, OpenFrontierCount, TopBranchId, TopBranchInformationValue, FrontierJson,
			TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@SnapB, @S9, @Recomp,
			0.902000, 0.150000, 3, @S9Bambig, 0.610000,
			N'{"phase":"BEFORE","topBranchCode":"C2.B1","topBranchName":"Ambiguity in the disclaimer language","branches":["C1.B1","C2.B1","C1.B2"]}',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFrontierSnapshot WHERE DecisionFrontierSnapshotId = @SnapA)
		INSERT POLOXI.Legal_DecisionFrontierSnapshot (DecisionFrontierSnapshotId, DecisionSessionId, DecisionRecompetitionId,
			Entropy, Margin, OpenFrontierCount, TopBranchId, TopBranchInformationValue, FrontierJson,
			TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@SnapA, @S9, @Recomp,
			0.981000, 0.040000, 2, @S9Buccx, 0.680000,
			N'{"phase":"AFTER","topBranchCode":"C1.B2","topBranchName":"UCC 2-316(3) conspicuousness exception","branches":["C2.B1","C1.B2"]}',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- 4) ResearchNeed: the new outcome-directed investigation produced from the new highest-IV frontier
	--    branch. This is what the cockpit's "Next investigation" line reads back.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionResearchNeed WHERE DecisionResearchNeedId = @Need)
		INSERT POLOXI.Legal_DecisionResearchNeed (DecisionResearchNeedId, DecisionSessionId, MatterId, DecisionBranchId, DecisionDependencyEventId,
			IssueLabel, PropositionToResolve, AuthorityKind, RequiredEvidenceKind, WhyDecisionRelevant,
			ExpectedDiscrimination, CurrentUncertainty, InformationValue, FalsificationCondition, StatusCode,
			TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@Need, @S9, @M9, @S9Buccx, @Evt9,
			N'UCC 2-316(3) conspicuousness exception',
			N'Determine whether course of dealing, trade usage, or the buyer''s pre-contract examination excluded implied warranties under UCC 2-316(3) despite the non-conspicuous 2-316(2) disclaimer.',
			N'STATUTE', N'TRANSACTION_HISTORY',
			N'After the conspicuousness proposition was invalidated, the exclusion''s validity now turns entirely on a 2-316(3) exception. It is the highest-IV branch on the new frontier and can flip the controlling outcome again.',
			0.7000, 0.6800, 0.680000,
			N'If the prior-orders file and trade-usage evidence show no consistent exclusion practice, the 2-316(3) exception fails and the buyer''s claim survives.',
			N'OPEN', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Event-sourced timeline: shows the full causal chain in order, ending PROVISIONAL (research remains) ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @S9)
		INSERT POLOXI.Legal_DecisionEvent (DecisionEventId, DecisionSessionId, SequenceNumber, EventType, StageCode, PayloadJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(N'A9000000-0000-0000-0000-000000000071', @S9, 1, N'SESSION_STARTED',       N'INTAKE',        N'{"query":"does the warranty disclaimer bar the implied-warranty claim"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A9000000-0000-0000-0000-000000000072', @S9, 2, N'CANDIDATES_SCORED',     N'COMPETITION',   N'{"winner":"C1","margin":0.150,"entropy":0.902}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A9000000-0000-0000-0000-000000000073', @S9, 3, N'EVIDENCE_RETRIEVED',    N'EVIDENCE',      N'{"branch":"C1.B1","verified":true}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A9000000-0000-0000-0000-000000000074', @S9, 4, N'VERIFICATION_CHANGED',  N'CLOSED_LOOP',   N'{"edge":"E5","previous":"VERIFIED","new":"INVALIDATED","essential":true,"dispositive":true}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A9000000-0000-0000-0000-000000000075', @S9, 5, N'PROPAGATION_APPLIED',   N'CLOSED_LOOP',   N'{"weakened":["S1","E3","E4"],"invalidatedSource":["P1"]}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A9000000-0000-0000-0000-000000000076', @S9, 6, N'RECOMPETITION',         N'CLOSED_LOOP',   N'{"winnerChanged":true,"previousWinner":"C1","currentWinner":"C2","previousMargin":0.150,"currentMargin":0.040,"previousEntropy":0.902,"currentEntropy":0.981,"reopenedBranchCount":1}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A9000000-0000-0000-0000-000000000077', @S9, 7, N'FRONTIER_UPDATED',      N'CLOSED_LOOP',   N'{"topBefore":"C2.B1","topAfter":"C1.B2","openBefore":3,"openAfter":2}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A9000000-0000-0000-0000-000000000078', @S9, 8, N'RESEARCH_NEED_CREATED', N'CLOSED_LOOP',   N'{"issue":"UCC 2-316(3) conspicuousness exception","informationValue":0.680}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A9000000-0000-0000-0000-000000000079', @S9, 9, N'TERMINAL_STATE',        N'PROVISIONAL_RESEARCH_REMAINS', N'{"statusCode":"PROVISIONAL_DECISION","margin":0.040,"entropy":0.981}', @Tenant, DATEADD(DAY, -1, @Now), @User);
END

COMMIT TRANSACTION;
