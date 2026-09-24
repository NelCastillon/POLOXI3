using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;

namespace Legal.Application;

// ── Phase 1 extraction (verbatim move; zero behavior change) ───────────────────────────────────
// Deterministic, zero-LLM legal-authority parsing, identity verification, and proposition-support
// scoring. Moved out of IntelligenceWide2Service.cs unchanged to shrink the orchestration monolith.
// All members are pure statics; being a partial class, cross-references to the rest of the service
// (NormalizeQuery, RetrievalQueryStopwords, LegalAuthorityKind, DTOs) resolve identically.
public sealed partial class IntelligenceWide2Service
{
    // A specific legal authority parsed from branch text, with the source it should be routed to and
    // the distinctive tokens a retrieved source must contain to verify it is actually that authority.
    private readonly record struct LegalAuthorityReference(string Query,LegalAuthorityKind Kind,IReadOnlyList<string> VerificationTokens);

    // Matches reported case names of the form "Party v Party" / "Party v. Party", capturing multi-word
    // party names (e.g. "Konic International Corp. v. Spokane Computer Services"). Deterministic and
    // fail-soft: yields nothing when no citation is present.
    private static readonly System.Text.RegularExpressions.Regex LegalCaseCitationRegex=new(
        @"\b[A-Z][A-Za-z.&'\u2019\-]+(?:\s+[A-Z][A-Za-z0-9.&'\u2019\-]+){0,5}\s+v\.?\s+[A-Z][A-Za-z.&'\u2019\-]+(?:\s+[A-Za-z0-9.&'\u2019\-]+){0,5}",
        System.Text.RegularExpressions.RegexOptions.Compiled|System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    // Matches U.S.C. citations and Public Law numbers (legislative material → GovInfo).
    private static readonly System.Text.RegularExpressions.Regex LegalStatuteCitationRegex=new(
        @"\b(?:\d+\s+U\.?\s?S\.?\s?C\.?\s+(?:§+\s*)?\d[\w.\-]*|Pub(?:lic)?\.?\s+L(?:aw)?\.?\s+(?:No\.?\s*)?\d+[\-\u2013]\d+)",
        System.Text.RegularExpressions.RegexOptions.Compiled|System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    // Matches C.F.R. citations (regulatory material → GovInfo/eCFR).
    private static readonly System.Text.RegularExpressions.Regex LegalRegulationCitationRegex=new(
        @"\b\d+\s+C\.?\s?F\.?\s?R\.?\s+(?:§+\s*)?\d[\w.\-]*",
        System.Text.RegularExpressions.RegexOptions.Compiled|System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    // Generic corporate/legal-entity tokens that never distinguish one party from another; excluded
    // from case verification tokens so matching relies on the distinctive party names.
    private static readonly HashSet<string> CaseTokenStopwords=new(StringComparer.OrdinalIgnoreCase)
    {
        "corp","corporation","inc","incorporated","llc","company","co","ltd","limited","the","and"
    };

    // Extracts the explicit legal authorities a branch already cites (case names, U.S.C./Public Law,
    // and C.F.R. citations), classified so each can be routed to the correct source and verified. No
    // additional LLM call — extraction is deterministic. Returns up to three authorities per branch.
    private static IReadOnlyList<LegalAuthorityReference> ExtractLegalAuthorities(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return [];
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var authorities=new List<LegalAuthorityReference>();
        void Add(string raw,LegalAuthorityKind kind,IReadOnlyList<string> tokens)
        {
            var name=NormalizeQuery(raw);
            if(name.Length<3||tokens.Count==0||!seen.Add(name))return;
            authorities.Add(new(name,kind,tokens));
        }
        foreach(System.Text.RegularExpressions.Match match in LegalCaseCitationRegex.Matches(text))
            Add(match.Value,LegalAuthorityKind.Case,CaseVerificationTokens(match.Value));
        foreach(System.Text.RegularExpressions.Match match in LegalStatuteCitationRegex.Matches(text))
            Add(match.Value,LegalAuthorityKind.Statute,CitationVerificationTokens(match.Value));
        foreach(System.Text.RegularExpressions.Match match in LegalRegulationCitationRegex.Matches(text))
            Add(match.Value,LegalAuthorityKind.Regulation,CitationVerificationTokens(match.Value));
        return authorities.Take(3).ToArray();
    }

    // Concept fallback bridge: when a legal branch names no explicit citation, map its decisive doctrine
    // to concrete UCC/U.S. Code citations using the DB-backed concept map (POLOXI.Legal_LegalConceptAuthority).
    // Deterministic AND-match: every keyword in a concept row must appear in the branch text. The emitted
    // citations flow through the SAME retrieval + mandatory identity gate, so a wrong mapping simply fails
    // verification and is dropped (never inflates confidence). Returns up to three authorities.
    private static IReadOnlyList<LegalAuthorityReference> ResolveConceptAuthorities(string text,IReadOnlyCollection<WideLegalConceptAuthorityDto> conceptMap)
    {
        if(string.IsNullOrWhiteSpace(text)||conceptMap.Count==0)return [];
        var haystack=NormalizeQuery(text).ToLowerInvariant();
        if(haystack.Length<3)return [];
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var authorities=new List<LegalAuthorityReference>();
        foreach(var concept in conceptMap)
        {
            var keywords=(concept.ConceptKeywords??string.Empty).Split([' ','\t',','],StringSplitOptions.RemoveEmptyEntries);
            if(keywords.Length==0||!keywords.All(keyword=>ContainsWord(haystack,keyword.ToLowerInvariant())))continue;
            // Context-anchor gate: when a concept declares domain-context tokens, at least one must ALSO
            // appear in the text before it resolves. This stops generic doctrine words (e.g. "cure",
            // "waiver", "good faith") from matching commercial statutes on unrelated (family, medical,
            // constitutional) questions. Empty anchors preserve the original keyword-only behavior.
            var anchors=(concept.ContextAnchors??string.Empty).Split([',',';'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if(anchors.Length>0&&!anchors.Any(anchor=>ContainsWord(haystack,anchor.ToLowerInvariant())))continue;
            var citation=NormalizeQuery(concept.CitationText);
            if(citation.Length<3||!seen.Add(citation))continue;
            var tokens=(concept.VerificationTokens??string.Empty).Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(token=>token.Length>=1).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(tokens.Length==0)tokens=CitationVerificationTokens(concept.CitationText).ToArray();
            if(tokens.Length==0)continue;
            var kind=Enum.TryParse<LegalAuthorityKind>(concept.AuthorityKindCode,ignoreCase:true,out var parsed)?parsed:LegalAuthorityKind.Statute;
            authorities.Add(new(citation,kind,tokens));
            if(authorities.Count>=3)break;
        }
        return authorities;
    }

    // Word-boundary containment over a space-normalized, lowercased haystack. A keyword matches only when
    // it appears as a whole word (or whole multi-word phrase), so "cure" no longer matches "secure" and a
    // commercial doctrine cannot latch onto an unrelated substring. Multi-word keywords match if all their
    // sub-tokens appear as whole words (order-independent), preserving the existing AND-phrase behavior.
    private static bool ContainsWord(string haystack,string keyword)
    {
        if(string.IsNullOrWhiteSpace(keyword))return false;
        foreach(var token in keyword.Split([' ','\t'],StringSplitOptions.RemoveEmptyEntries))
        {
            var index=0;
            var found=false;
            while((index=haystack.IndexOf(token,index,StringComparison.Ordinal))>=0)
            {
                var beforeOk=index==0||!char.IsLetterOrDigit(haystack[index-1]);
                var after=index+token.Length;
                var afterOk=after>=haystack.Length||!char.IsLetterOrDigit(haystack[after]);
                if(beforeOk&&afterOk){found=true;break;}
                index=after;
            }
            if(!found)return false;
        }
        return true;
    }

    private static IReadOnlyList<string> CaseVerificationTokens(string caseName)
    {
        var tokens=new List<string>();
        foreach(var raw in caseName.Split([' ','.',',','\'','\u2019','-'],StringSplitOptions.RemoveEmptyEntries))
        {
            var token=raw.Trim();
            if(token.Length<4||string.Equals(token,"v",StringComparison.OrdinalIgnoreCase)||CaseTokenStopwords.Contains(token))continue;
            tokens.Add(token);
        }
        return tokens;
    }

    private static IReadOnlyList<string> CitationVerificationTokens(string citation)=>
        System.Text.RegularExpressions.Regex.Matches(citation,@"\d+").Select(match=>match.Value).Where(value=>value.Length>=2).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    // Verification gate: an authority proposed by the LLM is only evidence once a retrieved source
    // actually refers to it. The single-snippet identity check below is the mandatory gate applied
    // during retrieval before proposition-support scoring.
    // Identity check for a single snippet: does the retrieved source actually reference the proposed
    // authority (at least one distinctive token in the title/snippet)? This is the MANDATORY gate.
    private static bool SnippetMatchesAuthorityIdentity(WideExternalKnowledgeSnippet snippet,LegalAuthorityReference authority)
    {
        if(authority.VerificationTokens.Count==0)return false;
        // Normalize both sides so ordinary legal-name formatting (punctuation, "v." vs "v", extra
        // whitespace, casing) never causes a false negative: "Raffles v. Wichelhaus",
        // "Raffles v Wichelhaus", and "Raffles versus Wichelhaus" all resolve identically.
        var haystack=NormalizeIdentityText($"{snippet.Title} {snippet.Snippet}");
        return authority.VerificationTokens.Any(token=>haystack.Contains(NormalizeIdentityText(token),StringComparison.Ordinal));
    }

    // Identity normalization: lowercase, replace any non-alphanumeric run with a single space, and trim.
    // This makes distinctive-token matching resilient to citation punctuation without weakening identity
    // (the distinctive party/citation tokens themselves must still be present).
    private static string NormalizeIdentityText(string text)=>
        string.IsNullOrWhiteSpace(text)?string.Empty:System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(),@"[^a-z0-9]+"," ").Trim();

    // Tri-state proposition-support status. Deterministic, no LLM. A weighting signal, not legal truth.
    private const string PropositionStatusVerifiedSupport="VERIFIED_SUPPORT";
    private const string PropositionStatusUnclear="AUTHORITY_FOUND_BUT_SUPPORT_UNCLEAR";
    private const string PropositionStatusUnverified="UNVERIFIED";
    // Overlap at/above this share of branch-claim concept tokens counts as strong proposition support.
    private const decimal PropositionSupportThreshold=0.34m;

    // Deterministic proposition-support score in [0,1]: weighted overlap between the branch claim's
    // distinctive concept tokens and the retrieved snippet text. Normalizes case, strips stopwords, and
    // rewards longer (more distinctive) claim terms. This does NOT establish legal truth — it only
    // measures how much the source text talks about the same concepts the branch claim asserts, and is
    // used to weight (never to gate) evidence contribution. Identity verification remains the gate.
    private static decimal ComputePropositionSupport(string branchClaim,string snippetText)
    {
        var claimTokens=PropositionConceptTokens(branchClaim);
        if(claimTokens.Count==0||string.IsNullOrWhiteSpace(snippetText))return 0m;
        var haystackTokens=PropositionConceptTokens(snippetText).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(haystackTokens.Count==0)return 0m;
        decimal matchedWeight=0m,totalWeight=0m;
        foreach(var token in claimTokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // Longer terms are more distinctive; weight them more heavily than short common words.
            var weight=token.Length>=8?3m:token.Length>=6?2m:1m;
            totalWeight+=weight;
            if(haystackTokens.Contains(token))matchedWeight+=weight;
        }
        return totalWeight==0m?0m:Math.Clamp(matchedWeight/totalWeight,0m,1m);
    }

    // Concept tokens for proposition overlap: lowercase alphanumeric words >=4 chars that are not
    // conversational stopwords. Legal phrasing (mutual, assent, meanings, peerless, contract, ...) is
    // preserved; filler (the, with, would, ...) is dropped.
    private static IReadOnlyList<string> PropositionConceptTokens(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return [];
        return System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(),@"[a-z0-9]{4,}")
            .Select(match=>match.Value)
            .Where(token=>!RetrievalQueryStopwords.Contains(token))
            .ToArray();
    }

    // Classifies a single legal snippet into the tri-state status. Identity failure is terminal:
    // proposition scoring can never rescue an authority that was not actually retrieved.
    private static (bool IdentityVerified,decimal SupportScore,string Status) ClassifyLegalSnippet(WideExternalKnowledgeSnippet snippet,LegalAuthorityReference authority,string branchClaim)
    {
        if(!SnippetMatchesAuthorityIdentity(snippet,authority))return (false,0m,PropositionStatusUnverified);
        var support=ComputePropositionSupport(branchClaim,$"{snippet.Title} {snippet.Snippet}");
        var status=support>=PropositionSupportThreshold?PropositionStatusVerifiedSupport:PropositionStatusUnclear;
        return (true,support,status);
    }
}

