using Legal.Application.Features.Intelligence.RunManagement;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ──────────────────────────────────────────────────────────────────────────────────────────────
// RunQualityScorer — deterministic, explainable per-run quality score regression suite.
//
// The scorer converts persisted, DB-backed decision signals into a 0-100 score + letter grade. These
// tests pin the behavior the feature depends on:
//   • A complete, well-supported, verified run scores high (grade A/B).
//   • A single weak candidate run scores low — many weak candidates cannot inflate the score.
//   • Constraint-violating candidates are excluded from the viable count.
//   • Fraction (0-1) and percent (0-100) persisted values are both normalized correctly.
//   • Candidate richness saturates (diminishing returns) so quantity alone never dominates quality.
// ──────────────────────────────────────────────────────────────────────────────────────────────
public sealed class RunQualityScorerTests
{
    private static RunQualitySignals Signals(
        string status = "Completed",
        string? verification = "Verified",
        decimal finalConfidence = 0m,
        decimal decisionConfidence = 0m,
        decimal evidenceCoverage = 0m,
        decimal decisionEvidenceCoverage = 0m,
        decimal winnerStability = 0m,
        decimal topKStability = 0m,
        int candidateCount = 0,
        int viableCandidateCount = 0,
        decimal topCompositeScore = 0m,
        decimal meanViableCompositeScore = 0m)
        => new(
            Guid.NewGuid(), status, verification, finalConfidence, decisionConfidence,
            evidenceCoverage, decisionEvidenceCoverage, winnerStability, topKStability,
            candidateCount, viableCandidateCount, topCompositeScore, meanViableCompositeScore);

    [Fact]
    public void StrongCompleteRun_ScoresHigh()
    {
        var s = Signals(
            decisionConfidence: 0.9m, decisionEvidenceCoverage: 0.88m,
            winnerStability: 0.85m, topKStability: 0.8m,
            candidateCount: 4, viableCandidateCount: 4,
            topCompositeScore: 0.92m, meanViableCompositeScore: 0.84m);

        var result = RunQualityScorer.Score(s);

        Assert.True(result.Score >= 80, $"Expected a high score, got {result.Score}.");
        Assert.Contains(result.Grade, new[] { "A", "B" });
        Assert.Equal(6, result.Dimensions.Count);
    }

    [Fact]
    public void SingleWeakCandidateRun_ScoresLow()
    {
        var s = Signals(
            decisionConfidence: 0.2m, decisionEvidenceCoverage: 0.15m,
            winnerStability: 0.2m, topKStability: 0.1m,
            candidateCount: 1, viableCandidateCount: 1,
            topCompositeScore: 0.2m, meanViableCompositeScore: 0.2m);

        var result = RunQualityScorer.Score(s);

        Assert.True(result.Score <= 40, $"Expected a low score, got {result.Score}.");
        Assert.Contains(result.Grade, new[] { "D", "E" });
    }

    [Fact]
    public void ManyWeakCandidates_DoNotBeatFewerStrongCandidates()
    {
        var manyWeak = RunQualityScorer.Score(Signals(
            decisionConfidence: 0.2m, decisionEvidenceCoverage: 0.2m,
            candidateCount: 8, viableCandidateCount: 8,
            topCompositeScore: 0.25m, meanViableCompositeScore: 0.22m));

        var fewStrong = RunQualityScorer.Score(Signals(
            decisionConfidence: 0.85m, decisionEvidenceCoverage: 0.85m,
            winnerStability: 0.8m, topKStability: 0.8m,
            candidateCount: 2, viableCandidateCount: 2,
            topCompositeScore: 0.9m, meanViableCompositeScore: 0.85m));

        Assert.True(fewStrong.Score > manyWeak.Score,
            $"Strong run ({fewStrong.Score}) should beat many weak candidates ({manyWeak.Score}).");
    }

    [Fact]
    public void PercentScaledValues_NormalizeSameAsFractions()
    {
        var fraction = RunQualityScorer.Score(Signals(
            decisionConfidence: 0.75m, decisionEvidenceCoverage: 0.6m,
            candidateCount: 2, viableCandidateCount: 2, meanViableCompositeScore: 0.7m));

        var percent = RunQualityScorer.Score(Signals(
            decisionConfidence: 75m, decisionEvidenceCoverage: 60m,
            candidateCount: 2, viableCandidateCount: 2, meanViableCompositeScore: 70m));

        Assert.Equal(fraction.Score, percent.Score);
    }

    [Fact]
    public void CandidateRichness_Saturates()
    {
        var three = RunQualityScorer.Score(Signals(
            candidateCount: 3, viableCandidateCount: 3, meanViableCompositeScore: 0.6m));
        var ten = RunQualityScorer.Score(Signals(
            candidateCount: 10, viableCandidateCount: 10, meanViableCompositeScore: 0.6m));

        Assert.Equal(three.Score, ten.Score);
    }

    [Fact]
    public void IncompleteExecution_LowersScore()
    {
        var completed = RunQualityScorer.Score(Signals(
            status: "Completed", verification: "Verified",
            decisionConfidence: 0.7m, decisionEvidenceCoverage: 0.7m,
            candidateCount: 2, viableCandidateCount: 2, meanViableCompositeScore: 0.7m));

        var failed = RunQualityScorer.Score(Signals(
            status: "Failed", verification: "Unverified",
            decisionConfidence: 0.7m, decisionEvidenceCoverage: 0.7m,
            candidateCount: 2, viableCandidateCount: 2, meanViableCompositeScore: 0.7m));

        Assert.True(failed.Score < completed.Score,
            $"Failed/unverified ({failed.Score}) should score below completed/verified ({completed.Score}).");
    }

    [Fact]
    public void Headline_ReflectsViableCandidateCount()
    {
        var result = RunQualityScorer.Score(Signals(
            candidateCount: 5, viableCandidateCount: 3,
            decisionEvidenceCoverage: 0.8m, meanViableCompositeScore: 0.7m));

        Assert.Contains("3 candidate", result.Headline);
    }
}
