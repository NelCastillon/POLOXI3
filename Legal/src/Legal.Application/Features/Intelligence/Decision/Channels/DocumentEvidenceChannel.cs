using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// DocumentEvidence channel (slice 1). Turns VERIFIED document evidence into qualitative contributions
// bound to nodes of the matter's AUTHORITATIVE persisted hierarchy (migration 0366).
//
// Validator ownership: DocumentEvidence trusts the EXISTING AER pipeline. It only admits source
// anchors whose VerificationStateCode is VERIFIED (Legal_SourceAssertion). It never re-verifies text
// and never invents a node or a signal — the mapping from an assertion to a hierarchy node is a
// deterministic lexical overlap (no LLM, no scoring engine), and unmatched assertions are dropped
// fail-soft. Every emitted contribution targets the EvidenceSupport signal with a SUPPORTS relation;
// POLOXI Wide2 remains the sole owner of how that support changes candidate competition.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DocumentEvidenceChannel(
    ILegalDocumentCorpusRepository documentCorpusRepository,
    ILegalHierarchyExecutionRepository hierarchyExecutionRepository) : IDecisionChannel
{
    // Minimum shared significant tokens between an assertion's quoted text and a node statement before
    // we consider the assertion to be evidence for that node. Deterministic, conservative gate.
    private const int MinimumSharedTokens = 2;

    public DecisionChannelType ChannelType => DecisionChannelType.DocumentEvidence;

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

        // Pre-tokenize the candidate nodes once. Only leaf-ish proposition/factor nodes carry evidence;
        // grouping/dimension containers are skipped so support lands on the specific proposition.
        var nodeIndex = ChannelNodeTextMatcher.BuildNodeIndex(execution.Nodes);
        if (nodeIndex.Count == 0)
            return [];

        var contributions = new List<DecisionContribution>();
        foreach (var assertion in verifiedAssertions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bestNode = ChannelNodeTextMatcher.BestMatch(assertion.QuotedText, nodeIndex, MinimumSharedTokens, out var bestOverlap);
            if (bestNode is null)
                continue;

            contributions.Add(new DecisionContribution
            {
                TenantId = context.TenantId,
                DecisionMatterId = context.DecisionMatterId,
                HierarchyExecutionId = executionId,
                HierarchyNodeId = bestNode.HierarchyNodeId,
                ChannelType = DecisionChannelType.DocumentEvidence,
                Relation = ContributionRelation.Supports,
                VerificationState = ContributionVerificationState.Verified,
                TargetSignalCode = DecisionChannelCodes.TargetSignal.EvidenceSupport,
                ActorUserId = context.UserId,
                Provenance = new ContributionProvenance
                {
                    SourceTypeCode = "Legal_SourceAssertion",
                    SourceId = assertion.LegalSourceAssertionId,
                    SourceLabel = ChannelNodeTextMatcher.Truncate(assertion.QuotedText, 400),
                    LegalDocumentVersionId = assertion.LegalDocumentVersionId,
                    LegalDocumentPassageId = assertion.LegalDocumentPassageId,
                    VerificationReason = $"AER-verified source anchor matched node on {bestOverlap} shared terms.",
                },
            });
        }

        return contributions;
    }
}
