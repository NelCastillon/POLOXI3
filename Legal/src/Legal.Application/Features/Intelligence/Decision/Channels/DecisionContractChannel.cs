using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// DecisionContract channel (slice 5). Turns the attorney-defined, versioned Decision Contract — the
// DB-backed decision boundary (ILegalDecisionContractRepository) — into qualitative contributions
// bound to nodes of the matter's AUTHORITATIVE persisted hierarchy (migration 0366).
//
// Validator ownership: the DecisionContract's OWN validator is its GOVERNED lifecycle. A contract's
// boundary is only admitted once it has passed governance (ACTIVE, or APPROVED awaiting activation);
// a DRAFT / READY_FOR_REVIEW / SUPERSEDED / REJECTED contract carries no authoritative boundary and
// is dropped fail-soft. This channel never re-adjudicates the contract, never edits POLOXI weights,
// and never invents a node or a signal. From the governed contract it emits:
//   * A KNOWN fact boundary  → VERIFIED SUPPORTS on FactSupport (an attorney-established boundary fact).
//   * A DISPUTED fact boundary → DISPUTED CHALLENGES on Uncertainty (a contested boundary reopens
//     verification — it raises uncertainty, it does not add positive support). UNKNOWN is dropped.
//   * A STATUTE_RULE tag → VERIFIED ESTABLISHES on AuthoritySupport (the governing rule the contract
//     pins the decision to), carrying the contract AuthorityCutoffDate as its effectivity bound.
//   * A KEY_ISSUE tag → VERIFIED CONTEXT_ONLY on FactSupport (frames the node; does not move support
//     by itself).
// It maps each boundary/tag to a hierarchy node by the SAME deterministic lexical overlap the other
// channels use (no LLM, no scoring engine) and drops unmatched items fail-soft. POLOXI Wide2 remains
// the sole owner of how support/uncertainty change candidate competition and outcome.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionContractChannel(
    ILegalDecisionContractRepository decisionContractRepository,
    ILegalHierarchyExecutionRepository hierarchyExecutionRepository) : IDecisionChannel
{
    // Contract text is authored against the same decision context, so a conservative overlap gate keeps
    // the contract-to-hierarchy binding tight.
    private const int MinimumSharedTokens = 2;

    // Contract statuses whose boundary has passed governance and is therefore authoritative. A DRAFT or
    // in-review contract is intentionally excluded — its boundary is not yet trustworthy.
    private static readonly HashSet<string> GovernedStatusCodes =
        new(StringComparer.OrdinalIgnoreCase) { DecisionContractStatuses.Active, DecisionContractStatuses.Approved };

    public DecisionChannelType ChannelType => DecisionChannelType.DecisionContract;

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

        var contract = await decisionContractRepository.GetCurrentContractAsync(context.TenantId, context.DecisionMatterId, cancellationToken);

        // Only a governance-approved (ACTIVE / APPROVED) contract carries an authoritative boundary.
        if (contract is null || !GovernedStatusCodes.Contains(contract.StatusCode))
            return [];

        var nodeIndex = ChannelNodeTextMatcher.BuildNodeIndex(execution.Nodes);
        if (nodeIndex.Count == 0)
            return [];

        // A statute-rule authority is time-scoped by the contract's authority cutoff, when set.
        DateTime? authorityCutoffUtc = contract.AuthorityCutoffDate is { } cutoff
            ? cutoff.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            : null;

        var contributions = new List<DecisionContribution>();

        foreach (var boundary in contract.FactBoundaries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // An UNKNOWN boundary asserts nothing yet — it does not bear on the decision.
            var isKnown = string.Equals(boundary.FactStateCode, DecisionContractFactStates.Known, StringComparison.OrdinalIgnoreCase);
            var isDisputed = string.Equals(boundary.FactStateCode, DecisionContractFactStates.Disputed, StringComparison.OrdinalIgnoreCase);
            if (!isKnown && !isDisputed)
                continue;

            if (string.IsNullOrWhiteSpace(boundary.SnapshotText))
                continue;

            var bestNode = ChannelNodeTextMatcher.BestMatch(boundary.SnapshotText, nodeIndex, MinimumSharedTokens, out var bestOverlap);
            if (bestNode is null)
                continue;

            contributions.Add(new DecisionContribution
            {
                TenantId = context.TenantId,
                DecisionMatterId = context.DecisionMatterId,
                HierarchyExecutionId = executionId,
                HierarchyNodeId = bestNode.HierarchyNodeId,
                ChannelType = DecisionChannelType.DecisionContract,
                // A KNOWN boundary supports the fact; a DISPUTED boundary challenges verification.
                Relation = isKnown ? ContributionRelation.Supports : ContributionRelation.Challenges,
                VerificationState = isKnown ? ContributionVerificationState.Verified : ContributionVerificationState.Disputed,
                TargetSignalCode = isKnown
                    ? DecisionChannelCodes.TargetSignal.FactSupport
                    : DecisionChannelCodes.TargetSignal.Uncertainty,
                ActorUserId = context.UserId,
                Provenance = new ContributionProvenance
                {
                    SourceTypeCode = isKnown ? "DecisionContractFactBoundary.Known" : "DecisionContractFactBoundary.Disputed",
                    SourceId = boundary.DecisionContractFactBoundaryId,
                    SourceLabel = ChannelNodeTextMatcher.Truncate(boundary.SnapshotText, 400),
                    VerificationReason =
                        $"Governed decision contract v{contract.VersionNumber} ({contract.StatusCode}) {boundary.FactStateCode} fact boundary matched node on {bestOverlap} shared terms.",
                },
            });
        }

        foreach (var tag in contract.Tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(tag.TagText))
                continue;

            var isStatuteRule = string.Equals(tag.TagKindCode, DecisionContractTagKinds.StatuteRule, StringComparison.OrdinalIgnoreCase);
            var isKeyIssue = string.Equals(tag.TagKindCode, DecisionContractTagKinds.KeyIssue, StringComparison.OrdinalIgnoreCase);
            if (!isStatuteRule && !isKeyIssue)
                continue;

            var bestNode = ChannelNodeTextMatcher.BestMatch(tag.TagText, nodeIndex, MinimumSharedTokens, out var bestOverlap);
            if (bestNode is null)
                continue;

            contributions.Add(new DecisionContribution
            {
                TenantId = context.TenantId,
                DecisionMatterId = context.DecisionMatterId,
                HierarchyExecutionId = executionId,
                HierarchyNodeId = bestNode.HierarchyNodeId,
                ChannelType = DecisionChannelType.DecisionContract,
                // A statute rule establishes the governing authority; a key issue only frames the node.
                Relation = isStatuteRule ? ContributionRelation.Establishes : ContributionRelation.ContextOnly,
                VerificationState = ContributionVerificationState.Verified,
                TargetSignalCode = isStatuteRule
                    ? DecisionChannelCodes.TargetSignal.AuthoritySupport
                    : DecisionChannelCodes.TargetSignal.FactSupport,
                // A statute rule is bounded by the contract's authority cutoff (null = always effective).
                EffectiveToUtc = isStatuteRule ? authorityCutoffUtc : null,
                ActorUserId = context.UserId,
                Provenance = new ContributionProvenance
                {
                    SourceTypeCode = isStatuteRule ? "DecisionContractTag.StatuteRule" : "DecisionContractTag.KeyIssue",
                    SourceId = tag.DecisionContractTagId,
                    SourceLabel = ChannelNodeTextMatcher.Truncate(tag.TagText, 400),
                    VerificationReason =
                        $"Governed decision contract v{contract.VersionNumber} ({contract.StatusCode}) {tag.TagKindCode} matched node on {bestOverlap} shared terms.",
                },
            });
        }

        return contributions;
    }
}
