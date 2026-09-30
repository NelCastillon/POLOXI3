using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// ExternalResearch channel (slice 6, final). Turns EXTERNALLY-RESEARCHED, verified legal authority —
// statutes, regulations, and case law retrieved from external providers through the research loop and
// persisted as verification rows (Legal_DecisionEvidenceVerification joined to Legal_DecisionEvidence)
// — into qualitative contributions bound to nodes of the matter's AUTHORITATIVE persisted hierarchy
// (migration 0366).
//
// This channel is DISTINCT from LegalAuthority: LegalAuthority reads authority captured in the matter
// evidence graph (typically document-derived), while ExternalResearch reads authority the research loop
// went OUT and retrieved from external sources, carrying its own provider provenance and verification
// lifecycle.
//
// Validator ownership: ExternalResearch trusts the EXISTING evidence-verification path. A retrieved
// authority only feeds positive support once its verification says IsVerified AND IsDecisionAuthorized
// (an unverifiable or non-authorized retrieval stays unadmitted and is dropped fail-soft). It never
// re-verifies a citation, never invents a node or a signal, and maps a retrieved authority to a
// hierarchy node by the SAME deterministic lexical overlap the other channels use (no LLM, no scoring
// engine). Every admitted contribution targets the AuthoritySupport signal with an ESTABLISHES relation
// and records the qualitative directness qualifier reserved on Legal_ChannelContribution. POLOXI Wide2
// remains the sole owner of how authority support changes candidate competition and outcome.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class ExternalResearchChannel(
    ILegalDecisionRepository decisionRepository,
    ILegalHierarchyExecutionRepository hierarchyExecutionRepository) : IDecisionChannel
{
    // Retrieved authority text is authored elsewhere (external provider), so a conservative overlap gate
    // keeps the retrieval-to-hierarchy binding tight.
    private const int MinimumSharedTokens = 2;

    public DecisionChannelType ChannelType => DecisionChannelType.ExternalResearch;

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

        var researched = await decisionRepository.GetMatterExternalResearchEvidenceAsync(
            context.TenantId, context.DecisionMatterId, cancellationToken);

        // Only externally-retrieved authority that is verified AND decision-authorized may feed positive
        // AuthoritySupport into POLOXI. Anything unverified or not yet authorized is intentionally excluded.
        var admitted = researched
            .Where(e => e.IsVerified && e.IsDecisionAuthorized)
            .Where(e => !string.IsNullOrWhiteSpace(BestText(e)))
            .ToArray();
        if (admitted.Length == 0)
            return [];

        var nodeIndex = ChannelNodeTextMatcher.BuildNodeIndex(execution.Nodes);
        if (nodeIndex.Count == 0)
            return [];

        var contributions = new List<DecisionContribution>();
        foreach (var authority in admitted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var matchText = BestText(authority);
            var bestNode = ChannelNodeTextMatcher.BestMatch(matchText, nodeIndex, MinimumSharedTokens, out var bestOverlap);
            if (bestNode is null)
                continue;

            contributions.Add(new DecisionContribution
            {
                TenantId = context.TenantId,
                DecisionMatterId = context.DecisionMatterId,
                HierarchyExecutionId = executionId,
                HierarchyNodeId = bestNode.HierarchyNodeId,
                ChannelType = DecisionChannelType.ExternalResearch,
                Relation = ContributionRelation.Establishes,
                VerificationState = ContributionVerificationState.Verified,
                TargetSignalCode = DecisionChannelCodes.TargetSignal.AuthoritySupport,
                // Qualitative qualifiers (codes, not scores). Applicability records the retrieved authority
                // type; directness is derived from how strongly the authority text lexically governs the node.
                ApplicabilityCode = string.IsNullOrWhiteSpace(authority.SourceTypeCode) ? null : authority.SourceTypeCode,
                DirectnessCode = ResolveDirectness(bestOverlap),
                ActorUserId = context.UserId,
                Provenance = new ContributionProvenance
                {
                    SourceTypeCode = "Legal_DecisionEvidenceVerification.ExternalResearch",
                    SourceId = authority.DecisionEvidenceVerificationId,
                    SourceLabel = ChannelNodeTextMatcher.Truncate(matchText, 400),
                    VerificationReason =
                        $"Verified external research authority ({authority.SourceTypeCode}"
                        + $"{(string.IsNullOrWhiteSpace(authority.SourceProvider) ? string.Empty : $" via {authority.SourceProvider}")}) "
                        + $"matched node on {bestOverlap} shared terms.",
                },
            });
        }

        return contributions;
    }

    // Prefer the retrieved source title as the anchor for lexical matching, falling back to the snippet.
    private static string BestText(ExternalResearchEvidenceDto e) =>
        !string.IsNullOrWhiteSpace(e.SourceTitle) ? e.SourceTitle : (e.Snippet ?? string.Empty);

    // Deterministic, qualitative directness band from lexical overlap — NOT a numeric score persisted to
    // the boundary. POLOXI still owns the effective authority signal.
    private static string ResolveDirectness(int sharedTokens) => sharedTokens switch
    {
        >= 5 => "DIRECTLY_GOVERNING",
        >= 3 => "GOVERNING",
        _ => "PERSUASIVE",
    };
}
