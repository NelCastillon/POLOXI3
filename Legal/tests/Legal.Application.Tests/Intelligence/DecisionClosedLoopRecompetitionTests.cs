using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI Legal V2.1 closed-loop acceptance: a verification change to an essential dependency must
// flow through deterministic recompetition and change the ranking (Acme v. Globex-style flip),
// while remaining idempotent and authoritative (the graph never scores; only supplies deltas).
public sealed class DecisionClosedLoopRecompetitionTests
{
    private static DecisionCoreSettings Settings() => new(
        0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
        0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
        4, 24, 8);

    private static DecisionCandidatePersistence Candidate(string code, string name, decimal composite, decimal verification, bool winner)
        => new(
            Guid.NewGuid(), code, name, name,
            LegalSupport: 0.60m, FactSupport: 0.60m, EvidenceSupport: 0.55m, AuthoritySupport: 0.55m,
            Verification: verification, Uncertainty: 0.30m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: composite, DecisionSupportCeiling: 0.90m,
            RankOrder: winner ? 1 : 2, IsWinner: winner, IsEliminated: false);

    private static DecisionBranchPersistence Branch(string code, string state, bool onFrontier)
        => new(
            Guid.NewGuid(), null, 1, code, code, null, state,
            InformationValue: 0.40m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: onFrontier, StopReason: null, SortOrder: 0);

    [Fact]
    public void EssentialDependencyFailure_FlipsWinner_AndIsIdempotent()
    {
        // Arrange: C1 leads C2 narrowly; C1 owns branch "C1.B1" whose essential dependency just failed.
        var winner = Candidate("C1", "Acme prevails", composite: 0.62m, verification: 0.80m, winner: true);
        var challenger = Candidate("C2", "Globex prevails", composite: 0.58m, verification: 0.60m, winner: false);
        var candidates = new[] { winner, challenger };

        var c1Branch = Branch("C1.B1", "ACTIVE", onFrontier: false);
        var c2Branch = Branch("C2.B1", "ACTIVE", onFrontier: true);
        var branches = new[] { c1Branch, c2Branch };

        // A strong negative support delta on C1's branch (essential dependency invalidated).
        var signals = new[]
        {
            new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged,
                BranchId: c1Branch.DecisionBranchId,
                CandidateId: null,
                SupportDelta: -0.50,
                ReopenRequested: true,
                ReasonCode: "ESSENTIAL_DEPENDENCY_FAILED")
        };

        var reopenAllowed = new HashSet<Guid> { c1Branch.DecisionBranchId, c2Branch.DecisionBranchId };

        // Act
        var result = DecisionRecompetition.Run(candidates, branches, signals, Settings(), reopenAllowed);

        // Assert: the winner flipped away from C1.
        Assert.True(result.WinnerChanged);
        Assert.Equal(winner.DecisionCandidateId, result.PreviousWinnerId);
        Assert.Equal(challenger.DecisionCandidateId, result.CurrentWinnerId);
        Assert.True(result.ReopenedBranchCount >= 1);

        // Idempotent: re-running with the already-recompeted state and the same signals must not
        // flip again (deterministic, stable ranking).
        var second = DecisionRecompetition.Run(result.Candidates, result.Branches, signals, Settings(), reopenAllowed);
        Assert.Equal(result.CurrentWinnerId, second.CurrentWinnerId);
    }

    [Fact]
    public void NoSignals_LeavesRankingUnchanged()
    {
        var winner = Candidate("C1", "Acme prevails", composite: 0.62m, verification: 0.80m, winner: true);
        var challenger = Candidate("C2", "Globex prevails", composite: 0.58m, verification: 0.60m, winner: false);
        var candidates = new[] { winner, challenger };
        var branches = new[] { Branch("C1.B1", "ACTIVE", false), Branch("C2.B1", "ACTIVE", true) };

        var result = DecisionRecompetition.Run(candidates, branches, [], Settings(), new HashSet<Guid>());

        Assert.False(result.WinnerChanged);
        Assert.Equal(winner.DecisionCandidateId, result.CurrentWinnerId);
        Assert.Equal(0, result.ReopenedBranchCount);
    }
}
