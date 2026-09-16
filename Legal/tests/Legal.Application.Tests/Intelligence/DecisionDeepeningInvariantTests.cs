using System;
using System.Collections.Generic;
using System.Linq;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Bounded adaptive-deepening invariants (deepening loop). The core rule under test:
//   A coarse L1 branch deepens into its proposed sub-branches ONLY when it is genuinely worth it,
//   and the recursion is strictly bounded. Deepening must be cost-free (it only consumes sub-branches
//   the discovery LLM already returned) and a no-op when none are proposed, so existing sessions and
//   golden masters are unaffected.
//
// Gate (mirrors POLOXI frontier semantics):
//   deepen iff  Children.Count > 0
//           AND IsOnFrontier(branch)                       // DR >= tau_D AND FP >= tau_F
//           AND FlipPotential >= ThresholdDeepeningFlip
//           AND level < MaxDepth
public sealed class DecisionDeepeningInvariantTests
{
    // Mirrors seeded config: tau_D = 0.35, tau_F = 0.25, ThresholdDeepeningFlip = 0.40, MaxDepth = 4.
    private static DecisionCoreSettings Settings(int maxDepth = 4) => new(
        0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
        0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
        maxDepth, 24, 8);

    private static LegalDecisionService.ProposedBranch Branch(
        string name, double decisionRelevance, double flipPotential,
        params LegalDecisionService.ProposedBranch[] children) => new(
            name, $"{name} interpretation", decisionRelevance, flipPotential,
            EvidenceAvailability: 0.5, children);

    private static List<DecisionBranchPersistence> Materialize(
        LegalDecisionService.ProposedBranch root, DecisionCoreSettings settings)
    {
        var branches = new List<DecisionBranchPersistence>();
        LegalDecisionService.MaterializeBranch(root, settings, branches, parentBranchId: null, parentCode: "C1", level: 1, sortSeed: 0);
        return branches;
    }

    [Fact]
    public void EligibleBranch_DeepensIntoProposedChildren()
    {
        // High-flip, on-frontier L1 branch with a proposed sub-branch => deepens to L2.
        var root = Branch("Coarse governing-law branch", decisionRelevance: 0.80, flipPotential: 0.70,
            Branch("Decisive sub-question", decisionRelevance: 0.70, flipPotential: 0.60));

        var branches = Materialize(root, Settings());

        Assert.Equal(2, branches.Count);
        var l1 = Assert.Single(branches, b => b.LevelNumber == 1);
        var l2 = Assert.Single(branches, b => b.LevelNumber == 2);
        Assert.Equal("C1.B1", l1.BranchCode);
        Assert.Equal("C1.B1.B1", l2.BranchCode);
        Assert.Equal(l1.DecisionBranchId, l2.ParentDecisionBranchId);
    }

    [Fact]
    public void LowFlipBranch_DoesNotDeepen_EvenWithProposedChildren()
    {
        // On frontier (FP 0.30 >= tau_F 0.25) but below the deepening flip threshold (0.40): no L2.
        var root = Branch("Low-flip branch", decisionRelevance: 0.80, flipPotential: 0.30,
            Branch("Would-be sub-question", decisionRelevance: 0.70, flipPotential: 0.60));

        var branches = Materialize(root, Settings());

        var only = Assert.Single(branches);
        Assert.Equal(1, only.LevelNumber);
    }

    [Fact]
    public void OffFrontierBranch_DoesNotDeepen_EvenWithHighFlip()
    {
        // High flip but decision relevance below tau_D (0.35) => off frontier => no deepening.
        var root = Branch("Off-frontier branch", decisionRelevance: 0.10, flipPotential: 0.90,
            Branch("Would-be sub-question", decisionRelevance: 0.70, flipPotential: 0.60));

        var branches = Materialize(root, Settings());

        var only = Assert.Single(branches);
        Assert.Equal(1, only.LevelNumber);
        Assert.False(only.IsOnFrontier);
    }

    [Fact]
    public void NoProposedChildren_IsNoOp_SingleFlatBranch()
    {
        // The common case: no sub-branches proposed => identical to the prior flat single-pass output.
        var root = Branch("Coarse branch", decisionRelevance: 0.80, flipPotential: 0.90);

        var branches = Materialize(root, Settings());

        var only = Assert.Single(branches);
        Assert.Equal(1, only.LevelNumber);
        Assert.Null(only.ParentDecisionBranchId);
        Assert.Equal("C1.B1", only.BranchCode);
    }

    [Fact]
    public void Recursion_IsBoundedByMaxDepth()
    {
        // A chain of eligible branches deeper than MaxDepth must stop exactly at MaxDepth.
        var deepest = Branch("L5", 0.80, 0.70);
        var l4 = Branch("L4", 0.80, 0.70, deepest);
        var l3 = Branch("L3", 0.80, 0.70, l4);
        var l2 = Branch("L2", 0.80, 0.70, l3);
        var root = Branch("L1", 0.80, 0.70, l2);

        var branches = Materialize(root, Settings(maxDepth: 3));

        Assert.Equal(3, branches.Count);
        Assert.Equal(3, branches.Max(b => b.LevelNumber));
        Assert.DoesNotContain(branches, b => b.LevelNumber > 3);
    }

    [Fact]
    public void DeepenedChildren_InheritHierarchicalCode_ResolvingOwningCandidate()
    {
        // BranchCode.Split('.')[0] must still resolve the owning candidate at every level.
        var root = Branch("L1", 0.80, 0.70,
            Branch("L2", 0.80, 0.70,
                Branch("L3", 0.80, 0.70)));

        var branches = Materialize(root, Settings());

        Assert.All(branches, b => Assert.Equal("C1", b.BranchCode.Split('.')[0]));
        Assert.Equal(new[] { "C1.B1", "C1.B1.B1", "C1.B1.B1.B1" },
            branches.OrderBy(b => b.LevelNumber).Select(b => b.BranchCode).ToArray());
    }
}
