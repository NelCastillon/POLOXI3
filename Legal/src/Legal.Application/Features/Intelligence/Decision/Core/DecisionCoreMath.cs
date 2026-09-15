using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Core control math for the Legal Decision module. Every formula here is DETERMINISTIC and
// authoritative — the LLM never supplies these numbers (§3). Weights/thresholds are injected from
// DB-backed DecisionCoreSettings (§11,§12). Formula references map to the 1–61 placement map.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public static class DecisionCoreMath
{
    private const double Epsilon = 1e-9;

    // Composite candidate score from the multi-dimensional epistemic state (§9). The composite is an
    // internal ranking convenience; the component dimensions remain observable and are persisted.
    public static double CompositeScore(double legal, double fact, double evidence, double authority, double verification)
    {
        // Verification is weighted highest: only verified support authorizes decision strength (§17,§23).
        var raw = 0.25 * legal + 0.20 * fact + 0.20 * evidence + 0.15 * authority + 0.20 * verification;
        return Clamp01(raw);
    }

    // Certainty ceiling (§16,§17): a candidate's decision support can never exceed the weakest of its
    // essential dependencies. DecisionSupport(C) ≤ min(EssentialLaw, EssentialFacts, EssentialEvidence, …).
    public static double CertaintyCeiling(params double[] essentialSupports)
    {
        if (essentialSupports.Length == 0)
            return 1d;
        var min = 1d;
        foreach (var s in essentialSupports)
            min = Math.Min(min, Clamp01(s));
        return min;
    }

    // Softmax distribution over candidate scores, used only as an uncertainty signal (§9,§32):
    // "entropy measures uncertainty, not truth".
    public static double[] Distribution(IReadOnlyList<double> scores)
    {
        if (scores.Count == 0)
            return [];
        var max = double.NegativeInfinity;
        foreach (var s in scores)
            max = Math.Max(max, s);
        var exps = new double[scores.Count];
        var sum = 0d;
        for (var i = 0; i < scores.Count; i++)
        {
            exps[i] = Math.Exp(scores[i] - max);
            sum += exps[i];
        }
        if (sum <= Epsilon)
            return scores.Select(_ => 1d / scores.Count).ToArray();
        for (var i = 0; i < exps.Length; i++)
            exps[i] /= sum;
        return exps;
    }

    // Normalized candidate entropy (§32): H_N(C) = H(C)/log n, in [0,1]. n<2 ⇒ 0 (no uncertainty).
    public static double NormalizedEntropy(IReadOnlyList<double> probabilities)
    {
        if (probabilities.Count < 2)
            return 0d;
        var h = 0d;
        foreach (var p in probabilities)
            if (p > Epsilon)
                h -= p * Math.Log(p);
        return Clamp01(h / Math.Log(probabilities.Count));
    }

    // Candidate margin (§32): M = Score(C1) − Score(C2) between the top two candidates.
    public static double Margin(IReadOnlyList<double> orderedDescendingScores)
        => orderedDescendingScores.Count < 2 ? (orderedDescendingScores.Count == 1 ? orderedDescendingScores[0] : 0d)
            : orderedDescendingScores[0] - orderedDescendingScores[1];

    // Information Value (§12): IV(B) = 0.20U + 0.25RI + 0.25D + 0.15EA + 0.10N − 0.05RP, using the
    // DB-backed weights. IV is exploration value only — later control uses it as one input, not the
    // sole authority (IV → ExplorationValue, not IV → Everything).
    public static double InformationValue(DecisionCoreSettings s, double u, double ri, double d, double ea, double novelty, double redundancyPenalty)
        => Math.Max(0d,
            s.WeightUncertainty * Clamp01(u)
            + s.WeightRankingImpact * Clamp01(ri)
            + s.WeightDiscrimination * Clamp01(d)
            + s.WeightEvidenceAvailability * Clamp01(ea)
            + s.WeightNovelty * Clamp01(novelty)
            - s.WeightRedundancyPenalty * Clamp01(redundancyPenalty));

    // Legal advantage (§12): LegalADV(B) = IV(B) × DR(B) × FP(B) / (Cost(B) + ε). Drives which branch
    // to resolve next and whether research is exhausted (§13,§34).
    public static double LegalAdv(double informationValue, double decisionRelevance, double flipPotential, double cost)
        => informationValue * Clamp01(decisionRelevance) * Clamp01(flipPotential) / (Math.Max(0d, cost) + Epsilon);

    // Frontier membership (§11): F = { B : Unresolved(B) ∧ DR(B) ≥ τ_D ∧ FP(B) ≥ τ_F }.
    public static bool IsOnFrontier(DecisionCoreSettings s, string branchState, double decisionRelevance, double flipPotential)
        => !string.Equals(branchState, DecisionBranchStates.Resolved, StringComparison.Ordinal)
            && decisionRelevance >= s.ThresholdDecisionRelevance
            && flipPotential >= s.ThresholdFlipPotential;

    // Evidence verification value (§14): EV = Identity × Citation × Holding × Weight × PropositionFit.
    // If any essential factor is 0 the source cannot support the proposition.
    public static double EvidenceVerificationValue(double identity, double citation, double holding, double weight, double propositionFit)
        => Clamp01(identity) * Clamp01(citation) * Clamp01(holding) * Clamp01(weight) * Clamp01(propositionFit);

    // Jaccard token similarity for candidate diversity/redundancy (§8): Diversity = 1 − max Sim; RP = max Sim.
    public static double Similarity(string? a, string? b)
    {
        var ta = Tokenize(a);
        var tb = Tokenize(b);
        if (ta.Count == 0 || tb.Count == 0)
            return 0d;
        var intersect = ta.Count(tb.Contains);
        var union = ta.Count + tb.Count - intersect;
        return union == 0 ? 0d : (double)intersect / union;
    }

    private static HashSet<string> Tokenize(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.ToLowerInvariant().Split([' ', '\t', '\n', '\r', ',', '.', ';', ':', '(', ')', '-', '/'], StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    public static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;
}
