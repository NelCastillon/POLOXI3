using System.Text;
using System.Text.RegularExpressions;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Domain Pack mode — "is this catalog evidence type PRESENT in the matter?"
//
// The pack evidence catalog (~340 PI rows) is configuration, not matter evidence. Deterministic L2
// must only materialize the evidence types the matter actually exhibits, otherwise every catalog row
// competes in scoring regardless of relevance. Presence is decided deterministically from the
// matter's own signals — ALL matter fields (incl. Description), ALL PI profile fields and child
// entries, the uploaded-document evidence graph, retrieval evidence, and the original question —
// matched against the evidence type's Code, Name AND catalog Description.
//
// Two admission routes (either suffices):
//   1. DIRECT — a document evidence item already classified with the same EvidenceTypeCode.
//   2. LEXICAL — the evidence type's Name tokens all appear in the corpus, OR at least two distinct
//      significant terms from its catalog Description appear.
//
// Pure, side-effect-free and unit-testable. Performs NO inference and never mutates the catalog;
// unmatched rows simply do not become L2 nodes for this run (they stay in the DB for gap analysis).
// ─────────────────────────────────────────────────────────────────────────────────────────────
public static class DomainPackEvidencePresenceMatcher
{
    private const int MinimumTermLength = 4;
    private const int MinimumDescriptionTermHits = 2;

    private static readonly Regex TokenPattern = new(@"[a-z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Generic catalog vocabulary that appears in most descriptions and would otherwise make every
    // row match ("records", "report", "evidence", "documentation" …).
    private static readonly HashSet<string> GenericTerms = new(StringComparer.Ordinal)
    {
        "record","records","report","reports","document","documents","documentation","evidence",
        "form","forms","file","files","letter","letters","statement","statements","notes","note",
        "information","data","copy","copies","related","relating","regarding","including","includes",
        "include","used","using","from","with","that","this","these","those","other","such","than",
        "into","over","under","between","about","after","before","which","their","there","where",
        "when","what","also","each","both","either","party","parties","plaintiff","defendant",
        "claimant","insured","matter","case","claim","claims","injury","injuries","accident",
        "incident","medical","legal","personal",
    };

    public static IReadOnlyCollection<string> NormalizeCorpusTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        return TokenPattern.Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length >= MinimumTermLength)
            .Select(Stem)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static bool IsPresent(
        DecisionDomainPackEvidenceTypeDto evidenceType,
        IReadOnlySet<string> corpusTokens,
        IReadOnlySet<string> directEvidenceTypeCodes)
    {
        ArgumentNullException.ThrowIfNull(evidenceType);
        if (directEvidenceTypeCodes.Contains(evidenceType.EvidenceTypeCode))
            return true;
        if (corpusTokens.Count == 0)
            return false;

        var nameTerms = SignificantTerms(evidenceType.Name, allowGeneric: true)
            .Concat(SignificantTerms(evidenceType.EvidenceTypeCode.Replace('_', ' '), allowGeneric: true))
            .ToHashSet(StringComparer.Ordinal);
        var specificNameTerms = nameTerms.Where(t => !GenericTerms.Contains(t)).ToArray();
        // Name route: every SPECIFIC name term present (e.g. "surveillance" for Surveillance Video).
        // Names composed only of generic vocabulary ("Medical Records") need every term present.
        var nameRoute = specificNameTerms.Length > 0
            ? specificNameTerms.All(corpusTokens.Contains)
            : nameTerms.Count > 0 && nameTerms.All(corpusTokens.Contains);
        if (nameRoute)
            return true;

        var descriptionHits = SignificantTerms(evidenceType.Description, allowGeneric: false)
            .Count(corpusTokens.Contains);
        return descriptionHits >= MinimumDescriptionTermHits;
    }

    public static string BuildCorpus(params IEnumerable<string?>[] sources)
    {
        var sb = new StringBuilder();
        foreach (var source in sources)
            foreach (var text in source)
                if (!string.IsNullOrWhiteSpace(text))
                    sb.Append(text).Append('\n');
        return sb.ToString();
    }

    private static IEnumerable<string> SignificantTerms(string? text, bool allowGeneric)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        return TokenPattern.Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length >= MinimumTermLength)
            .Where(t => allowGeneric || !GenericTerms.Contains(t))
            .Select(Stem)
            .Distinct(StringComparer.Ordinal);
    }

    // Light suffix stem so "records"/"record", "statements"/"statement", "imaging"/"image" align.
    private static string Stem(string token)
    {
        if (token.Length > 5 && token.EndsWith("ing", StringComparison.Ordinal)) return token[..^3];
        if (token.Length > 4 && token.EndsWith("ies", StringComparison.Ordinal)) return token[..^3] + "y";
        if (token.Length > 4 && token.EndsWith("es", StringComparison.Ordinal)) return token[..^2];
        if (token.Length > 4 && token.EndsWith("s", StringComparison.Ordinal)) return token[..^1];
        return token;
    }
}
