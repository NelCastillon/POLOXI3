using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application;

// ── Matter-corpus evidence → Wide scoring projection ───────────────────────────────────────────────
// Verified uploaded-document propositions (POLOXI.Legal_MatterFactProposition, surfaced in the Evidence
// tab via LegalMatterEvidenceGraphDto) are a SEPARATE data plane from the external authorized-search
// snippets that actually drive Candidate × Branch scoring. As a result a matter with strong, verified
// corpus evidence could still show every Decision Outcome at 0% support, because the corpus propositions
// never reached ComputeEvidenceSupport / CountAdmittedExternalEvidence.
//
// This pure, deterministic projection converts each corpus proposition into attributed
// WideExternalKnowledgeSnippet rows so BOTH evidence sources vote through the EXISTING scoring chain.
// It never performs new legal reasoning and never lowers the admission gates:
//   • SUPPORTS edge + ESTABLISHED/SUPPORTED fact state + no CONTRADICTS  → VERIFIED_SUPPORT (admitted).
//   • DISPUTED / contradicted                                           → AUTHORITY_FOUND_BUT_SUPPORT_UNCLEAR.
//   • ALLEGED / INVALIDATED / no support edge                           → UNVERIFIED (present but not counted).
// Attribution reuses the same branch-attribution convention as the external snippets (explicit BranchId
// when a branch's claim text overlaps the proposition, otherwise the row carries no BranchId and is not
// credited). Fail-soft: callers pass an empty/absent graph and get back an empty list.
internal static class WideCorpusEvidenceProjection
{
    private const string StatusVerifiedSupport = "VERIFIED_SUPPORT";
    private const string StatusAuthorityUnclear = "AUTHORITY_FOUND_BUT_SUPPORT_UNCLEAR";
    private const string StatusUnverified = "UNVERIFIED";
    private const string CorpusProvider = "MATTER_CORPUS";

    // Short, common words that must not drive branch↔proposition attribution.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","of","to","and","or","in","on","for","with","by","is","are","was","were","be",
        "been","that","this","it","as","at","from","has","have","had","not","no","but","if","then",
        "which","who","whom","whose","their","there","these","those","such","any","all","may","can",
        "shall","will","would","should","could","must","under","over","per","into","within","about",
    };

    // Project every corpus proposition into attributed snippets. One snippet is emitted per matched
    // branch; propositions that match no branch are still emitted once with a null BranchId so they are
    // visible/auditable without being credited to an unrelated branch.
    public static List<WideExternalKnowledgeSnippet> Project(
        LegalMatterEvidenceGraphDto? graph,
        IReadOnlyCollection<WideBranchRecord> branches,
        DateTime nowUtc)
    {
        var result = new List<WideExternalKnowledgeSnippet>();
        if (graph is null || graph.Propositions is null || graph.Propositions.Count == 0)
            return result;

        var branchTokens = branches
            .Select(branch => (branch, tokens: Tokenize($"{branch.DisplayName} {branch.Interpretation}")))
            .ToArray();

        foreach (var proposition in graph.Propositions)
        {
            if (string.IsNullOrWhiteSpace(proposition.PropositionText))
                continue;

            var support = proposition.Support ?? [];
            var hasSupportEdge = support.Any(edge =>
                string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase));
            var hasContradiction = support.Any(edge =>
                string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Contradicts, StringComparison.OrdinalIgnoreCase));

            var (status, supportScore) = ClassifyAdmission(proposition, hasSupportEdge, hasContradiction);

            var propositionTokens = Tokenize(proposition.PropositionText);
            var matched = false;

            foreach (var (branch, tokens) in branchTokens)
            {
                var overlap = OverlapRatio(propositionTokens, tokens);
                if (overlap <= 0m)
                    continue;

                matched = true;
                // Weight the proposition's intrinsic admission score by claim↔branch overlap so an
                // on-point proposition contributes more than a tangential one. Never exceeds the
                // intrinsic score, so presence alone can never inflate support.
                var branchSupportScore = Math.Clamp(supportScore * overlap, 0m, 1m);
                result.Add(BuildSnippet(proposition, branch.WideBranchId, branch.DisplayName, status, branchSupportScore, nowUtc));
            }

            if (!matched)
                result.Add(BuildSnippet(proposition, branchId: null, branchDisplayName: null, status, supportScore: 0m, nowUtc));
        }

        return result;
    }

    // Map a corpus proposition's fact state and support edges onto the external-snippet admission
    // vocabulary plus an intrinsic 0..1 support score. Mirrors MatterPropositionClaimProjection's
    // deterministic reading of FactStateCode / SUPPORTS / CONTRADICTS without re-scoring.
    private static (string status, decimal supportScore) ClassifyAdmission(
        LegalEvidenceGraphPropositionDto proposition, bool hasSupportEdge, bool hasContradiction)
    {
        var confidence = Math.Clamp(proposition.Confidence ?? 0.5m, 0m, 1m);
        var factState = proposition.FactStateCode?.Trim().ToUpperInvariant();
        var authoritativeBoost = proposition.IsDecisionAuthoritative ? 0.15m : 0m;

        var established = factState is LegalFactStates.Established or LegalFactStates.Supported;
        if (established && hasSupportEdge && !hasContradiction)
            return (StatusVerifiedSupport, Math.Clamp(confidence + authoritativeBoost, 0m, 1m));

        if (hasContradiction || factState == LegalFactStates.Disputed)
            return (StatusAuthorityUnclear, Math.Clamp(confidence * 0.5m, 0m, 1m));

        // ALLEGED / INVALIDATED / no supporting evidence: present for audit, never credited as support.
        return (StatusUnverified, 0m);
    }

    private static WideExternalKnowledgeSnippet BuildSnippet(
        LegalEvidenceGraphPropositionDto proposition,
        Guid? branchId,
        string? branchDisplayName,
        string status,
        decimal supportScore,
        DateTime nowUtc)
    {
        var admitted = string.Equals(status, StatusVerifiedSupport, StringComparison.Ordinal);
        return new WideExternalKnowledgeSnippet
        {
            // Query is the attribution fallback channel for rows without a BranchId; when a branch is
            // matched, BranchId is authoritative and Query simply mirrors the branch name.
            Query = branchDisplayName ?? proposition.PropositionText,
            Title = "Matter evidence",
            // Stable, de-dup-friendly identity for the corpus proposition (+ branch) pair.
            Url = $"matter-corpus://proposition/{proposition.LegalFactPropositionId:N}" + (branchId is { } id ? $"#{id:N}" : string.Empty),
            Snippet = proposition.PropositionText,
            Score = 0m,
            RetrievedDateUtc = nowUtc,
            BranchId = branchId,
            BranchDisplayName = branchDisplayName,
            SourceProvider = CorpusProvider,
            // Corpus propositions are derived from uploaded documents already admitted into the matter,
            // so their provider/identity is verified by construction; the admission STATUS below still
            // governs whether a proposition is counted as support.
            ProviderIdentityVerified = true,
            AuthorityIdentityVerified = admitted,
            PropositionSupportScore = supportScore,
            PropositionSupportStatus = status,
        };
    }

    private static IReadOnlyCollection<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split([' ', '\t', '\n', '\r', ',', '.', ';', ':', '(', ')', '[', ']', '"', '\'', '/', '-', '\u2019'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length < 3)
                continue;
            if (StopWords.Contains(raw))
                continue;
            tokens.Add(raw.ToLowerInvariant());
        }
        return tokens;
    }

    // Fraction of the branch's significant tokens that also appear in the proposition. Using the branch
    // token count as the denominator keeps short, specific branch names from being diluted by long
    // proposition text while still requiring genuine overlap.
    private static decimal OverlapRatio(IReadOnlyCollection<string> propositionTokens, IReadOnlyCollection<string> branchTokens)
    {
        if (propositionTokens.Count == 0 || branchTokens.Count == 0)
            return 0m;
        var shared = branchTokens.Count(token => propositionTokens.Contains(token));
        if (shared == 0)
            return 0m;
        return Math.Clamp((decimal)shared / branchTokens.Count, 0m, 1m);
    }
}
