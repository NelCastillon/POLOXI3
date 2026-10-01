using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// DocumentEvidence channel (slice 1). Turns VERIFIED document evidence into qualitative contributions
// bound to nodes of the matter's AUTHORITATIVE persisted hierarchy (migration 0366).
//
// Validator ownership: DocumentEvidence trusts the EXISTING AER/HRR pipeline. It only admits source
// anchors whose VerificationStateCode is VERIFIED (Legal_SourceAssertion). It never re-verifies text
// and never invents a node or a signal.
//
// RELATION (what the evidence establishes) comes from the AER edge the assertion already carries:
// LegalSourceAssertion.LegalPropositionSupportId → Legal_MatterPropositionSupport.RelationshipTypeCode
// (SUPPORTS / CONTRADICTS / QUALIFIES / INSUFFICIENT). The channel does NOT decide the answer — it
// reports the independently-derived AER verdict, so a CONTRADICTS anchor reaches POLOXI as Contradicts
// (never silently flipped to Supports). This honours "Retrieval Target ≠ Expected Answer".
//
// NODE BINDING (where the evidence belongs / HRR) prefers the canonical proposition statement the AER
// edge resolved to, matched against the authoritative hierarchy node text. Only when an assertion has
// NO proposition-support edge does the channel fall back to the deterministic lexical overlap of the
// raw quote with a SUPPORTS relation (unchanged legacy behaviour). Unmatched assertions are dropped
// fail-soft. POLOXI Wide2 remains the sole owner of how a contribution changes candidate competition.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DocumentEvidenceChannel(
    ILegalDocumentCorpusRepository documentCorpusRepository,
    ILegalHierarchyExecutionRepository hierarchyExecutionRepository) : IDecisionChannel
{
    // Minimum shared significant tokens between an assertion's quoted text and a node statement before
    // we consider the assertion to be evidence for that node. Deterministic, conservative gate.
    private const int MinimumSharedTokens = 2;

    public DecisionChannelType ChannelType => DecisionChannelType.DocumentEvidence;

    // Projects the authoritative AER edge vocabulary (Legal_MatterPropositionSupport.RelationshipTypeCode)
    // onto the qualitative ContributionRelation POLOXI understands. The channel never upgrades epistemic
    // state: CONTRADICTS stays Contradicts, QUALIFIES stays Qualifies, INSUFFICIENT stays Insufficient.
    // An unknown/association-only code resolves to ContextOnly (relevant context, moves no support by
    // itself) rather than being silently promoted to Supports.
    private static ContributionRelation ToContributionRelation(string? relationshipTypeCode) => relationshipTypeCode switch
    {
        LegalDocumentRelationshipTypes.Supports => ContributionRelation.Supports,
        LegalDocumentRelationshipTypes.Contradicts => ContributionRelation.Contradicts,
        LegalDocumentRelationshipTypes.Qualifies => ContributionRelation.Qualifies,
        LegalDocumentRelationshipTypes.Insufficient => ContributionRelation.Insufficient,
        _ => ContributionRelation.ContextOnly,
    };

    public async Task<IReadOnlyList<DecisionContribution>> ResolveContributionsAsync(
        DecisionChannelResolveContext context,
        CancellationToken cancellationToken = default)
    {
        // Fail-soft: no authoritative hierarchy means there is nothing to bind contributions to.
        if (context.AuthoritativeHierarchyExecutionId is not { } executionId)
            return [];

        var execution = await hierarchyExecutionRepository.GetExecutionAsync(context.TenantId, executionId, cancellationToken);
        if (execution is null || execution.Nodes.Count == 0)
            return [];

        var evidenceGraph = await documentCorpusRepository.GetMatterEvidenceGraphAsync(context.TenantId, context.DecisionMatterId, cancellationToken);

        // Only VERIFIED anchors may feed positive EvidenceSupport into POLOXI (mirrors the core invariant).
        var verifiedAssertions = evidenceGraph.SourceAssertions
            .Where(a => string.Equals(a.VerificationStateCode, LegalSourceAssertionStates.Verified, StringComparison.OrdinalIgnoreCase))
            .Where(a => !string.IsNullOrWhiteSpace(a.QuotedText))
            .ToArray();
        if (verifiedAssertions.Length == 0)
            return [];

        // Domain Pack (advisory): synonym terminology improves recall of node matching, and the
        // evidence-type→signal map lets a verified assertion inform the pack-declared POLOXI signal
        // instead of the hardcoded default. Null pack = unchanged raw-overlap + EvidenceSupport behavior.
        var resolvedPack = context.ResolvedPack;
        var termLookup = resolvedPack?.TermLookup;

        // Map each evidence item to its evidence-type code so a matched assertion can resolve the
        // pack signal via the item it anchors (LegalSourceAssertionDto has no type code of its own).
        var evidenceTypeByItemId = evidenceGraph.Evidence
            .Where(e => !string.IsNullOrWhiteSpace(e.EvidenceTypeCode))
            .GroupBy(e => e.LegalEvidenceItemId)
            .ToDictionary(g => g.Key, g => g.First().EvidenceTypeCode);

        // AER/HRR index. Every proposition-support edge (Legal_MatterPropositionSupport) carries the
        // independently-derived relation (SUPPORTS/CONTRADICTS/QUALIFIES/INSUFFICIENT) AND the canonical
        // proposition it was aligned to. An assertion that pins such an edge (LegalPropositionSupportId)
        // therefore already knows WHAT it establishes and WHERE it belongs — the channel reports that
        // verdict instead of re-deriving it by lexical overlap. Edge-id → (relation, proposition text).
        var supportEdgeById = evidenceGraph.Propositions
            .SelectMany(p => p.Support.Select(s => (Edge: s, p.PropositionText)))
            .GroupBy(x => x.Edge.LegalPropositionSupportId)
            .ToDictionary(
                g => g.Key,
                g => (g.First().Edge.RelationshipTypeCode, g.First().PropositionText));

        // Pre-tokenize the candidate nodes once. Only leaf-ish proposition/factor nodes carry evidence;
        // grouping/dimension containers are skipped so support lands on the specific proposition.
        var nodeIndex = ChannelNodeTextMatcher.BuildNodeIndex(execution.Nodes, termLookup);
        if (nodeIndex.Count == 0)
            return [];

        var contributions = new List<DecisionContribution>();
        foreach (var assertion in verifiedAssertions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // AER/HRR path: when the anchor pins a proposition-support edge, the edge is authoritative
            // for BOTH the relation (what it establishes) and the HRR target (the canonical proposition
            // text it was aligned to). Bind the node using that proposition text, not the raw quote, so
            // the evidence lands where the alignment already placed it. Relation flows straight through:
            // CONTRADICTS/QUALIFIES/INSUFFICIENT are preserved, never promoted to Supports.
            string matchText = assertion.QuotedText;
            ContributionRelation? aerRelation = null;
            if (assertion.LegalPropositionSupportId is { } supportId &&
                supportEdgeById.TryGetValue(supportId, out var edge))
            {
                aerRelation = ToContributionRelation(edge.RelationshipTypeCode);
                if (!string.IsNullOrWhiteSpace(edge.PropositionText))
                    matchText = edge.PropositionText;
            }

            var bestNode = ChannelNodeTextMatcher.BestMatch(matchText, nodeIndex, MinimumSharedTokens, termLookup, out var bestOverlap);
            if (bestNode is null)
                continue;

            // Match-confidence magnitude: how COMPLETELY the match text's wording covers the
            // proposition's wording = sharedTokens / nodeSignificantTokenCount, clamped to [0,1]. This is
            // an evidence-INTRINSIC strength (a property of the text), never the node's own POLOXI score,
            // so it cannot create a feedback loop and is stable across re-runs. Null when the node has no
            // significant tokens → the adapter falls back to its fixed δ. Mirrors the Human Intelligence
            // placement magnitude; POLOXI Wide2 still owns the scoring consequence.
            var nodeTokenCount = ChannelNodeTextMatcher.TokenizeWithSynonyms(bestNode.Statement, termLookup).Count;
            double? matchConfidence = nodeTokenCount > 0
                ? Math.Clamp((double)bestOverlap / nodeTokenCount, 0.0, 1.0)
                : null;

            // Default POLOXI signal/relation for verified document evidence. The AER edge relation (when
            // present) takes precedence over the hardcoded Supports default so the channel reports the
            // independently-derived verdict rather than assuming an answer.
            var targetSignalCode = DecisionChannelCodes.TargetSignal.EvidenceSupport;
            var relation = aerRelation ?? ContributionRelation.Supports;

            // Pack override: only when the anchored evidence item's type has a declared signal map entry.
            // The pack may redirect the target signal; the AER edge still owns the relation when it exists.
            if (resolvedPack is not null &&
                assertion.LegalEvidenceItemId is { } evidenceItemId &&
                evidenceTypeByItemId.TryGetValue(evidenceItemId, out var evidenceTypeCode) &&
                resolvedPack.ResolveSignal(evidenceTypeCode) is { } signal)
            {
                targetSignalCode = signal.TargetSignalCode;
                if (aerRelation is null)
                    relation = DecisionChannelCodes.ToRelation(signal.RelationCode);
            }

            contributions.Add(new DecisionContribution
            {
                TenantId = context.TenantId,
                DecisionMatterId = context.DecisionMatterId,
                HierarchyExecutionId = executionId,
                HierarchyNodeId = bestNode.HierarchyNodeId,
                ChannelType = DecisionChannelType.DocumentEvidence,
                Relation = relation,
                VerificationState = ContributionVerificationState.Verified,
                TargetSignalCode = targetSignalCode,
                Magnitude = matchConfidence,
                ActorUserId = context.UserId,
                Provenance = new ContributionProvenance
                {
                    SourceTypeCode = "Legal_SourceAssertion",
                    SourceId = assertion.LegalSourceAssertionId,
                    SourceLabel = ChannelNodeTextMatcher.Truncate(assertion.QuotedText, 400),
                    LegalDocumentVersionId = assertion.LegalDocumentVersionId,
                    LegalDocumentPassageId = assertion.LegalDocumentPassageId,
                    VerificationReason = aerRelation is { } aer
                        ? $"AER edge resolved {DecisionChannelCodes.ToCode(aer)}; bound to node on {bestOverlap} shared proposition terms."
                        : $"AER-verified source anchor matched node on {bestOverlap} shared terms.",
                },
            });
        }

        return contributions;
    }
}
