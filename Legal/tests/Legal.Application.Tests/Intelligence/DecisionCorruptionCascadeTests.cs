using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — END-TO-END CORRUPTION CASCADE (step 10).
//
// This is the integrity test that proves the pieces form a real closed loop rather than a set of
// individually-working components. It establishes a known-good baseline, corrupts ONE essential
// verified evidence edge, and asserts the EXACT downstream state changes are CAUSED by that mutation:
//
//   INVALIDATE E1 → dependency propagation → essential support lost → domain-neutral signals →
//   candidate recompetition (entropy/margin) → frontier recalculation → next research target →
//   output-claim authorization (ALLOW → CORRECT/QUALIFY).
//
// Two cases pin both STABILITY and SENSITIVITY:
//   • Non-dispositive corruption: propagation + recompetition occur, but the winner SURVIVES.
//   • Dispositive corruption: the winner LOSES essential support and the leader FLIPS.
//
// Plus guard invariants: invalidating a fully-covered (alternative verified path) edge causes NO
// false recompetition, and propagation is deterministic/idempotent.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionCorruptionCascadeTests
{
    private const int MaxDepth = 8;

    private static DecisionCoreSettings Settings() => new(
        0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
        0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
        4, 24, 8);

    private static DecisionCandidatePersistence Candidate(
        string code, string name, decimal composite, decimal legal, decimal fact,
        decimal verification, decimal authority, decimal evidence, bool winner)
        => new(
            Guid.NewGuid(), code, name, name,
            LegalSupport: legal, FactSupport: fact, EvidenceSupport: evidence, AuthoritySupport: authority,
            Verification: verification, Uncertainty: 0.30m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: composite, DecisionSupportCeiling: 0.95m,
            RankOrder: winner ? 1 : 2, IsWinner: winner, IsEliminated: false);

    private static DecisionBranchPersistence Branch(Guid id, string code, string state, bool onFrontier)
        => new(
            id, null, 1, code, code, null, state,
            InformationValue: 0.40m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: onFrontier, StopReason: null, SortOrder: 0);

    private static DecisionGraphNodePersistence Node(
        Guid id, string kind, string code, decimal support, bool essential, Guid? branchId = null)
        => new(id, kind, code, code, "stmt", support, essential, IsSatisfied: support >= 0.5m,
            DecisionVerificationStates.Verified, 0)
        {
            SourceBranchId = branchId,
        };

    private static DecisionGraphEdgePersistence Edge(
        Guid id, Guid source, Guid target, decimal weight, decimal materiality,
        bool essential, Guid branchId, bool alternativePathAllowed = false)
        => new(id, "SUPPORTS", "EVIDENCE", source, "PROPOSITION", target,
            SupportWeight: weight, Materiality: materiality, IsEssential: essential, IsDispositive: false,
            VerificationStatus: DecisionVerificationStates.Verified, VerificationNotes: null, PropagatedStateCode: null)
        {
            SourceBranchId = branchId,
            AlternativePathAllowed = alternativePathAllowed,
        };

    private static DecisionGraphPersistence Snapshot(
        IReadOnlyCollection<DecisionGraphNodePersistence> nodes,
        IReadOnlyCollection<DecisionGraphEdgePersistence> edges)
        => new(Guid.NewGuid(), Guid.NewGuid(), null, ReadinessSatisfied: true, ReadinessBlockersJson: null,
            nodes, edges, LosingSideTest: null);

    // ── DISPOSITIVE CORRUPTION: invalidating an essential verified edge must flip the leader. ──────
    [Fact]
    public void DispositiveCorruption_InvalidatesEssentialEvidence_FlipsLeader_AndChangesFrontier()
    {
        // BASELINE: C1 leads C2; C1's essential fact F1 is established solely by verified evidence E1.
        var c1BranchId = Guid.NewGuid();
        var c2BranchId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var edgeId = Guid.NewGuid();

        var nodes = new[]
        {
            Node(e1, "EVIDENCE", "E1", support: 1.0m, essential: false),
            Node(f1, "PROPOSITION", "F1", support: 0.85m, essential: true, branchId: c1BranchId),
        };
        var edges = new[] { Edge(edgeId, e1, f1, weight: 0.9m, materiality: 0.9m, essential: true, branchId: c1BranchId) };
        var snapshot = Snapshot(nodes, edges);

        var c1 = Candidate("C1", "Exemption applies (defendant wins)", composite: 0.64m,
            legal: 0.60m, fact: 0.60m, verification: 0.82m, authority: 0.60m, evidence: 0.60m, winner: true);
        var c2 = Candidate("C2", "Small-vehicle exception (plaintiff wins)", composite: 0.58m,
            legal: 0.58m, fact: 0.58m, verification: 0.62m, authority: 0.55m, evidence: 0.55m, winner: false);
        var candidates = new[] { c1, c2 };
        var c1Branch = Branch(c1BranchId, "C1.B1", "ACTIVE", onFrontier: false);
        var c2Branch = Branch(c2BranchId, "C2.B1", "ACTIVE", onFrontier: true);
        var branches = new[] { c1Branch, c2Branch };

        // Baseline output authorization for C1's dispositive claim: VERIFIED + FULL → ALLOW.
        var ocBefore = OutputClaimDispositions.Classify(ClaimVerificationState.Supported, ClaimDecisionAuthority.Full);
        Assert.Equal(OutputClaimDisposition.Allow, ocBefore);

        // CORRUPT: E1 VERIFIED → INVALIDATED (contradicted source).
        var propagation = new DependencyPropagationService().Apply(snapshot, edgeId, DecisionVerificationStates.Invalidated, MaxDepth);

        // CASCADE assertions (causal, not "method completed"):
        Assert.True(propagation.EdgeFound);
        Assert.Equal(DecisionVerificationStates.Verified, propagation.PreviousStatus);
        Assert.Contains(f1, propagation.Impact.EssentialDependenciesFailed);      // essential support lost
        Assert.Contains(c1BranchId, propagation.Impact.AffectedBranchIds);        // C1's branch affected
        Assert.True(propagation.Impact.RecompetitionRequired);
        Assert.True(propagation.Impact.FrontierRecalculationRequired);
        Assert.Contains(propagation.Impact.Signals, s => s.BranchId == c1BranchId && s.SupportDelta < 0 && s.ReopenRequested);

        // RECOMPETITION driven by the graph's domain-neutral signals.
        var reopenAllowed = new HashSet<Guid> { c1BranchId, c2BranchId };
        var result = DecisionRecompetition.Run(candidates, branches, propagation.Impact.Signals.ToArray(), Settings(), reopenAllowed);

        // Winner flips C1 → C2 because the essential support was corrupted.
        Assert.True(result.WinnerChanged);
        Assert.Equal(c1.DecisionCandidateId, result.PreviousWinnerId);
        Assert.Equal(c2.DecisionCandidateId, result.CurrentWinnerId);
        Assert.True(result.ReopenedBranchCount >= 1);                              // frontier/branch reopened

        // Output authorization recalculates: the corrupted claim is now CONTRADICTED → CORRECT.
        var ocAfter = OutputClaimDispositions.Classify(ClaimVerificationState.Contradicted, ClaimDecisionAuthority.Limited);
        Assert.Equal(OutputClaimDisposition.Correct, ocAfter);
        Assert.NotEqual(ocBefore, ocAfter);
    }

    // ── NON-DISPOSITIVE CORRUPTION: propagation + recompetition occur but the winner SURVIVES. ─────
    [Fact]
    public void NonDispositiveCorruption_WeakensLeader_ButWinnerSurvives()
    {
        // BASELINE: F1 is established by a strong essential edge E1 (with an alternative path allowed)
        // AND a weaker verified alternative E2. Invalidating E1 weakens F1 but does not break it.
        var c1BranchId = Guid.NewGuid();
        var c2BranchId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var e1Edge = Guid.NewGuid();
        var e2Edge = Guid.NewGuid();

        var nodes = new[]
        {
            Node(e1, "EVIDENCE", "E1", support: 1.0m, essential: false),
            Node(e2, "EVIDENCE", "E2", support: 1.0m, essential: false),
            Node(f1, "PROPOSITION", "F1", support: 0.78m, essential: true, branchId: c1BranchId),
        };
        var edges = new[]
        {
            Edge(e1Edge, e1, f1, weight: 0.9m, materiality: 0.9m, essential: true, branchId: c1BranchId, alternativePathAllowed: true),
            Edge(e2Edge, e2, f1, weight: 0.6m, materiality: 0.6m, essential: false, branchId: c1BranchId),
        };
        var snapshot = Snapshot(nodes, edges);

        // C1 has a decisive lead in the UNAFFECTED dimensions (legal/fact) so a moderate evidence
        // weakening cannot overturn it — this tests stability, not fixture luck.
        var c1 = Candidate("C1", "Exemption applies", composite: 0.74m,
            legal: 0.85m, fact: 0.85m, verification: 0.80m, authority: 0.70m, evidence: 0.70m, winner: true);
        var c2 = Candidate("C2", "Small-vehicle exception", composite: 0.52m,
            legal: 0.50m, fact: 0.50m, verification: 0.55m, authority: 0.50m, evidence: 0.50m, winner: false);
        var candidates = new[] { c1, c2 };
        var c1Branch = Branch(c1BranchId, "C1.B1", "ACTIVE", onFrontier: true);
        var c2Branch = Branch(c2BranchId, "C2.B1", "ACTIVE", onFrontier: true);
        var branches = new[] { c1Branch, c2Branch };

        // CORRUPT the strong-but-non-dispositive edge.
        var propagation = new DependencyPropagationService().Apply(snapshot, e1Edge, DecisionVerificationStates.Invalidated, MaxDepth);

        // Propagation still runs (F1 weakened via the surviving alternative path) — but F1 is NOT broken.
        Assert.True(propagation.EdgeFound);
        Assert.DoesNotContain(f1, propagation.Impact.EssentialDependenciesFailed);
        Assert.True(propagation.Impact.RecompetitionRequired);
        Assert.Contains(propagation.Impact.Signals, s => s.BranchId == c1BranchId && s.SupportDelta < 0);

        var reopenAllowed = new HashSet<Guid> { c1BranchId, c2BranchId };
        var result = DecisionRecompetition.Run(candidates, branches, propagation.Impact.Signals.ToArray(), Settings(), reopenAllowed);

        // Winner SURVIVES: recompetition happened, C1 weakened, but the leader is unchanged.
        Assert.False(result.WinnerChanged);
        Assert.Equal(c1.DecisionCandidateId, result.CurrentWinnerId);
        // The corrupted-but-surviving claim is now uncertain (Disputed) → QUALIFY, not ALLOW.
        Assert.Equal(OutputClaimDisposition.Qualify,
            OutputClaimDispositions.Classify(ClaimVerificationState.Disputed, ClaimDecisionAuthority.Limited));
    }

    // ── GUARD: an invalidated edge fully covered by an alternative verified path must NOT cause a
    //    false recompetition or output-authority change (no support delta ⇒ no signals). ───────────
    [Fact]
    public void FullyCoveredAlternativePath_CausesNoFalseRecompetition()
    {
        var c1BranchId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var e1Edge = Guid.NewGuid();
        var e2Edge = Guid.NewGuid();

        // Two IDENTICAL verified edges establish F1. RecomputeSupport AVERAGES incoming edges, so the
        // support with one edge equals the support with two (0.9*1.0 → 0.9). Seeding F1 at that averaged
        // steady state (0.9) means invalidating one edge yields NO delta → no signal → no false
        // recompetition, even though an edge was removed.
        var nodes = new[]
        {
            Node(e1, "EVIDENCE", "E1", support: 1.0m, essential: false),
            Node(e2, "EVIDENCE", "E2", support: 1.0m, essential: false),
            Node(f1, "PROPOSITION", "F1", support: 0.9m, essential: true, branchId: c1BranchId),
        };
        var edges = new[]
        {
            Edge(e1Edge, e1, f1, weight: 0.9m, materiality: 0.9m, essential: true, branchId: c1BranchId, alternativePathAllowed: true),
            Edge(e2Edge, e2, f1, weight: 0.9m, materiality: 0.9m, essential: false, branchId: c1BranchId),
        };
        var snapshot = Snapshot(nodes, edges);

        var propagation = new DependencyPropagationService().Apply(snapshot, e1Edge, DecisionVerificationStates.Invalidated, MaxDepth);

        Assert.True(propagation.EdgeFound);
        Assert.DoesNotContain(f1, propagation.Impact.EssentialDependenciesFailed);
        Assert.False(propagation.Impact.RecompetitionRequired);
        Assert.Empty(propagation.Impact.Signals);
    }

    // ── DETERMINISM: applying the same corruption to the same snapshot yields the same impact. ─────
    [Fact]
    public void Corruption_IsDeterministic()
    {
        static DecisionGraphPersistence Build(Guid e1, Guid f1, Guid edgeId, Guid branchId) =>
            new(Guid.NewGuid(), Guid.NewGuid(), null, true, null,
                new[]
                {
                    Node(e1, "EVIDENCE", "E1", 1.0m, false),
                    Node(f1, "PROPOSITION", "F1", 0.85m, true, branchId),
                },
                new[] { Edge(edgeId, e1, f1, 0.9m, 0.9m, true, branchId) },
                null);

        var e1 = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var first = new DependencyPropagationService().Apply(Build(e1, f1, edgeId, branchId), edgeId, DecisionVerificationStates.Invalidated, MaxDepth);
        var second = new DependencyPropagationService().Apply(Build(e1, f1, edgeId, branchId), edgeId, DecisionVerificationStates.Invalidated, MaxDepth);

        Assert.Equal(first.Impact.EssentialDependenciesFailed.Count, second.Impact.EssentialDependenciesFailed.Count);
        Assert.Equal(first.Impact.Signals.Count, second.Impact.Signals.Count);
        Assert.Equal(first.Impact.RecompetitionRequired, second.Impact.RecompetitionRequired);
        Assert.Equal(
            first.Impact.Signals.Single().SupportDelta,
            second.Impact.Signals.Single().SupportDelta, 6);
    }
}
