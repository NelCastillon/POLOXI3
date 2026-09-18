namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1 claim identity / deduplication (§15).
//
// The LLM may restate the same proposition many ways. The extractor may PROPOSE equivalence, but
// POLOXI owns registration. This deterministic resolver normalizes and matches proposals against the
// existing authoritative claim set so we do not create redundant independent claims.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

public enum ClaimIdentityOutcome
{
    New,
    Equivalent,
    Refinement,
    Contradiction,
    Distinct
}

public sealed record ClaimIdentityResolution(
    ClaimIdentityOutcome Outcome,
    Guid? MatchedClaimId,
    string NormalizedText,
    double Similarity,
    string Reason);

public interface IClaimIdentityResolver
{
    ClaimIdentityResolution Resolve(ClaimProposal proposal, IReadOnlyList<ClaimProposition> existingClaims);

    string Normalize(string text);
}

public sealed class ClaimIdentityResolver : IClaimIdentityResolver
{
    // Above this token-set similarity two claims are treated as the same proposition.
    private const double EquivalenceThreshold = 0.82d;

    // Above this (but below equivalence) they are a refinement of the same underlying proposition.
    private const double RefinementThreshold = 0.60d;

    private static readonly HashSet<string> NegationTokens =
        new(StringComparer.OrdinalIgnoreCase) { "not", "no", "never", "without", "cannot", "n't" };

    public ClaimIdentityResolution Resolve(ClaimProposal proposal, IReadOnlyList<ClaimProposition> existingClaims)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(existingClaims);

        var normalized = string.IsNullOrWhiteSpace(proposal.NormalizedText)
            ? Normalize(proposal.Text)
            : Normalize(proposal.NormalizedText);

        var proposalTokens = Tokenize(normalized);
        Guid? bestId = null;
        var bestSimilarity = 0d;
        var bestNegationConflict = false;

        foreach (var existing in existingClaims)
        {
            var existingTokens = Tokenize(existing.NormalizedText);
            var similarity = Jaccard(proposalTokens, existingTokens);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestId = existing.ClaimId;
                bestNegationConflict = HasNegationConflict(proposalTokens, existingTokens);
            }
        }

        if (bestId is null || bestSimilarity < RefinementThreshold)
            return new ClaimIdentityResolution(ClaimIdentityOutcome.New, null, normalized, bestSimilarity,
                "No sufficiently similar existing claim; registering a new proposition.");

        // Strongly overlapping wording but opposing polarity is a contradiction, not an equivalence.
        if (bestNegationConflict && bestSimilarity >= RefinementThreshold)
            return new ClaimIdentityResolution(ClaimIdentityOutcome.Contradiction, bestId, normalized, bestSimilarity,
                "High lexical overlap with opposite polarity; treated as a contradicting proposition.");

        if (bestSimilarity >= EquivalenceThreshold)
            return new ClaimIdentityResolution(ClaimIdentityOutcome.Equivalent, bestId, normalized, bestSimilarity,
                "Equivalent to an existing authoritative claim.");

        return new ClaimIdentityResolution(ClaimIdentityOutcome.Refinement, bestId, normalized, bestSimilarity,
            "Refines an existing authoritative claim.");
    }

    public string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lowered = text.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            if (char.IsLetterOrDigit(ch) || ch == '\'')
                builder.Append(ch);
            else if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch))
                builder.Append(' ');
        }

        return System.Text.RegularExpressions.Regex.Replace(builder.ToString(), "\\s+", " ").Trim();
    }

    private static HashSet<string> Tokenize(string normalized) =>
        new(normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0)
            return 1d;
        if (a.Count == 0 || b.Count == 0)
            return 0d;

        var intersection = a.Count(b.Contains);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0d : (double)intersection / union;
    }

    private static bool HasNegationConflict(HashSet<string> a, HashSet<string> b)
    {
        var aNeg = a.Any(NegationTokens.Contains);
        var bNeg = b.Any(NegationTokens.Contains);
        return aNeg != bNeg;
    }
}
