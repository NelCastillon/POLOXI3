namespace Legal.Application.Features.Intelligence.Decision.Core;

// Deterministic, side-effect-free normalizer applied to a parsed research-need proposal BEFORE the
// Researchability Gate evaluates it. Its single, bounded job is to self-heal the recoverable retrieval
// fields (SearchQuery / SearchConcepts) of researchable MATTER-DOCUMENT leaves whose factual proposition is
// otherwise valid but that omitted a concrete document-search expression.
//
// WHY THIS EXISTS
//   A weaker producer (e.g. MINI) frequently emits a valid MATTER_FACT / MATTER_EVIDENCE proposition but
//   forgets its SearchQuery/SearchConcepts. Without this step those leaves raise SEARCH_QUERY_MISSING /
//   SEARCH_CONCEPTS_MISSING defects that the gate treats as BLOCKING, which prevents an independently valid
//   LEGAL_RULE leaf from ever reaching retrieval and collapses the whole run to RESEARCH_NEED_UNRESOLVED.
//
// WHAT IT IS NOT
//   The derived SearchQuery is a PROPOSED retrieval expression only — it targets the matter corpus. It is
//   NEVER evidence that any matching document exists, and it never invents witness statements, expert
//   testimony, or reconstruction reports. If the matter corpus is empty the leaf simply retrieves nothing
//   and retains its proper unresolved status downstream. The gate is not weakened: only matter leaves that
//   already carry a valid declarative proposition are healed, and legal-authority leaves are untouched.
public static class DecisionResearchNeedContractNormalizer
{
    // Common English stopwords plus a few discourse fillers stripped from a proposition so the derived
    // query keeps only content-bearing retrieval terms. Kept small and deterministic on purpose.
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "if", "then", "than", "that", "this", "these", "those",
        "is", "are", "was", "were", "be", "been", "being", "am", "to", "of", "in", "on", "at", "by",
        "for", "with", "about", "as", "into", "over", "from", "up", "down", "out", "off", "it", "its",
        "their", "there", "here", "which", "who", "whom", "whose", "what", "when", "where", "why", "how",
        "whether", "do", "does", "did", "has", "have", "had", "will", "would", "should", "could", "may",
        "might", "must", "can", "shall", "not", "no", "so", "such", "any", "all", "each", "some", "more",
        "most", "other", "one", "two", "both", "he", "she", "they", "we", "you", "i", "his", "her",
        "them", "our", "your", "my",
    };

    private const int MaxQueryTerms = 8;
    private const int MaxConcepts = 4;

    // Returns a proposal in which every recoverable MATTER-DOCUMENT leaf carries a deterministic SearchQuery
    // and at least one SearchConcept derived from its proposition. Non-matter leaves, non-researchable
    // leaves, and matter leaves lacking a usable proposition are returned unchanged.
    public static DecisionResearchSemanticProposal Normalize(DecisionResearchSemanticProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.Leaves.Count == 0)
            return proposal;

        var healed = false;
        var leaves = new List<DecisionResearchSemanticLeaf>(proposal.Leaves.Count);
        foreach (var leaf in proposal.Leaves)
        {
            var normalized = NormalizeLeaf(leaf);
            if (!ReferenceEquals(normalized, leaf))
                healed = true;
            leaves.Add(normalized);
        }

        return healed ? proposal with { Leaves = leaves } : proposal;
    }

    private static DecisionResearchSemanticLeaf NormalizeLeaf(DecisionResearchSemanticLeaf leaf)
    {
        // Only researchable matter-document leaves are eligible for retrieval-field self-healing.
        if (!leaf.Researchable)
            return leaf;
        if (!leaf.SourceClass.Equals(DecisionResearchSourceClasses.MatterDocument, StringComparison.OrdinalIgnoreCase))
            return leaf;
        if (!DecisionResearchNeedTypes.RequiresMatterSources(leaf.ResearchNeedType))
            return leaf;

        var needsQuery = string.IsNullOrWhiteSpace(leaf.SearchQuery);
        var needsConcepts = leaf.SearchConcepts.Count == 0;
        if (!needsQuery && !needsConcepts)
            return leaf;

        // Derive terms deterministically from the leaf's factual proposition. If the proposition yields no
        // content-bearing terms there is nothing safe to synthesize — leave the leaf as-is so the gate can
        // still reject it rather than fabricating a meaningless query.
        var terms = ExtractTerms(leaf.Proposition);
        if (terms.Count == 0)
            return leaf;

        var query = needsQuery ? string.Join(' ', terms.Take(MaxQueryTerms)) : leaf.SearchQuery;
        var concepts = needsConcepts ? BuildConcepts(terms) : leaf.SearchConcepts;

        return leaf with
        {
            SearchQuery = query,
            SearchConcepts = concepts,
        };
    }

    // Splits a proposition into ordered, de-duplicated, content-bearing lowercase terms.
    private static List<string> ExtractTerms(string? proposition)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(proposition))
            return terms;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in proposition.Split(
            [' ', '\t', '\n', '\r', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'', '/', '\\', '?', '!'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw.Trim('-');
            if (token.Length < 3)
                continue;
            if (Stopwords.Contains(token))
                continue;
            if (!seen.Add(token))
                continue;
            terms.Add(token.ToLowerInvariant());
        }

        return terms;
    }

    // Groups the leading terms into a few short, distinct concept phrases for the retrieval planner.
    private static IReadOnlyList<string> BuildConcepts(List<string> terms)
    {
        var concepts = new List<string>(MaxConcepts);
        for (var i = 0; i < terms.Count && concepts.Count < MaxConcepts; i += 2)
        {
            var phrase = i + 1 < terms.Count ? $"{terms[i]} {terms[i + 1]}" : terms[i];
            concepts.Add(phrase);
        }

        return concepts;
    }
}
