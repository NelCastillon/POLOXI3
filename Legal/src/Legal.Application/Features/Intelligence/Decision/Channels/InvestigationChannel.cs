using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Investigation channel (slice 4). Turns matter-scoped intelligence findings — the DB-backed
// investigation/detection surface (AI.Legal_IntelligenceFinding via IIntelligenceRepository) — into
// qualitative contributions bound to nodes of the matter's AUTHORITATIVE persisted hierarchy (0366).
//
// Validator ownership: Investigation trusts the EXISTING finding-governance lifecycle. A finding's
// signal is only admitted through its OWN state (never re-adjudicated here):
//   * A RESOLVED finding whose resolution CONFIRMS the finding emits a VERIFIED ESTABLISHES
//     contribution on FactSupport (an investigation established/confirmed the fact).
//   * An OPEN finding emits a DISPUTED CHALLENGES contribution on Uncertainty (an active investigation
//     reopens verification — it raises uncertainty, it does not add positive support).
//   * A finding resolved as dismissed / false-positive is dropped: it no longer bears on the decision.
// It maps a finding to a hierarchy node by the SAME deterministic lexical overlap the other channels
// use (no LLM, no scoring engine) over the finding Title + Summary, and drops unmatched findings
// fail-soft. POLOXI Wide2 remains the sole owner of how support/uncertainty change candidate
// competition and outcome — the finding's own numeric Score/Confidence are intentionally NOT carried.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class InvestigationChannel(
    IIntelligenceRepository intelligenceRepository,
    ILegalHierarchyExecutionRepository hierarchyExecutionRepository) : IDecisionChannel
{
    private const int MinimumSharedTokens = 2;

    // The intelligence-platform entity code a matter's findings are recorded against.
    private const string MatterEntityTypeCode = "Matter";
    private const string ResolvedStatusCode = "RESOLVED";
    private const string OpenStatusCode = "OPEN";

    // Resolutions that mean the investigation CONFIRMED the finding (admitted as verified support).
    private static readonly HashSet<string> ConfirmingResolutionCodes =
        new(StringComparer.OrdinalIgnoreCase) { "CONFIRM", "CONFIRMED", "ACCEPT", "ACCEPTED", "SUBSTANTIATED" };

    // Resolutions that mean the finding no longer bears on the decision (dropped, not support).
    private static readonly HashSet<string> DismissedResolutionCodes =
        new(StringComparer.OrdinalIgnoreCase) { "DISMISS", "DISMISSED", "FALSE_POSITIVE", "REJECTED", "WAIVED" };

    public DecisionChannelType ChannelType => DecisionChannelType.Investigation;

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

        var findingsPage = await intelligenceRepository.SearchFindingsAsync(
            new SearchIntelligenceFindingsQuery(
                TenantId: context.TenantId,
                SearchTerm: null,
                CapabilityCode: null,
                EntityTypeCode: MatterEntityTypeCode,
                EntityId: context.DecisionMatterId,
                SeverityCode: null,
                StatusCode: null,
                PageNumber: 1,
                PageSize: 200),
            cancellationToken);

        var findings = findingsPage.Items
            .Where(f => !string.IsNullOrWhiteSpace(f.Title) || !string.IsNullOrWhiteSpace(f.Summary))
            .ToArray();
        if (findings.Length == 0)
            return [];

        // Domain Pack synonym terminology (advisory) only ADDS node-match recall; null pack = raw overlap.
        var termLookup = context.ResolvedPack?.TermLookup;
        var nodeIndex = ChannelNodeTextMatcher.BuildNodeIndex(execution.Nodes, termLookup);
        if (nodeIndex.Count == 0)
            return [];

        var contributions = new List<DecisionContribution>();
        foreach (var finding in findings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A finding dismissed as false-positive/rejected no longer informs the decision.
            if (IsDismissed(finding))
                continue;

            var matchText = $"{finding.Title} {finding.Summary}";
            var bestNode = ChannelNodeTextMatcher.BestMatch(matchText, nodeIndex, MinimumSharedTokens, termLookup, out var bestOverlap);
            if (bestNode is null)
                continue;

            // Match-confidence magnitude (support branch only): how COMPLETELY the finding text lexically
            // covers the node's wording = sharedTokens / nodeSignificantTokenCount, clamped to [0,1]. It is
            // finding-INTRINSIC, never the node's own POLOXI score, so it cannot create a feedback loop and is
            // stable across re-runs. Null when the node has no significant tokens -> the adapter falls back to
            // its fixed delta. Mirrors DocumentEvidence; POLOXI Wide2 still owns the scoring consequence.
            var nodeTokenCount = ChannelNodeTextMatcher.TokenizeWithSynonyms(bestNode.Statement, termLookup).Count;
            double? matchConfidence = nodeTokenCount > 0
                ? Math.Clamp((double)bestOverlap / nodeTokenCount, 0.0, 1.0)
                : null;

            // A confirmed, resolved finding is a VERIFIED investigation result establishing the fact.
            if (IsConfirmedResolved(finding))
            {
                contributions.Add(new DecisionContribution
                {
                    TenantId = context.TenantId,
                    DecisionMatterId = context.DecisionMatterId,
                    HierarchyExecutionId = executionId,
                    HierarchyNodeId = bestNode.HierarchyNodeId,
                    ChannelType = DecisionChannelType.Investigation,
                    Relation = ContributionRelation.Establishes,
                    VerificationState = ContributionVerificationState.Verified,
                    TargetSignalCode = DecisionChannelCodes.TargetSignal.FactSupport,
                    Magnitude = matchConfidence,
                    ActorUserId = context.UserId,
                    Provenance = new ContributionProvenance
                    {
                        SourceTypeCode = "IntelligenceFinding.Confirmed",
                        SourceId = finding.IntelligenceFindingId,
                        SourceLabel = ChannelNodeTextMatcher.Truncate(finding.Title, 400),
                        VerificationReason =
                            $"Confirmed {finding.CapabilityName} finding ({finding.ResolutionCode}) matched node on {bestOverlap} shared terms.",
                    },
                });
                continue;
            }

            // An open investigation finding reopens verification: it raises uncertainty, never support.
            if (IsOpen(finding))
            {
                contributions.Add(new DecisionContribution
                {
                    TenantId = context.TenantId,
                    DecisionMatterId = context.DecisionMatterId,
                    HierarchyExecutionId = executionId,
                    HierarchyNodeId = bestNode.HierarchyNodeId,
                    ChannelType = DecisionChannelType.Investigation,
                    Relation = ContributionRelation.Challenges,
                    VerificationState = ContributionVerificationState.Disputed,
                    TargetSignalCode = DecisionChannelCodes.TargetSignal.Uncertainty,
                    ActorUserId = context.UserId,
                    Provenance = new ContributionProvenance
                    {
                        SourceTypeCode = "IntelligenceFinding.Open",
                        SourceId = finding.IntelligenceFindingId,
                        SourceLabel = ChannelNodeTextMatcher.Truncate(finding.Title, 400),
                        VerificationReason =
                            $"Open {finding.CapabilityName} finding ({finding.SeverityCode}) matched node on {bestOverlap} shared terms.",
                    },
                });
            }
        }

        return contributions;
    }

    private static bool IsConfirmedResolved(IntelligenceFindingDto finding)
        => string.Equals(finding.StatusCode, ResolvedStatusCode, StringComparison.OrdinalIgnoreCase)
           && finding.ResolutionCode is { } code
           && ConfirmingResolutionCodes.Contains(code);

    private static bool IsDismissed(IntelligenceFindingDto finding)
        => finding.ResolutionCode is { } code && DismissedResolutionCodes.Contains(code);

    private static bool IsOpen(IntelligenceFindingDto finding)
        => string.Equals(finding.StatusCode, OpenStatusCode, StringComparison.OrdinalIgnoreCase)
           || finding.ResolvedDateUtc is null && !IsConfirmedResolved(finding);
}
