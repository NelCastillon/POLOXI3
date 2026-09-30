using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// HumanIntelligence channel (slice 2). Turns attorney decision input — the DB-backed Human Intelligence
// surface (IAttorneyDecisionInputRepository) — into qualitative contributions bound to nodes of the
// matter's AUTHORITATIVE persisted hierarchy (migration 0366).
//
// Validator ownership: HumanIntelligence trusts the EXISTING attorney governance path. An attorney's
// node signal is only admitted through its OWN lifecycle state:
//   * A node carrying a governance-APPROVED matter assessment emits a VERIFIED SUPPORTS contribution
//     on FactSupport (an authorized attorney confirmed the node).
//   * A node with OPEN attorney challenges emits a DISPUTED CHALLENGES contribution on Uncertainty
//     (a human reopened verification — this raises uncertainty, it does not add positive support).
// It never re-adjudicates attorney work, never invents a node or a signal, and maps an ADI node to a
// hierarchy node by the SAME deterministic lexical overlap the DocumentEvidence channel uses (no LLM,
// no scoring engine). Unmatched attorney nodes are dropped fail-soft. POLOXI Wide2 remains the sole
// owner of how support/uncertainty change candidate competition and outcome.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class HumanIntelligenceChannel(
    IAttorneyDecisionInputRepository attorneyDecisionInputRepository,
    ILegalHierarchyExecutionRepository hierarchyExecutionRepository) : IDecisionChannel
{
    // Attorney node text is authored against the same decision context, so a slightly stronger overlap
    // gate keeps the human-to-hierarchy binding conservative.
    private const int MinimumSharedTokens = 2;

    public DecisionChannelType ChannelType => DecisionChannelType.HumanIntelligence;

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

        var humanIntelligence = await attorneyDecisionInputRepository.GetMatterHumanIntelligenceAsync(
            context.TenantId, context.DecisionMatterId, cancellationToken);

        // Fail-soft: attorney input disabled or no attorney nodes captured yet.
        if (!humanIntelligence.AttorneyDecisionInputEnabled || humanIntelligence.Nodes.Count == 0)
            return [];

        // Only attorney nodes that carry an authoritative human signal (an approved assessment or an
        // open challenge) can move a hierarchy node; the rest are advisory and dropped.
        var signalNodes = humanIntelligence.Nodes
            .Where(n => n.ApprovedAssessment is not null || n.OpenChallengeCount > 0)
            .Where(n => !string.IsNullOrWhiteSpace(n.NodeText))
            .ToArray();
        if (signalNodes.Length == 0)
            return [];

        var nodeIndex = ChannelNodeTextMatcher.BuildNodeIndex(execution.Nodes);
        if (nodeIndex.Count == 0)
            return [];

        var contributions = new List<DecisionContribution>();
        foreach (var attorneyNode in signalNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bestNode = ChannelNodeTextMatcher.BestMatch(attorneyNode.NodeText, nodeIndex, MinimumSharedTokens, out var bestOverlap);
            if (bestNode is null)
                continue;

            // A governance-approved matter assessment is a VERIFIED human confirmation of the node.
            if (attorneyNode.ApprovedAssessment is { } approval)
            {
                contributions.Add(new DecisionContribution
                {
                    TenantId = context.TenantId,
                    DecisionMatterId = context.DecisionMatterId,
                    HierarchyExecutionId = executionId,
                    HierarchyNodeId = bestNode.HierarchyNodeId,
                    ChannelType = DecisionChannelType.HumanIntelligence,
                    Relation = ContributionRelation.Supports,
                    VerificationState = ContributionVerificationState.Verified,
                    TargetSignalCode = DecisionChannelCodes.TargetSignal.FactSupport,
                    ActorUserId = approval.ApprovedByUserId,
                    Provenance = new ContributionProvenance
                    {
                        SourceTypeCode = "AttorneyDecisionNode.ApprovedAssessment",
                        SourceId = approval.ApprovalId,
                        SourceLabel = ChannelNodeTextMatcher.Truncate(attorneyNode.NodeText, 400),
                        VerificationReason =
                            $"Governance-approved attorney assessment ({approval.GovernancePolicyCode}) by {approval.ApprovedByDisplayName} matched node on {bestOverlap} shared terms.",
                    },
                });
            }

            // Open attorney challenges reopen verification: they raise uncertainty, never add support.
            if (attorneyNode.OpenChallengeCount > 0)
            {
                contributions.Add(new DecisionContribution
                {
                    TenantId = context.TenantId,
                    DecisionMatterId = context.DecisionMatterId,
                    HierarchyExecutionId = executionId,
                    HierarchyNodeId = bestNode.HierarchyNodeId,
                    ChannelType = DecisionChannelType.HumanIntelligence,
                    Relation = ContributionRelation.Challenges,
                    VerificationState = ContributionVerificationState.Disputed,
                    TargetSignalCode = DecisionChannelCodes.TargetSignal.Uncertainty,
                    ActorUserId = context.UserId,
                    Provenance = new ContributionProvenance
                    {
                        SourceTypeCode = "AttorneyDecisionNode.OpenChallenge",
                        SourceId = attorneyNode.DecisionNodeId,
                        SourceLabel = ChannelNodeTextMatcher.Truncate(attorneyNode.NodeText, 400),
                        VerificationReason =
                            $"{attorneyNode.OpenChallengeCount} open attorney challenge(s) reopened verification on a node matched by {bestOverlap} shared terms.",
                    },
                });
            }
        }

        return contributions;
    }
}
