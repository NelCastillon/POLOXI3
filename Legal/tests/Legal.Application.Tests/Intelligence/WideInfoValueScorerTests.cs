using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Regression coverage for the deterministic L2 info-value prioritization that replaced the former
// synchronous WideL2PriorityReview LLM round-trip. The score is ORDER-ONLY: it must never gate
// inclusion, must stay bounded to [0,1], and must rank the pivotal (relevant, required, under-covered)
// parent ahead of a saturated, already-documented one.
public sealed class WideInfoValueScorerTests
{
    // Default criterion weights (mirrors the DB-seeded defaults loaded by IntelligenceWide2Repository:
    // ranking impact .25, uncertainty .20, discrimination .25, evidence availability .15, novelty .10,
    // redundancy penalty .05).
    private static WideConfiguration Config() => new(
        TargetConfidence: .80m, MinimumBranchConfidence: .20m, MaximumBranchesPerLevel: 12,
        AbsoluteDepthCeiling: 4, MaximumTotalLlmCalls: 40)
    {
        CriterionRankingImpactWeight = .25m,
        CriterionUncertaintyWeight = .20m,
        CriterionDiscriminationWeight = .25m,
        CriterionEvidenceAvailabilityWeight = .15m,
        CriterionNoveltyWeight = .10m,
        CriterionRedundancyPenalty = .05m,
    };

    [Fact]
    public void Score_Is_Always_Bounded_To_Unit_Interval()
    {
        var config = Config();

        // Extreme "high" inputs and extreme "redundant" inputs both stay within [0,1].
        var high = WideInfoValueScorer.Score(relevance: 5m, mappedPassageCount: 0, documentPresent: false,
            requiredCoverage: true, evidenceCount: 1000, priorConfidence: 5m, config);
        var redundant = WideInfoValueScorer.Score(relevance: -5m, mappedPassageCount: 999, documentPresent: true,
            requiredCoverage: false, evidenceCount: 0, priorConfidence: -5m, config);

        Assert.InRange(high, 0m, 1m);
        Assert.InRange(redundant, 0m, 1m);
    }

    [Fact]
    public void Pivotal_Parent_Ranks_Ahead_Of_Saturated_Documented_Parent()
    {
        var config = Config();

        // Pivotal: highly relevant, required coverage, has retrieval evidence but NOT yet saturated.
        var pivotal = WideInfoValueScorer.Score(relevance: .90m, mappedPassageCount: 1, documentPresent: false,
            requiredCoverage: true, evidenceCount: 6, priorConfidence: .60m, config);

        // Saturated: already fully covered and documented, so re-decomposing yields little new info.
        var saturated = WideInfoValueScorer.Score(relevance: .90m, mappedPassageCount: 20, documentPresent: true,
            requiredCoverage: false, evidenceCount: 6, priorConfidence: .60m, config);

        Assert.True(pivotal > saturated,
            $"pivotal ({pivotal}) should outrank saturated ({saturated})");
    }

    [Fact]
    public void Higher_Relevance_Raises_Score_All_Else_Equal()
    {
        var config = Config();

        var low = WideInfoValueScorer.Score(relevance: .20m, mappedPassageCount: 2, documentPresent: false,
            requiredCoverage: false, evidenceCount: 3, priorConfidence: .40m, config);
        var high = WideInfoValueScorer.Score(relevance: .95m, mappedPassageCount: 2, documentPresent: false,
            requiredCoverage: false, evidenceCount: 3, priorConfidence: .40m, config);

        Assert.True(high > low, $"higher relevance ({high}) should exceed lower relevance ({low})");
    }

    [Fact]
    public void Required_Coverage_Adds_Discrimination_Weight()
    {
        var config = Config();

        var optional = WideInfoValueScorer.Score(relevance: .50m, mappedPassageCount: 2, documentPresent: false,
            requiredCoverage: false, evidenceCount: 4, priorConfidence: .30m, config);
        var required = WideInfoValueScorer.Score(relevance: .50m, mappedPassageCount: 2, documentPresent: false,
            requiredCoverage: true, evidenceCount: 4, priorConfidence: .30m, config);

        Assert.True(required > optional, $"required ({required}) should outrank optional ({optional})");
    }

    [Fact]
    public void Saturated_Document_Increases_Redundancy_Penalty()
    {
        var config = Config();

        // Same inputs except one already has the document present at full coverage (more redundant).
        var notDocumented = WideInfoValueScorer.Score(relevance: .50m, mappedPassageCount: 8, documentPresent: false,
            requiredCoverage: false, evidenceCount: 2, priorConfidence: .30m, config);
        var documented = WideInfoValueScorer.Score(relevance: .50m, mappedPassageCount: 8, documentPresent: true,
            requiredCoverage: false, evidenceCount: 2, priorConfidence: .30m, config);

        Assert.True(documented < notDocumented,
            $"documented+saturated ({documented}) should score below not-documented ({notDocumented})");
    }

    [Fact]
    public void Score_Is_Deterministic_For_Identical_Inputs()
    {
        var config = Config();

        var a = WideInfoValueScorer.Score(.7m, 3, false, true, 5, .5m, config);
        var b = WideInfoValueScorer.Score(.7m, 3, false, true, 5, .5m, config);

        Assert.Equal(a, b);
    }
}
