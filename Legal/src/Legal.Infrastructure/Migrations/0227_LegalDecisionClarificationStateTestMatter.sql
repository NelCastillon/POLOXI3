SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — USER_CLARIFICATION_REQUIRED TERMINAL-STATE TEST MATTER (M7 / S7).
--
-- Purpose: cover the third terminal state (alongside M5 PROVISIONAL_DECISION and M6 DECISION_READY).
-- This matter reproduces the AMBIGUOUS-TIE clarification branch (§7):
--
--     if (!hasClarification && status != DECISION_READY && entropy >= 0.85 && margin < 0.05)
--         => USER_CLARIFICATION_REQUIRED, pivoting on the highest-flip open frontier branch.
--
-- It is deliberately constructed so the engine must ASK THE USER rather than decide:
--
--     • Dead-heat candidates (composite 0.512 vs 0.508 ⇒ margin 0.004)  → near-maximum entropy (~0.999)
--     • The tie turns on a USER-OWNED fact the engine cannot resolve by research: which of two
--       written agreements the parties actually intended to govern (a subjective-intent question).
--     • The highest-flip open frontier branch (FlipPotential 0.88) becomes the clarification pivot.
--
--   Because the deciding fact is a user/party-intent question (not a retrievable authority), the
--   correct terminal state is USER_CLARIFICATION_REQUIRED — the engine records a targeted question
--   and target rather than composing a final answer. A later session carrying the clarification
--   answer would continue without re-asking.
--
-- Idempotent: keyed on deterministic GUIDs (A7000000-*) so re-running never duplicates rows.
-- Tenant-scoped to the development demo tenant. Runs only after the base decision tables exist.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- Deterministic ids ── Matter 7 (Aspen Textiles — which contract governs / conflicting agreements).
DECLARE @M7   UNIQUEIDENTIFIER = N'A7000000-0000-0000-0000-000000000001';
DECLARE @S7   UNIQUEIDENTIFIER = N'A7000000-0000-0000-0000-000000000010';
DECLARE @S7C1 UNIQUEIDENTIFIER = N'A7000000-0000-0000-0000-000000000021';  -- razor-thin leader (Master Agreement)
DECLARE @S7C2 UNIQUEIDENTIFIER = N'A7000000-0000-0000-0000-000000000022';  -- dead-heat rival (Purchase Order)
DECLARE @S7B1 UNIQUEIDENTIFIER = N'A7000000-0000-0000-0000-000000000031';  -- OPEN clarification pivot (user intent)
DECLARE @S7B2 UNIQUEIDENTIFIER = N'A7000000-0000-0000-0000-000000000032';  -- resolved secondary branch

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND OBJECT_ID(N'POLOXI.Legal_DecisionSession', N'U') IS NOT NULL
BEGIN
	-- ── Matter ──────────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M7 AND TenantId = @Tenant)
		INSERT POLOXI.Legal_DecisionMatter
			(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
			 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
			 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
			 TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(@M7,
			 N'Aspen Textiles v. Birchwood Supply - Which Contract Governs (Conflicting Agreements)',
			 N'Civil Litigation - Contract',
			 N'United States - Colorado State - District Court - Denver County - Governing law: Colorado',
			 N'Cross-motions for summary judgment - Movant: both parties - Target: which of two agreements governs - Requested disposition: enforcement of the movant''s preferred terms',
			 N'Type: Civil Litigation (Contract).' + NCHAR(10)
			  + N'Jurisdiction: United States, Colorado District Court, Denver County; governing law: Colorado.' + NCHAR(10)
			  + N'Posture: Cross-motions for summary judgment. Each party contends a different writing governs the disputed order: Aspen relies on the earlier Master Supply Agreement (with arbitration + net-60 terms); Birchwood relies on a later signed Purchase Order (with litigation + net-30 terms).' + NCHAR(10)
			  + N'Core question: which writing the parties intended to govern the disputed transaction. The two outcomes are in a dead heat on the current record, and the dispositive fact is the PARTIES'' SUBJECTIVE INTENT about which agreement controlled - a fact only the user/client can supply, not one the engine can resolve by legal research. The correct action is to ASK the user, not to decide.',
			 N'OPEN',
			 N'Contract',
			 N'United States - State',
			 N'Colorado',
			 N'District Court',
			 N'Denver County',
			 N'Colorado',
			 N'Aspen Textiles',
			 N'Birchwood Supply',
			 N'Which of two conflicting agreements governs',
			 N'Enforcement of the movant''s preferred terms',
			 @Tenant, DATEADD(DAY, -2, @Now), @User);

	-- ── Session (persisted decision run) ── USER_CLARIFICATION_REQUIRED (ambiguous tie) ──────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S7)
		INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
			TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
			DepthReached, LlmCallCount, DurationMs, FinalAnswer, ClarificationQuestion, ClarificationTarget, MatterId,
			NextBestActionText, NextBestActionImpactCode, NextBestActionRationale, UseDependencyGraph, ReadinessSatisfied,
			ReadinessBlockersJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S7,
			N'Which agreement governs the disputed order between Aspen Textiles and Birchwood Supply?',
			N'LEGAL', N'gpt-4.1-mini', 1, N'USER_CLARIFICATION_REQUIRED', N'USER_CLARIFICATION_REQUIRED',
			N'AMBIGUOUS_TIE_NEEDS_USER_INPUT', @S7C1,
			0.6800, 0.999000, 0.004000,
			2, 6, 5240,
			NULL,  -- no final answer is composed for a clarification terminal state
			N'To decide between the leading outcomes, can you clarify ''Which writing did the parties intend to govern the disputed order?'' The record is in a dead heat between the earlier Master Supply Agreement and the later signed Purchase Order; the deciding fact is the parties'' intent, which only you can confirm.',
			N'Which writing did the parties intend to govern the disputed order?',
			@M7,
			N'Confirm which agreement the parties intended to govern the disputed order (Master Supply Agreement vs. later Purchase Order).',
			N'VERY HIGH',
			N'This is a user/party-intent question the engine cannot resolve by legal research. It is the highest-flip open frontier item (FlipPotential 0.88) and the tie cannot break without it, so the correct terminal state is USER_CLARIFICATION_REQUIRED rather than a provisional or ready decision.',
			1, 0,
			N'["Winner leads by only 0.004 with near-maximum entropy (0.999).","Dispositive fact is party intent - not resolvable by research.","A high-flip open frontier branch (FP 0.88) remains and requires user input."]',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Candidates (dead heat ⇒ near-maximum entropy, only a hairline ranked leader) ─────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S7C1)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S7C1, @S7, N'C1', N'Master Supply Agreement governs', N'The earlier Master Supply Agreement (arbitration, net-60) controls the disputed order.',
			0.5200, 0.5100, 0.5000, 0.5300, 0.5000, 0.4900, 0.5000, 0.5100, 0.512000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S7C2)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S7C2, @S7, N'C2', N'Later Purchase Order governs', N'The later signed Purchase Order (litigation, net-30) supersedes and controls the disputed order.',
			0.5100, 0.5100, 0.5000, 0.5100, 0.5100, 0.4900, 0.4900, 0.5000, 0.508000, 2, 0, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Branches: B1 OPEN clarification pivot (highest flip) + B2 resolved ───────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S7B1)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S7B1, @S7, 1, N'C2.B1', N'Which writing did the parties intend to govern the disputed order?',
			N'The dispositive, still-open fact: the parties'' subjective intent about which agreement controlled. Only the user/client can confirm this - it is not resolvable by legal research - so it is the clarification pivot.',
			N'ACTIVE', 0.700000, 0.9200, 0.8800, 0.2000, 0.700000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S7B2)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S7B2, @S7, 1, N'C1.B1', N'Are both writings independently enforceable as to form?',
			N'Whether each writing satisfies the statute of frauds and signature/formation requirements - both do on the current record; resolved. This does not break the tie.',
			N'RESOLVED', 0.320000, 0.5000, 0.2200, 0.8500, 0.320000, 0, 2, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Evidence (both writings present; the deciding INTENT fact is unverifiable by research) ─────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A7000000-0000-0000-0000-000000000041')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A7000000-0000-0000-0000-000000000041', @S7, @S7B1, N'Master Supply Agreement (executed) + later signed Purchase Order',
			N'Two conflicting writings', N'Both writings are authenticated and facially enforceable; they conflict on forum (arbitration vs. litigation) and payment terms (net-60 vs. net-30). Neither expressly supersedes the other.',
			0.6000, 0.6000, 0.600000, N'VERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A7000000-0000-0000-0000-000000000042')
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A7000000-0000-0000-0000-000000000042', @S7, @S7B1, N'Parties'' intent as to controlling agreement (unavailable - requires client confirmation)',
			N'Party intent (open)', N'Which writing the parties intended to govern the disputed order is not established by any available document; it depends on the client''s confirmation and cannot be resolved by legal research - this is the clarification pivot.',
			0.5000, 0.6500, 0.200000, N'UNVERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Flip point (resolving party intent flips the razor-thin leader) ──────────────────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFlipPoint WHERE DecisionFlipPointId = N'A7000000-0000-0000-0000-000000000051')
		INSERT POLOXI.Legal_DecisionFlipPoint (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description,
			ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A7000000-0000-0000-0000-000000000051', @S7, @S7B1,
			N'If the parties confirm they intended the later Purchase Order to govern, the winner flips from "Master Supply Agreement" to "Purchase Order".',
			0.004000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Dependency (essential, unverified user-intent fact ⇒ keeps the frontier open) ─────────────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependency WHERE DecisionDependencyId = N'A7000000-0000-0000-0000-000000000061')
		INSERT POLOXI.Legal_DecisionDependency (DecisionDependencyId, DecisionSessionId, DecisionCandidateId, NodeKind, Statement,
			Support, IsEssential, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (N'A7000000-0000-0000-0000-000000000061', @S7, @S7C1, N'FACT',
			N'The parties intended the Master Supply Agreement to govern the disputed order (essential to the C1 outcome; requires user confirmation).',
			0.5100, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Event-sourced timeline (terminates USER_CLARIFICATION_REQUIRED; NO answer composed) ───────
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @S7)
		INSERT POLOXI.Legal_DecisionEvent (DecisionEventId, DecisionSessionId, SequenceNumber, EventType, StageCode, PayloadJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(N'A7000000-0000-0000-0000-000000000071', @S7, 1, N'SESSION_STARTED',     N'DISCOVERY',   N'{"query":"which contract governs conflicting agreements"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A7000000-0000-0000-0000-000000000072', @S7, 2, N'CANDIDATES_PROPOSED', N'DISCOVERY',   N'{"count":2}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A7000000-0000-0000-0000-000000000073', @S7, 3, N'CANDIDATES_SCORED',   N'COMPETITION', N'{"margin":0.004,"entropy":0.999}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A7000000-0000-0000-0000-000000000074', @S7, 4, N'EVIDENCE_RETRIEVED',  N'RETRIEVAL',   N'{"evidenceCount":2,"decidingFact":"party-intent (unverifiable by research)"}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A7000000-0000-0000-0000-000000000075', @S7, 5, N'FRONTIER_UPDATED',    N'RANKING',     N'{"frontier":["B1"],"pivotFlipPotential":0.88}', @Tenant, DATEADD(DAY, -1, @Now), @User),
			(N'A7000000-0000-0000-0000-000000000076', @S7, 6, N'TERMINAL_STATE',      N'CONVERGENCE', N'{"statusCode":"USER_CLARIFICATION_REQUIRED","reason":"AMBIGUOUS_TIE_NEEDS_USER_INPUT"}', @Tenant, DATEADD(DAY, -1, @Now), @User);
END

COMMIT TRANSACTION;
