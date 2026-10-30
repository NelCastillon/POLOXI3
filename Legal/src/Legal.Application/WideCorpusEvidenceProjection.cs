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
        DateTime nowUtc,
        decimal minOverlap=0m,
        Func<Guid,Guid,decimal?>? semanticSimilarity=null,
        decimal minSemantic=1m)
    {
        // Branch is credited only when the proposition shares at least this fraction of the branch's
        // significant tokens (DB-seeded WideConfiguration.CorpusBranchMinOverlap). Clamped defensively.
        minOverlap=Math.Clamp(minOverlap,0m,1m);
        // Optional semantic gate (DB-seeded, OFF by default). When no lookup is supplied the gate is
        // unreachable (minSemantic clamped to 1 and similarity null), so behavior matches lexical-only.
        minSemantic=Math.Clamp(minSemantic,0m,1m);
        var result = new List<WideExternalKnowledgeSnippet>();
        if (graph is null || graph.Propositions is null || graph.Propositions.Count == 0)
            return result;

        // Map each supporting evidence item to the ACTUAL uploaded document version (+ file name) so a
        // corpus snippet can deep-link to the real source document instead of the non-navigable
        // matter-corpus:// identity Url. Empty when the graph carries no evidence rows.
        var documentByItemId = new Dictionary<Guid, (Guid VersionId, string FileName)>();
        if (graph.Evidence is not null)
        {
            foreach (var item in graph.Evidence)
            {
                if (item.LegalDocumentVersionId != Guid.Empty)
                    documentByItemId[item.LegalEvidenceItemId] = (item.LegalDocumentVersionId, item.DocumentFileName);
            }
        }

        // Source assertions pin a real document version directly onto an evidence item AND/OR a
        // proposition-support edge, so they resolve the backing document for propositions whose SUPPORTS
        // edge doesn't map through graph.Evidence (the exact gap behind "View matter data"-only rows).
        var (documentBySupportId, assertionDocumentByItemId) = BuildAssertionDocumentIndex(graph);

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

            // Resolve the backing source document from the proposition's SUPPORTS edges (prefer a
            // SUPPORTS edge; otherwise accept any edge that resolves). Stays null when no supporting
            // evidence item maps to a stored document version, so the snippet keeps its identity Url.
            (Guid VersionId, string FileName)? sourceDocument = null;
            foreach (var edge in support)
            {
                if (documentByItemId.TryGetValue(edge.LegalEvidenceItemId, out var doc))
                {
                    sourceDocument = doc;
                    if (string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase))
                        break;
                }
            }

            // Fallback: resolve the backing document through source assertions (pinned to a support edge
            // or to the evidence item) when the SUPPORTS edge didn't map through graph.Evidence.
            sourceDocument ??= ResolveFromAssertions(support, documentBySupportId, assertionDocumentByItemId);

            var propositionTokens = Tokenize(proposition.PropositionText);
            var matched = false;

            foreach (var (branch, tokens) in branchTokens)
            {
                var overlap = OverlapRatio(propositionTokens, tokens);
                var lexicalPass = overlap > 0m && overlap >= minOverlap;
                // Semantic path (OR): attach when the embedding cosine clears its threshold even if the
                // lexical overlap did not, so a paraphrased proposition the token gate misses still scores.
                var semantic = semanticSimilarity?.Invoke(proposition.LegalFactPropositionId, branch.WideBranchId);
                var semanticPass = semantic is { } sim && sim >= minSemantic;
                if (!lexicalPass && !semanticPass)
                    continue;

                matched = true;
                // Weight the proposition's intrinsic admission score by the STRONGER of claim/branch
                // overlap and semantic similarity so an on-point proposition contributes more than a
                // tangential one. Never exceeds the intrinsic score, so presence (lexical OR semantic)
                // can never inflate support beyond what ClassifyAdmission already allowed.
                var relevance = Math.Max(overlap, semantic ?? 0m);
                var branchSupportScore = Math.Clamp(supportScore * relevance, 0m, 1m);
                result.Add(BuildSnippet(proposition, branch.WideBranchId, branch.DisplayName, status, branchSupportScore, nowUtc, sourceDocument));
            }

            if (!matched)
                result.Add(BuildSnippet(proposition, branchId: null, branchDisplayName: null, status, supportScore: 0m, nowUtc, sourceDocument));
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

    // Default entity-type code when an admitted proposition's supporting evidence item carries no
    // domain EvidenceTypeCode. Keeps persisted Legal_WideRunEvidence document rows distinguishable
    // from MATTER_FACT rows even when the corpus item type is unknown.
    private const string DefaultDocumentEntityTypeCode = "DOCUMENT_EVIDENCE";
    private const string DocumentModuleCode = "LEGAL_DOCUMENT";

    // Project ADMITTED (VERIFIED_SUPPORT) corpus propositions into branch-attributed evidence rows so
    // the uploaded-document evidence is PERSISTED into Legal_WideRunEvidence with its real document
    // EvidenceTypeCode — not only matter facts (MATTER_FACT/LEGAL_MATTER). Mirrors the admission gate and
    // branch-attribution convention used by Project(...): only propositions that are established + have a
    // SUPPORTS edge + no contradiction and that overlap a branch are credited. Unmatched or unverified
    // propositions are never emitted here (they remain auditable via the external-snippet projection).
    public static List<PoloxiEvidenceDto> ProjectEvidence(
        LegalMatterEvidenceGraphDto? graph,
        IReadOnlyCollection<WideBranchRecord> branches,
        Guid matterId,
        decimal minOverlap=0m,
        Func<Guid,Guid,decimal?>? semanticSimilarity=null,
        decimal minSemantic=1m)
    {
        minOverlap=Math.Clamp(minOverlap,0m,1m);
        minSemantic=Math.Clamp(minSemantic,0m,1m);
        var result = new List<PoloxiEvidenceDto>();
        if (graph is null || graph.Propositions is null || graph.Propositions.Count == 0 || branches.Count == 0)
            return result;

        // Map each supporting evidence item to its domain EvidenceTypeCode so admitted propositions can
        // carry the real document evidence type instead of a generic placeholder.
        var evidenceTypeByItemId = new Dictionary<Guid, string>();
        // Map each supporting evidence item to the ACTUAL uploaded document version (+ file name) so the
        // evidence row can deep-link to the real source document via the authenticated content endpoint.
        var documentByItemId = new Dictionary<Guid, (Guid VersionId, string FileName)>();
        if (graph.Evidence is not null)
        {
            foreach (var item in graph.Evidence)
            {
                if (!string.IsNullOrWhiteSpace(item.EvidenceTypeCode))
                    evidenceTypeByItemId[item.LegalEvidenceItemId] = item.EvidenceTypeCode.Trim();
                if (item.LegalDocumentVersionId != Guid.Empty)
                    documentByItemId[item.LegalEvidenceItemId] = (item.LegalDocumentVersionId, item.DocumentFileName);
            }
        }

        var (documentBySupportId, assertionDocumentByItemId) = BuildAssertionDocumentIndex(graph);

        var branchTokens = branches
            .Select(branch => (branch, tokens: Tokenize($"{branch.DisplayName} {branch.Interpretation}")))
            .ToArray();

        var rank = 0;
        foreach (var proposition in graph.Propositions)
        {
            if (string.IsNullOrWhiteSpace(proposition.PropositionText))
                continue;

            var support = proposition.Support ?? [];
            var hasSupportEdge = support.Any(edge =>
                string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase));
            var hasContradiction = support.Any(edge =>
                string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Contradicts, StringComparison.OrdinalIgnoreCase));

            var (status, _) = ClassifyAdmission(proposition, hasSupportEdge, hasContradiction);
            // Admission gate is never lowered: only VERIFIED_SUPPORT propositions are persisted as evidence.
            if (!string.Equals(status, StatusVerifiedSupport, StringComparison.Ordinal))
                continue;

            // Resolve the document evidence-type code AND the backing source document from the
            // proposition's SUPPORTS edges.
            var entityTypeCode = DefaultDocumentEntityTypeCode;
            (Guid VersionId, string FileName)? sourceDocument = null;
            foreach (var edge in support)
            {
                if (!string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (evidenceTypeByItemId.TryGetValue(edge.LegalEvidenceItemId, out var typeCode))
                    entityTypeCode = typeCode;
                if (sourceDocument is null && documentByItemId.TryGetValue(edge.LegalEvidenceItemId, out var doc))
                    sourceDocument = doc;
                if (sourceDocument is not null && !string.Equals(entityTypeCode, DefaultDocumentEntityTypeCode, StringComparison.Ordinal))
                    break;
            }

            // Fallback: resolve the backing document through source assertions when no SUPPORTS edge
            // mapped through graph.Evidence.
            sourceDocument ??= ResolveFromAssertions(support, documentBySupportId, assertionDocumentByItemId);

            var propositionTokens = Tokenize(proposition.PropositionText);
            foreach (var (branch, tokens) in branchTokens)
            {
                var overlap = OverlapRatio(propositionTokens, tokens);
                var lexicalPass = overlap > 0m && overlap >= minOverlap;
                var semantic = semanticSimilarity?.Invoke(proposition.LegalFactPropositionId, branch.WideBranchId);
                var semanticPass = semantic is { } sim && sim >= minSemantic;
                if (!lexicalPass && !semanticPass)
                    continue;
                result.Add(new PoloxiEvidenceDto(
                    HierarchyBranchId: branch.WideBranchId,
                    SearchDocumentId: Guid.Empty,
                    EntityTypeCode: entityTypeCode,
                    EntityId: proposition.LegalFactPropositionId,
                    ModuleCode: DocumentModuleCode,
                    Title: entityTypeCode,
                    Excerpt: proposition.PropositionText,
                    NavigationRoute: null,
                    RelevanceScore: 1m,
                    RankNumber: ++rank,
                    MatchedBranches: [branch.DisplayName])
                {
                    LegalDocumentVersionId = sourceDocument?.VersionId,
                    LegalDocumentFileName = sourceDocument?.FileName,
                });
            }
        }

        return result;
    }

    // Build two lookups from the graph's immutable source assertions: one keyed by the pinned
    // proposition-support edge id, one keyed by the pinned evidence item id. Each resolves to the
    // real uploaded document version (+ file name) so a proposition can deep-link to its source even
    // when its SUPPORTS edge doesn't map through graph.Evidence. Empty when the graph has no assertions.
    private static (Dictionary<Guid, (Guid VersionId, string FileName)> BySupportId,
                    Dictionary<Guid, (Guid VersionId, string FileName)> ByItemId)
        BuildAssertionDocumentIndex(LegalMatterEvidenceGraphDto graph)
    {
        var bySupportId = new Dictionary<Guid, (Guid VersionId, string FileName)>();
        var byItemId = new Dictionary<Guid, (Guid VersionId, string FileName)>();
        if (graph.SourceAssertions is null || graph.SourceAssertions.Count == 0)
            return (bySupportId, byItemId);

        // File names are not carried on the assertion row; recover them from the matching evidence item
        // (same document version) so the UI can label and title the PDF. Falls back to empty when unknown.
        var fileByVersionId = new Dictionary<Guid, string>();
        if (graph.Evidence is not null)
        {
            foreach (var item in graph.Evidence)
            {
                if (item.LegalDocumentVersionId != Guid.Empty && !fileByVersionId.ContainsKey(item.LegalDocumentVersionId))
                    fileByVersionId[item.LegalDocumentVersionId] = item.DocumentFileName;
            }
        }

        foreach (var assertion in graph.SourceAssertions)
        {
            if (assertion.LegalDocumentVersionId == Guid.Empty)
                continue;
            if (assertion.VerificationStateCode == LegalSourceAssertionStates.Invalidated)
                continue;

            var fileName = fileByVersionId.TryGetValue(assertion.LegalDocumentVersionId, out var f) ? f : string.Empty;
            var doc = (assertion.LegalDocumentVersionId, fileName);

            if (assertion.LegalPropositionSupportId is { } supportId && supportId != Guid.Empty)
                bySupportId.TryAdd(supportId, doc);
            if (assertion.LegalEvidenceItemId is { } itemId && itemId != Guid.Empty)
                byItemId.TryAdd(itemId, doc);
        }

        return (bySupportId, byItemId);
    }

    // Resolve a proposition's backing document from its support edges using the source-assertion
    // indexes (support-edge id first, then evidence item id). Returns null when no assertion pins any
    // of the proposition's edges to a document version.
    private static (Guid VersionId, string FileName)? ResolveFromAssertions(
        IReadOnlyCollection<LegalPropositionSupportDto> support,
        Dictionary<Guid, (Guid VersionId, string FileName)> documentBySupportId,
        Dictionary<Guid, (Guid VersionId, string FileName)> assertionDocumentByItemId)
    {
        if (documentBySupportId.Count == 0 && assertionDocumentByItemId.Count == 0)
            return null;

        foreach (var edge in support)
        {
            if (documentBySupportId.TryGetValue(edge.LegalPropositionSupportId, out var byEdge))
                return byEdge;
            if (assertionDocumentByItemId.TryGetValue(edge.LegalEvidenceItemId, out var byItem))
                return byItem;
        }

        return null;
    }

    private static WideExternalKnowledgeSnippet BuildSnippet(
        LegalEvidenceGraphPropositionDto proposition,
        Guid? branchId,
        string? branchDisplayName,
        string status,
        decimal supportScore,
        DateTime nowUtc,
        (Guid VersionId, string FileName)? sourceDocument = null)
    {
        var admitted = string.Equals(status, StatusVerifiedSupport, StringComparison.Ordinal);
        return new WideExternalKnowledgeSnippet
        {
            // Query is the attribution fallback channel for rows without a BranchId; when a branch is
            // matched, BranchId is authoritative and Query simply mirrors the branch name.
            Query = branchDisplayName ?? proposition.PropositionText,
            // A snippet backed by a resolvable uploaded document is DOCUMENT EVIDENCE (titled by its file
            // name); only a proposition with no backing document falls back to the generic matter label.
            Title = sourceDocument is { } doc && !string.IsNullOrWhiteSpace(doc.FileName)
                ? $"Document Evidence — {doc.FileName}"
                : "Matter evidence",
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
            // Authoritative uploaded-document version backing this proposition, when resolvable. Lets the UI
            // open/stream the ACTUAL source document instead of the non-navigable matter-corpus:// Url.
            LegalDocumentVersionId = sourceDocument?.VersionId,
            LegalDocumentFileName = sourceDocument?.FileName,
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
