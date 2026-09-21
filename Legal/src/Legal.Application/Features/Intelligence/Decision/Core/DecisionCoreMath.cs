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

    // English function words that carry no proposition-support signal. Raw Jaccard over these produces
    // false overlap (e.g. an unrelated title sharing "that"/"for" with a legal objective), so they are
    // removed before measuring whether a passage actually supports a proposition. Kept deliberately
    // small and generic — this is stopword filtering, not domain modelling.
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "been", "being", "but", "by", "for", "from",
        "had", "has", "have", "he", "her", "his", "how", "i", "if", "in", "into", "is", "it", "its",
        "may", "no", "nor", "not", "of", "on", "or", "over", "s", "she", "should", "so", "some",
        "such", "than", "that", "the", "their", "them", "then", "there", "these", "they", "this",
        "those", "to", "under", "up", "was", "we", "were", "what", "when", "where", "whether",
        "which", "while", "who", "whom", "will", "with", "would", "you", "your",
    };

    private static HashSet<string> ContentTokens(string? text)
    {
        var tokens = Tokenize(text);
        tokens.RemoveWhere(t => t.Length < 3 || StopWords.Contains(t));
        return tokens;
    }

    // Proposition-support measure for the evidence verifier (§14). Unlike the generic candidate-diversity
    // Similarity, this removes stopwords/short tokens (so incidental overlap on words like "that"/"for"
    // cannot manufacture support) and requires a MINIMUM number of shared CONTENT tokens. A single
    // incidental content match (e.g. "employees") is not enough to establish that a passage supports a
    // proposition. Returns content-token Jaccard, or 0 when the shared-content-token floor is not met.
    public const int MinSharedContentTokens = 2;

    public static double PropositionSupport(string? objective, string? passage)
    {
        var to = ContentTokens(objective);
        var tp = ContentTokens(passage);
        if (to.Count == 0 || tp.Count == 0)
            return 0d;
        var intersect = to.Count(tp.Contains);
        if (intersect < MinSharedContentTokens)
            return 0d;   // too little genuine overlap to claim support — reject incidental single hits
        var union = to.Count + tp.Count - intersect;
        return union == 0 ? 0d : (double)intersect / union;
    }

    public static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;

    // Passage-vs-identity guard (§14). A retrieved source's TITLE establishes IDENTITY, not PROPOSITION
    // SUPPORT. When the "supporting passage" contributes no content tokens beyond the title itself (i.e.
    // the snippet merely echoes the title, as with a bare "Proposed Rule on Overtime Pay" result), there
    // is NO independently-located passage to support any proposition — topical relevance must never be
    // mistaken for support. Returns true when the passage adds nothing beyond the title's content tokens.
    public static bool PassageEchoesTitle(string? title, string? passage)
    {
        var passageTokens = ContentTokens(passage);
        if (passageTokens.Count == 0)
            return true;   // no content at all → certainly not an independent supporting passage
        var titleTokens = ContentTokens(title);
        // The passage carries independent substance only if it contributes at least one content token the
        // title does not already carry. Otherwise it is a title echo (identity), not a located passage.
        passageTokens.ExceptWith(titleTokens);
        return passageTokens.Count == 0;
    }
}