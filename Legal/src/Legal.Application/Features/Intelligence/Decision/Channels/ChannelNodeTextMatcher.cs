using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Deterministic lexical node matcher shared by decision channels. A channel that holds a piece of
// text (a verified quote, an attorney assessment, an authority holding) needs to bind it to exactly
// one node of the AUTHORITATIVE persisted hierarchy. That binding is a conservative, LLM-free token
// overlap — NO scoring, NO ranking engine — so every channel resolves node targets the same way and
// unmatched text is dropped fail-soft. POLOXI Wide2 still owns all candidate competition and outcome.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class ChannelNodeTextMatcher
{
    // Support lands on the specific proposition/factor/discriminator, never on structural containers.
    public static bool IsEvidenceBearing(HierarchyNodeDto node) => node.NodeRoleCode switch
    {
        "GROUPING" => false,
        "DIMENSION" => false,
        _ => true,
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","of","to","in","on","at","by","for","with","that","this","is","are",
        "was","were","be","been","being","as","it","its","from","not","no","but","if","then","than",
        "which","who","whom","whose","will","shall","may","must","should","would","could","can","has",
        "have","had","do","does","did","so","such","any","all","some","more","most","he","she","they",
        "his","her","their","them","there","here","into","over","under","about","between",
    };

    public static HashSet<string> Tokenize(string? text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return tokens;

        var span = text.AsSpan();
        var start = -1;
        for (var i = 0; i <= span.Length; i++)
        {
            var isWord = i < span.Length && char.IsLetterOrDigit(span[i]);
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                var word = span[start..i].ToString();
                if (word.Length > 2 && !StopWords.Contains(word))
                    tokens.Add(word);
                start = -1;
            }
        }
        return tokens;
    }

    public static int CountSharedTokens(HashSet<string> a, HashSet<string> b)
    {
        var (small, large) = a.Count <= b.Count ? (a, b) : (b, a);
        var shared = 0;
        foreach (var token in small)
            if (large.Contains(token))
                shared++;
        return shared;
    }

    // Additive synonym expansion. Tokenizes as usual, then for every surface token that the Domain Pack
    // knows as a synonym (termLookup: surface form -> canonical code) ALSO adds the canonical code as an
    // extra token. This can only ADD shared-token overlaps, never remove existing raw-overlap matches, so
    // it strictly improves recall and preserves the "never inferior" guarantee. A null/empty lookup is a
    // no-op that returns the plain token set.
    public static HashSet<string> TokenizeWithSynonyms(string? text, IReadOnlyDictionary<string, string>? termLookup)
    {
        var tokens = Tokenize(text);
        if (termLookup is null || termLookup.Count == 0 || tokens.Count == 0)
            return tokens;

        // Expand whole-surface-form terms (e.g. "rear end") and single tokens against the lookup.
        if (!string.IsNullOrWhiteSpace(text) && termLookup.TryGetValue(text.Trim(), out var phraseCanonical))
            tokens.Add(phraseCanonical);

        foreach (var token in tokens.ToArray())
            if (termLookup.TryGetValue(token, out var canonical))
                tokens.Add(canonical);

        return tokens;
    }

    // Synonym-aware node index. Identical to BuildNodeIndex but expands each node statement with the
    // pack's synonym terminology. Falls back to the plain index when termLookup is null/empty.
    public static IReadOnlyList<(HierarchyNodeDto Node, HashSet<string> Tokens)> BuildNodeIndex(
        IEnumerable<HierarchyNodeDto> nodes,
        IReadOnlyDictionary<string, string>? termLookup)
        => nodes
            .Where(IsEvidenceBearing)
            .Select(n => (Node: n, Tokens: TokenizeWithSynonyms(n.Statement, termLookup)))
            .Where(x => x.Tokens.Count > 0)
            .ToArray();

    // Pre-tokenizes the evidence-bearing nodes of an execution once so a channel can match many
    // source texts against them without re-tokenizing per lookup.
    public static IReadOnlyList<(HierarchyNodeDto Node, HashSet<string> Tokens)> BuildNodeIndex(
        IEnumerable<HierarchyNodeDto> nodes)
        => nodes
            .Where(IsEvidenceBearing)
            .Select(n => (Node: n, Tokens: Tokenize(n.Statement)))
            .Where(x => x.Tokens.Count > 0)
            .ToArray();

    // Returns the node with the greatest shared-token overlap against the source text, or null when
    // no node reaches minimumSharedTokens. Deterministic and conservative by design.
    public static HierarchyNodeDto? BestMatch(
        string? sourceText,
        IReadOnlyList<(HierarchyNodeDto Node, HashSet<string> Tokens)> nodeIndex,
        int minimumSharedTokens,
        out int overlap)
        => BestMatch(sourceText, nodeIndex, minimumSharedTokens, null, out overlap);

    // Synonym-aware overload. Expands the source text with the pack's synonym terminology before
    // matching; a null/empty lookup behaves exactly like the plain overload (additive, recall-only).
    public static HierarchyNodeDto? BestMatch(
        string? sourceText,
        IReadOnlyList<(HierarchyNodeDto Node, HashSet<string> Tokens)> nodeIndex,
        int minimumSharedTokens,
        IReadOnlyDictionary<string, string>? termLookup,
        out int overlap)
    {
        overlap = 0;
        var sourceTokens = TokenizeWithSynonyms(sourceText, termLookup);
        if (sourceTokens.Count == 0)
            return null;

        HierarchyNodeDto? bestNode = null;
        var bestOverlap = 0;
        foreach (var (node, tokens) in nodeIndex)
        {
            var shared = CountSharedTokens(sourceTokens, tokens);
            if (shared > bestOverlap)
            {
                bestOverlap = shared;
                bestNode = node;
            }
        }

        if (bestNode is null || bestOverlap < minimumSharedTokens)
            return null;

        overlap = bestOverlap;
        return bestNode;
    }

    public static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
