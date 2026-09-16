using System;
using System.Collections.Generic;
using System.Linq;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Flip-point projection invariants. A flip point that changes the winner is, at minimum, a swap
// of the top two candidates — an ordinal rank displacement of 1. The projection must never emit
// "Δrank 0 · flips winner", which is self-contradictory. This exercises presentation/projection
// logic only; it does not touch POLOXI candidate scoring or frontier selection.
public sealed class DecisionFlipPointInvariantTests
{
    private static DecisionBranchPersistence Branch(string name, decimal flipPotential, bool onFrontier, string? branchCode = null) => new(
        Guid.NewGuid(), null, 1, branchCode ?? name.Replace(' ', '_').ToUpperInvariant(), name, null, "OPEN",
        InformationValue: 0.6m, DecisionRelevance: 0.6m, FlipPotential: flipPotential,
        EvidenceAvailability: 0.5m, AdvScore: 0.5m, Cost: 0.2m, IsOnFrontier: onFrontier,
        StopReason: null, SortOrder: 0);

    private static DecisionCandidatePersistence Candidate(string code, int rankOrder, bool isWinner) => new(
        Guid.NewGuid(), code, code, code,
        LegalSupport: 0.5m, FactSupport: 0.5m, EvidenceSupport: 0.5m, AuthoritySupport: 0.5m,
        Verification: 0.5m, Uncertainty: 0.5m, Discrimination: 0.5m, RankingImpact: 0.5m,
        Diversity: 0.5m, RedundancyPenalty: 0m, CompositeScore: 0.5m, DecisionSupportCeiling: 1m,
        RankOrder: rankOrder, IsWinner: isWinner, IsEliminated: false);

    [Fact]
    public void WinnerChangingFlipPoint_HasNonZeroRankDelta()
    {
        var branches = new List<DecisionBranchPersistence>
        {
            Branch("Pollutants Interpretation", flipPotential: 0.70m, onFrontier: true, branchCode: "GRANT.POLLUTANTS")
        };
        var candidates = new List<DecisionCandidatePersistence>
        {
            Candidate("DENY", rankOrder: 1, isWinner: true),
            Candidate("GRANT", rankOrder: 2, isWinner: false)
        };

        var flips = LegalDecisionService.BuildFlipPoints(branches, candidates, Guid.NewGuid(), Guid.NewGuid());

        var winnerFlip = Assert.Single(flips);
        Assert.True(winnerFlip.WinnerChanges);
        Assert.NotEqual(0, winnerFlip.RankDelta);
    }

    [Fact]
    public void HighFlipBranchOwnedByWinner_IsNotAWinnerFlip()
    {
        // A branch that belongs to the current winner cannot flip the winner, even with high flip
        // potential. This prevents the self-contradictory "Winner: Deny → for Deny" rendering.
        var branches = new List<DecisionBranchPersistence>
        {
            Branch("Conflict of Terms Resolution", flipPotential: 0.88m, onFrontier: true, branchCode: "DENY.CONFLICT")
        };
        var candidates = new List<DecisionCandidatePersistence>
        {
            Candidate("DENY", rankOrder: 1, isWinner: true),
            Candidate("GRANT", rankOrder: 2, isWinner: false)
        };

        var flips = LegalDecisionService.BuildFlipPoints(branches, candidates, Guid.NewGuid(), Guid.NewGuid());

        var flip = Assert.Single(flips);
        Assert.False(flip.WinnerChanges);
        Assert.Equal(0, flip.RankDelta);
    }

    [Fact]
    public void NonWinnerChangingFlipPoint_HasZeroRankDelta()
    {
        var branches = new List<DecisionBranchPersistence>
        {
            Branch("Minor Ambiguity", flipPotential: 0.20m, onFrontier: true, branchCode: "GRANT.MINOR")
        };
        var candidates = new List<DecisionCandidatePersistence>
        {
            Candidate("DENY", rankOrder: 1, isWinner: true),
            Candidate("GRANT", rankOrder: 2, isWinner: false)
        };

        var flips = LegalDecisionService.BuildFlipPoints(branches, candidates, Guid.NewGuid(), Guid.NewGuid());

        var flip = Assert.Single(flips);
        Assert.False(flip.WinnerChanges);
        Assert.Equal(0, flip.RankDelta);
    }

    [Fact]
    public void OffFrontierBranches_ProduceNoFlipPoints()
    {
        var branches = new List<DecisionBranchPersistence>
        {
            Branch("Resolved Issue", flipPotential: 0.80m, onFrontier: false)
        };

        var flips = LegalDecisionService.BuildFlipPoints(branches, new List<DecisionCandidatePersistence>(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(flips);
    }
}
