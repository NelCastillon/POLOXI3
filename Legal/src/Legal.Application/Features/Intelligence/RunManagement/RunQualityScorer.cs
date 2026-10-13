namespace Legal.Application.Features.Intelligence.RunManagement;

// Deterministic, explainable run-quality scorer. Converts the raw DB-backed decision signals for a run
// into a 0-100 score, a letter grade, and a dimension breakdown. Nothing is fabricated: every dimension
// is a normalization of a persisted value. A run with no decision execution has no signals and therefore
// no score (the ledger renders it as "Not scored"). Candidate count uses diminishing returns so a run
// with many weak candidates cannot outrank one with fewer, well-supported, complete candidates.
public static class RunQualityScorer
{
    // Dimension weights (sum = 1.0). Tuned so outcome completeness and support dominate, while execution
    // integrity is a small hygiene factor.
    private const double CandidateRichnessWeight = 0.25;
    private const double OutcomeSupportWeight = 0.25;
    private const double EvidenceCoverageWeight = 0.20;
    private const double ConfidenceWeight = 0.15;
    private const double StabilityWeight = 0.10;
    private const double ExecutionWeight = 0.05;

    // Candidate count at which richness saturates (further viable candidates add nothing).
    private const int CandidateSaturation = 3;

    public static RunQualityScoreDto Score(RunQualitySignals s)
    {
        var richness = CandidateSaturation <= 0
            ? 0d
            : Math.Clamp((double)s.ViableCandidateCount / CandidateSaturation, 0d, 1d);

        // Prefer the mean viable composite; fall back to the top candidate when only one is viable.
        var support = Normalize(s.MeanViableCompositeScore > 0 ? s.MeanViableCompositeScore : s.TopCompositeScore);

        // Decision-scoped coverage is the most meaningful; fall back to the overall coverage.
        var coverage = Normalize(s.DecisionEvidenceCoverage > 0 ? s.DecisionEvidenceCoverage : s.EvidenceCoverage);

        var confidence = Normalize(s.DecisionConfidence > 0 ? s.DecisionConfidence : s.FinalConfidence);

        var stability = Normalize(s.WinnerStability) * 0.5 + Normalize(s.TopKStability) * 0.5;

        var execution = ExecutionIntegrity(s);

        var composite =
            richness * CandidateRichnessWeight +
            support * OutcomeSupportWeight +
            coverage * EvidenceCoverageWeight +
            confidence * ConfidenceWeight +
            stability * StabilityWeight +
            execution * ExecutionWeight;

        var score = (int)Math.Round(Math.Clamp(composite, 0d, 1d) * 100);
        var (grade, tone) = GradeFor(score);

        var dimensions = new List<RunQualityDimensionDto>
        {
            new("L1 candidates", Pct(richness),
                s.ViableCandidateCount == s.CandidateCount
                    ? $"{s.ViableCandidateCount} viable"
                    : $"{s.ViableCandidateCount} of {s.CandidateCount} viable"),
            new("Outcome support", Pct(support), $"{Pct(support)}% mean candidate support"),
            new("Evidence coverage", Pct(coverage), $"{Pct(coverage)}% decision coverage"),
            new("Decision confidence", Pct(confidence), $"{Pct(confidence)}% confidence"),
            new("Ranking stability", Pct(stability), $"{Pct(stability)}% stable"),
            new("Execution integrity", Pct(execution), ExecutionDetail(s)),
        };

        var headline = $"{s.ViableCandidateCount} candidate{(s.ViableCandidateCount == 1 ? "" : "s")} · " +
                       $"{Pct(coverage)}% coverage · {Pct(support)}% support";

        return new RunQualityScoreDto(score, grade, tone, headline, IsBestRun: false, dimensions);
    }

    // Treats a persisted 0-1 fraction value robustly; values already in 0-100 are rescaled.
    private static double Normalize(decimal value)
    {
        var v = (double)value;
        if (v <= 0) return 0d;
        if (v > 1d) v /= 100d;
        return Math.Clamp(v, 0d, 1d);
    }

    private static double ExecutionIntegrity(RunQualitySignals s)
    {
        var completed = string.Equals(s.StatusCode, "Completed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.StatusCode, "Succeeded", StringComparison.OrdinalIgnoreCase);
        var verified = !string.IsNullOrWhiteSpace(s.AnswerVerificationCode)
                       && !string.Equals(s.AnswerVerificationCode, "Unverified", StringComparison.OrdinalIgnoreCase)
                       && !string.Equals(s.AnswerVerificationCode, "Failed", StringComparison.OrdinalIgnoreCase);
        return (completed ? 0.7d : 0d) + (verified ? 0.3d : 0d);
    }

    private static string ExecutionDetail(RunQualitySignals s)
    {
        var status = string.IsNullOrWhiteSpace(s.StatusCode) ? "Unknown" : s.StatusCode;
        return string.IsNullOrWhiteSpace(s.AnswerVerificationCode)
            ? status
            : $"{status} · {s.AnswerVerificationCode}";
    }

    private static (string Grade, string Tone) GradeFor(int score) => score switch
    {
        >= 85 => ("A", "excellent"),
        >= 70 => ("B", "good"),
        >= 55 => ("C", "fair"),
        >= 40 => ("D", "weak"),
        _ => ("E", "poor"),
    };

    private static int Pct(double fraction) => (int)Math.Round(Math.Clamp(fraction, 0d, 1d) * 100);
}
