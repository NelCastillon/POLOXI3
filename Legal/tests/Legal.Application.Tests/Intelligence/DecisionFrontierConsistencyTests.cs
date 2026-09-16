using System;
using System.Collections.Generic;
using System.Linq;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// V2 readiness / frontier-consistency invariants. These lock in two projection-layer corrections:
//   1. POLOXI owns the frontier: the ordinary readiness panel and the V2 dependency-readiness gate
//      must consume ONE authoritative high-impact frontier count (CountHighImpactFrontier), so they
//      can never independently reconstruct — and disagree about — frontier state.
//   2. High-impact means IsOnFrontier AND FlipPotential >= 0.40 (a low-flip frontier branch is not
//      "high-impact"), matching the V2 gate that already used this threshold.
// These do not touch POLOXI scoring, propagation, or frontier ownership.
public sealed class DecisionFrontierConsistencyTests
{
    private static DecisionBranchPersistence Branch(string name, decimal flipPotential, bool onFrontier) => new(
        Guid.NewGuid(), null, 1, name.Replace(' ', '_').ToUpperInvariant(), name, null, "OPEN",
        InformationValue: 0.6m, DecisionRelevance: 0.6m, FlipPotential: flipPotential,
        EvidenceAvailability: 0.5m, AdvScore: 0.5m, Cost: 0.2m, IsOnFrontier: onFrontier,
        StopReason: null, SortOrder: 0);

    [Fact]
    public void HighImpactFrontier_CountsOnlyOnFrontierBranchesWithSufficientFlip()
    {
        var branches = new List<DecisionBranchPersistence>
        {
            Branch("High flip on frontier",  flipPotential: 0.80m, onFrontier: true),   // counts
            Branch("Low flip on frontier",   flipPotential: 0.20m, onFrontier: true),   // excluded (low flip)
            Branch("High flip off frontier", flipPotential: 0.90m, onFrontier: false),  // excluded (off frontier)
        };

        Assert.Equal(1, LegalDecisionService.CountHighImpactFrontier(branches));
    }

    [Fact]
    public void HighImpactFrontier_FlipPotentialThresholdIsInclusive()
    {
        var branches = new List<DecisionBranchPersistence>
        {
            Branch("At threshold", flipPotential: 0.40m, onFrontier: true)
        };

        Assert.Equal(1, LegalDecisionService.CountHighImpactFrontier(branches));
    }

    [Fact]
    public void HighImpactFrontier_EmptyWhenNothingOnFrontier()
    {
        var branches = new List<DecisionBranchPersistence>
        {
            Branch("Resolved high flip", flipPotential: 0.95m, onFrontier: false)
        };

        Assert.Equal(0, LegalDecisionService.CountHighImpactFrontier(branches));
    }
}
