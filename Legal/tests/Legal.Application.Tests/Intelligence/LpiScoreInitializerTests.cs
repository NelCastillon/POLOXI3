using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// LPI ancestor-informed score INITIALIZER — reproducible, inspectable regression suite.
//
// Every case is self-contained and prints its own original-vs-proposed calculation via the result's
// Explanation. The initializer is advisory and does NOT change parent aggregation; these tests pin:
//   • Disabled default == existing local-only initialization (no behavior change).
//   • The single worked example: local midpoint 60/80 → 70, ancestors {90,50} with α=0.2, λ=0.5
//       A_c = (1·90 + 0.5·50)/(1 + 0.5) = 115/1.5 = 76.6667 → S = 0.8·70 + 0.2·76.6667 = 71.3333 → 71.33.
//   • Added hierarchy depth alone does not inflate the score (deeper ancestor is down-weighted).
//   • Duplicate ancestor delivery cannot double-count (self node excluded; caller supplies distinct nodes).
//   • No local baseline but ancestors → provisional, review-required (never silently trusted).
//   • Neither baseline available → Uninitialized (no value invented).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LpiScoreInitializerTests
{
    private static LpiScoreInitializer Create(bool enabled, decimal alpha = 0.2m, decimal lambda = 0.5m)
        => new(new LpiScoreInitializerOptions
        {
            AncestorInfluenceEnabled = enabled,
            Alpha = alpha,
            Lambda = lambda
        });

    private static LpiScoreInitializerInput Input(
        decimal? prev, decimal? next, params LpiAncestorScore[] ancestors)
        => new(
            CandidateNodeId: Guid.NewGuid(),
            HierarchyRevision: 1,
            PreviousNeighborScore: prev,
            NextNeighborScore: next,
            PlacementFraction: 0.5m,
            PreInsertionAncestorScores: ancestors);

    [Fact]
    public void Disabled_PreservesExistingLocalOnlyInitialization()
    {
        var sut = Create(enabled: false);

        // Ancestors are supplied but MUST be ignored while disabled.
        var result = sut.Compute(Input(60m, 80m,
            new LpiAncestorScore(Guid.NewGuid(), 1, 90m, 1)));

        Assert.Equal(LpiInitializationMethod.Disabled, result.Method);
        Assert.True(result.HasScore);
        Assert.Equal(70m, result.InitialScore);         // local midpoint only
        Assert.Equal(70m, result.LocalBaseline);
        Assert.Null(result.AncestorContext);
        Assert.Empty(result.AncestorsUsed);
    }

    [Fact]
    public void Blended_WorkedExample_60_80_With_90_50_Yields_71_33()
    {
        var sut = Create(enabled: true, alpha: 0.2m, lambda: 0.5m);

        var result = sut.Compute(Input(60m, 80m,
            new LpiAncestorScore(Guid.NewGuid(), 1, 90m, 1),   // immediate parent, weight λ^0 = 1
            new LpiAncestorScore(Guid.NewGuid(), 2, 50m, 1))); // grandparent,     weight λ^1 = 0.5

        Assert.Equal(LpiInitializationMethod.Blended, result.Method);
        Assert.Equal(70m, result.LocalBaseline);
        Assert.Equal(76.67m, result.AncestorContext);         // (90 + 0.5*50)/1.5 = 76.6667 → 76.67
        Assert.Equal(71.33m, result.InitialScore);            // 0.8*70 + 0.2*76.6667 = 71.3333 → 71.33
        Assert.Equal(2, result.AncestorsUsed.Count);
    }

    [Fact]
    public void AddedDepth_DoesNotInflate_DeeperAncestorIsDownWeighted()
    {
        var sut = Create(enabled: true, alpha: 0.2m, lambda: 0.5m);

        // Same parent (90); adding a distant high ancestor must move the result LESS than the parent alone.
        var parentOnly = sut.Compute(Input(60m, 80m,
            new LpiAncestorScore(Guid.NewGuid(), 1, 90m, 1)));

        var withDeepAncestor = sut.Compute(Input(60m, 80m,
            new LpiAncestorScore(Guid.NewGuid(), 1, 90m, 1),
            new LpiAncestorScore(Guid.NewGuid(), 5, 100m, 1))); // far ancestor, weight λ^4 = 0.0625

        // The far ancestor barely nudges A_c, so the initial score stays close to the parent-only result.
        Assert.True(Math.Abs(withDeepAncestor.InitialScore!.Value - parentOnly.InitialScore!.Value) < 1m);
    }

    [Fact]
    public void SelfNode_IsExcluded_NoSelfReference()
    {
        var candidateId = Guid.NewGuid();
        var sut = Create(enabled: true);

        var input = new LpiScoreInitializerInput(
            CandidateNodeId: candidateId,
            HierarchyRevision: 1,
            PreviousNeighborScore: 60m,
            NextNeighborScore: 80m,
            PlacementFraction: 0.5m,
            PreInsertionAncestorScores: new[]
            {
                new LpiAncestorScore(candidateId, 1, 10m, 1),         // self — must be dropped
                new LpiAncestorScore(Guid.NewGuid(), 1, 90m, 1)
            });

        var result = sut.Compute(input);

        Assert.Single(result.AncestorsUsed);
        Assert.Equal(90m, result.AncestorContext);
    }

    [Fact]
    public void NoLocalBaseline_WithAncestors_IsProvisionalReviewRequired()
    {
        var sut = Create(enabled: true);

        var result = sut.Compute(Input(null, null,
            new LpiAncestorScore(Guid.NewGuid(), 1, 90m, 1)));

        Assert.Equal(LpiInitializationMethod.AncestorOnlyReviewRequired, result.Method);
        Assert.True(result.HasScore);
        Assert.Null(result.LocalBaseline);
        Assert.Equal(90m, result.InitialScore);
        Assert.Contains("review required", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoBaselineAndNoAncestors_IsUninitialized_NoValueInvented()
    {
        var sut = Create(enabled: true);

        var result = sut.Compute(Input(null, null));

        Assert.Equal(LpiInitializationMethod.Uninitialized, result.Method);
        Assert.False(result.HasScore);
        Assert.Null(result.InitialScore);
    }

    [Fact]
    public void SingleNeighbor_UsesThatNeighborAsLocalBaseline()
    {
        var sut = Create(enabled: true);

        var result = sut.Compute(Input(60m, null,
            new LpiAncestorScore(Guid.NewGuid(), 1, 60m, 1)));

        Assert.Equal(LpiInitializationMethod.Blended, result.Method);
        Assert.Equal(60m, result.LocalBaseline);
    }
}
