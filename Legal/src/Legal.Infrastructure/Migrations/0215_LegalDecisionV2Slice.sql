SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal V2 vertical-slice seed (idempotent). Seeds a complete typed dependency graph for the
-- existing Summary Judgment demo session (S1 = A1000000-...-000000000010, Acme v. Globex) so the
-- cockpit's V2 panels (typed graph, independent-verifier status, strongest-losing-side gate,
-- dependency-constrained readiness) render real DB-backed state on a fresh environment.
--
-- The slice deliberately leaves one MATERIAL authority edge UNVERIFIED and marks the essential
-- "use after termination" fact chain as unsatisfied, so readiness is correctly NOT READY — which
-- matches the seeded V1 answer (summary judgment likely DENIED; the frontier fact is unresolved).
-- Keyed on deterministic GUIDs so re-running never duplicates rows. Runs only if S1 exists.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @S1     UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000010';
DECLARE @S1C1   UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000021';  -- winner (SJ denied)
DECLARE @S1C2   UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000022';  -- challenger (SJ granted)

IF EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S1)
BEGIN
	-- Node ids (deterministic).
	DECLARE @Fact1 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-0000000000F1';  -- disputed use-after-termination
	DECLARE @Prop1 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-0000000000A1';  -- non-use clause survives termination
	DECLARE @Elem1 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-0000000000E1';  -- element: post-termination use
	DECLARE @Burden1 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-0000000000B1';
	DECLARE @Proc1 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-0000000000C1';  -- no genuine dispute of material fact
	DECLARE @Strat1 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-0000000000E5';

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFactProposition WHERE DecisionFactPropositionId = @Fact1)
		INSERT POLOXI.Legal_DecisionFactProposition (DecisionFactPropositionId, DecisionSessionId, NodeCode, Statement, Support, IsMaterial, IsDisputed, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Fact1, @S1, N'F1', N'Globex used the confidential specifications after the NDA terminated.', 0.4500, 1, 1, N'UNVERIFIED', 0, @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionLegalProposition WHERE DecisionLegalPropositionId = @Prop1)
		INSERT POLOXI.Legal_DecisionLegalProposition (DecisionLegalPropositionId, DecisionSessionId, NodeCode, Statement, AuthorityRef, Support, IsMaterial, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Prop1, @S1, N'P1', N'The NDA''s non-use covenant survives termination and bars post-termination use.', N'NDA §7.2; Restatement (Second) of Contracts §90', 0.8000, 1, N'VERIFIED', 0, @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionLegalElement WHERE DecisionLegalElementId = @Elem1)
		INSERT POLOXI.Legal_DecisionLegalElement (DecisionLegalElementId, DecisionSessionId, NodeCode, DisplayName, Statement, IsEssential, IsSatisfied, Support, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Elem1, @S1, N'E1', N'Post-termination use of confidential information', N'Movant must show no genuine dispute that use did/did not occur after termination.', 1, 0, 0.4500, N'UNVERIFIED', 0, @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBurdenRule WHERE DecisionBurdenRuleId = @Burden1)
		INSERT POLOXI.Legal_DecisionBurdenRule (DecisionBurdenRuleId, DecisionSessionId, NodeCode, BurdenedParty, StandardOfProof, Statement, IsSatisfied, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Burden1, @S1, N'B1', N'Defendant (movant)', N'No genuine dispute of material fact', N'On summary judgment the movant bears the burden of showing the absence of a triable issue.', 0, N'VERIFIED', 0, @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionProceduralConstraint WHERE DecisionProceduralConstraintId = @Proc1)
		INSERT POLOXI.Legal_DecisionProceduralConstraint (DecisionProceduralConstraintId, DecisionSessionId, NodeCode, DisplayName, Statement, IsSatisfied, IsDispositive, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Proc1, @S1, N'C1', N'Rule 56 — no genuine dispute of material fact', N'Summary judgment requires no genuine dispute as to any material fact.', 0, 1, N'VERIFIED', 0, @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionReasoningStrategy WHERE DecisionReasoningStrategyId = @Strat1)
		INSERT POLOXI.Legal_DecisionReasoningStrategy (DecisionReasoningStrategyId, DecisionSessionId, DecisionCandidateId, NodeCode, DisplayName, Rationale, Support, IsViable, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
		VALUES (@Strat1, @S1, @S1C1, N'S1', N'Deny SJ — triable issue on post-termination use', N'A genuine dispute over whether Globex used the specs after termination defeats summary judgment.', 0.6200, 1, N'VERIFIED', 0, @Tenant, @User);

	-- Typed edges (traceable chain). Edge ids deterministic.
	-- E1: Prop1 REQUIRES Elem1 (essential); E2: Fact1 SATISFIES Elem1 (essential, UNVERIFIED — the frontier);
	-- E3: Elem1 DEPENDS_ON Strat1; E4: Strat1 ESTABLISHES C1(winner); E5: Prop1 SUPPORTS Strat1 (material, VERIFIED).
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A1000000-0000-0000-0000-0000000000D1')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, TenantId, CreatedByUserId)
		VALUES (N'A1000000-0000-0000-0000-0000000000D1', @S1, N'REQUIRES', N'PROPOSITION', @Prop1, N'ELEMENT', @Elem1, 0.8000, 0.9000, 1, 0, N'VERIFIED', @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A1000000-0000-0000-0000-0000000000D2')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, TenantId, CreatedByUserId)
		VALUES (N'A1000000-0000-0000-0000-0000000000D2', @S1, N'SATISFIES', N'FACT', @Fact1, N'ELEMENT', @Elem1, 0.4500, 0.9500, 1, 0, N'UNVERIFIED', @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A1000000-0000-0000-0000-0000000000D3')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, TenantId, CreatedByUserId)
		VALUES (N'A1000000-0000-0000-0000-0000000000D3', @S1, N'DEPENDS_ON', N'ELEMENT', @Elem1, N'STRATEGY', @Strat1, 0.6200, 0.8000, 1, 0, N'VERIFIED', @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A1000000-0000-0000-0000-0000000000D4')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, TenantId, CreatedByUserId)
		VALUES (N'A1000000-0000-0000-0000-0000000000D4', @S1, N'ESTABLISHES', N'STRATEGY', @Strat1, N'CANDIDATE', @S1C1, 0.6200, 0.8500, 1, 1, N'VERIFIED', @Tenant, @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionGraphEdgeId = N'A1000000-0000-0000-0000-0000000000D5')
		INSERT POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId, SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, TenantId, CreatedByUserId)
		VALUES (N'A1000000-0000-0000-0000-0000000000D5', @S1, N'SUPPORTS', N'PROPOSITION', @Prop1, N'STRATEGY', @Strat1, 0.8000, 0.7000, 0, 0, N'VERIFIED', @Tenant, @User);

	-- Strongest-losing-side test result: winner (deny) survives the strongest opposing case (grant) by a small margin.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionLosingSideTest WHERE DecisionLosingSideTestId = N'A1000000-0000-0000-0000-0000000000AA')
		INSERT POLOXI.Legal_DecisionLosingSideTest (DecisionLosingSideTestId, DecisionSessionId, WinnerCandidateId, ChallengerCandidateId, StrongestCaseSummary, ChallengerStrength, WinnerStrength, WinnerSurvived, TenantId, CreatedByUserId)
		VALUES (N'A1000000-0000-0000-0000-0000000000AA', @S1, @S1C1, @S1C2,
			N'Strongest case for granting SJ: absent admissible evidence of post-termination use, the movant shows no triable issue. Winner leads because the source-control logs create a genuine dispute.',
			0.5400, 0.6200, 1, @Tenant, @User);

	-- Session V2 flags: this session used the dependency graph and is NOT ready (essential fact chain unverified).
	UPDATE POLOXI.Legal_DecisionSession
	   SET UseDependencyGraph = 1,
		   ReadinessSatisfied = 0,
			   ReadinessBlockersJson = N'["Essential dependencies are unsatisfied.","No material authority has been established yet."]',
		   ModifiedDateUtc = SYSUTCDATETIME(),
		   ModifiedByUserId = @User
	 WHERE DecisionSessionId = @S1 AND TenantId = @Tenant;
END

COMMIT TRANSACTION;
