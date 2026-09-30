using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// LegalAuthority channel (slice 3). Turns VERIFIED legal-authority evidence — statutes, regulations,
// and case law captured in the matter evidence graph (LegalEvidenceGraphItemDto with EvidenceTypeCode
// LEGAL_AUTHORITY) — into qualitative contributions bound to nodes of the matter's AUTHORITATIVE
// persisted hierarchy (migration 0366).
//
// Validator ownership: LegalAuthority trusts the EXISTING authority-verification path. An authority
// evidence item only feeds positive support once its EvidenceStateCode is VERIFIED (an unverifiable or
// hallucinated authority stays PROPOSED/INVALIDATED and is dropped). It never re-verifies a citation,
// never invents a node or a signal, and maps an authority to a hierarchy node by the SAME deterministic
// lexical overlap the other channels use (no LLM, no scoring engine). Every admitted contribution
// targets the AuthoritySupport signal with an ESTABLISHES relation and records the qualitative
// applicability/directness qualifiers reserved on Legal_ChannelContribution. POLOXI Wide2 remains the
// sole owner of how authority support changes candidate competition and outcome.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalAuthorityChannel(
    ILegalDocumentCorpusRepository documentCorpusRepository,
    ILegalHierarchyExecutionRepository hierarchyExecutionRepository) : IDecisionChannel
{
    private const int MinimumSharedTokens = 2;
    private const string LegalAuthorityEvidenceType = "LEGAL_AUTHORITY";

    public DecisionChannelType ChannelType => DecisionChannelType.LegalAuthority;

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

        // Only VERIFIED legal-authority evidence may feed positive AuthoritySupport into POLOXI. Anything
        // still PROPOSED/DISPUTED/INVALIDATED (e.g. an unverifiable citation) is intentionally excluded.
        var verifiedAuthorities = evidenceGraph.Evidence
            .Where(e => string.Equals(e.EvidenceTypeCode, LegalAuthorityEvidenceType, StringComparison.OrdinalIgnoreCase))
            .Where(e => string.Equals(e.EvidenceStateCode, LegalEvidenceStates.Verified, StringComparison.OrdinalIgnoreCase))
            .Where(e => !string.IsNullOrWhiteSpace(e.Summary))
            .ToArray();
        if (verifiedAuthorities.Length == 0)
            return [];

        var nodeIndex = ChannelNodeTextMatcher.BuildNodeIndex(execution.Nodes);
        if (nodeIndex.Count == 0)
            return [];

        var contributions = new List<DecisionContribution>();
        foreach (var authority in verifiedAuthorities)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bestNode = ChannelNodeTextMatcher.BestMatch(authority.Summary, nodeIndex, MinimumSharedTokens, out var bestOverlap);
            if (bestNode is null)
                continue;

            contributions.Add(new DecisionContribution
            {
                TenantId = context.TenantId,
                DecisionMatterId = context.DecisionMatterId,
                HierarchyExecutionId = executionId,
                HierarchyNodeId = bestNode.HierarchyNodeId,
                ChannelType = DecisionChannelType.LegalAuthority,
                Relation = ContributionRelation.Establishes,
                VerificationState = ContributionVerificationState.Verified,
                TargetSignalCode = DecisionChannelCodes.TargetSignal.AuthoritySupport,
                // Qualitative qualifiers (codes, not scores). Directness is derived from how strongly the
                // authority text lexically governs the node; applicability records the governing dimension.
                ApplicabilityCode = string.IsNullOrWhiteSpace(authority.DimensionCode) ? null : authority.DimensionCode,
                DirectnessCode = ResolveDirectness(bestOverlap),
                ActorUserId = context.UserId,
                Provenance = new ContributionProvenance
                {
                    SourceTypeCode = "Legal_EvidenceItem.LegalAuthority",
                    SourceId = authority.LegalEvidenceItemId,
                    SourceLabel = ChannelNodeTextMatcher.Truncate(authority.Summary, 400),
                    LegalDocumentId = authority.LegalDocumentId,
                    LegalDocumentVersionId = authority.LegalDocumentVersionId,
                    LegalDocumentPassageId = authority.LegalDocumentPassageId,
                    VerificationReason = $"Verified legal authority ({authority.DimensionCode}) matched node on {bestOverlap} shared terms.",
                },
            });
        }

        return contributions;
    }

    // Deterministic, qualitative directness band from lexical overlap — NOT a numeric score persisted to
    // the boundary. POLOXI still owns the effective authority signal.
    private static string ResolveDirectness(int sharedTokens) => sharedTokens switch
    {
        >= 5 => "DIRECTLY_GOVERNING",
        >= 3 => "GOVERNING",
        _ => "PERSUASIVE",
    };
}
