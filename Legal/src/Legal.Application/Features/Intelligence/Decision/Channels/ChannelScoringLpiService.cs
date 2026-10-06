using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// ChannelScoringLpiService — READ-ONLY transparency trace for the Channel Scoring LPI UI.
//
// It answers the question "how did each channel contribution affect the Outcome candidate scoring?"
// WITHOUT running any new algorithm. It loads the SAME persisted contributions and the SAME durable
// lineage the live recompetition path uses (mirrors ChannelContributionProjectionService), then uses
// ChannelScoringFormula — the single source of truth — to classify each contribution and compute the
// exact δ POLOXI consumed. Contributions that produced no signal (unverified / no lineage / context)
// are reported honestly as NO EFFECT rather than hidden.
//
// This never writes, never rescoring candidates, and never fabricates a composite score. POLOXI Core
// remains the sole owner of candidate ranking; this service only explains the channel inputs to it.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IChannelScoringLpiService
{
    Task<ChannelScoringLpiReadModel> GetForSessionAsync(
        Guid tenantId, Guid decisionSessionId, Guid matterId, CancellationToken cancellationToken = default);
}

public sealed class ChannelScoringLpiService(
    IChannelContributionRepository contributionRepository,
    IHierarchyNodeDecisionLineageRepository lineageRepository) : IChannelScoringLpiService
{
    private readonly IChannelContributionRepository _contributionRepository =
        contributionRepository ?? throw new ArgumentNullException(nameof(contributionRepository));
    private readonly IHierarchyNodeDecisionLineageRepository _lineageRepository =
        lineageRepository ?? throw new ArgumentNullException(nameof(lineageRepository));

    public async Task<ChannelScoringLpiReadModel> GetForSessionAsync(
        Guid tenantId, Guid decisionSessionId, Guid matterId, CancellationToken cancellationToken = default)
    {
        // Durable 0368 lineage is the authoritative link between hierarchy executions/nodes and this
        // decision session — exactly what the recompetition projection reads first.
        var lineageRows = await _lineageRepository.GetLineageForSessionAsync(tenantId, decisionSessionId, cancellationToken);
        if (lineageRows.Count == 0)
            return ChannelScoringLpiReadModel.Empty(matterId);

        var nodeKeys = lineageRows
            .Select(r => (r.HierarchyExecutionId, r.HierarchyNodeId))
            .ToHashSet();

        var contributions = new List<DecisionContribution>();
        foreach (var executionId in lineageRows.Select(r => r.HierarchyExecutionId).Distinct())
        {
            var dtos = await _contributionRepository.GetContributionsForExecutionAsync(tenantId, executionId, cancellationToken);
            foreach (var dto in dtos)
            {
                if (nodeKeys.Contains((dto.HierarchyExecutionId, dto.HierarchyNodeId)))
                    contributions.Add(ChannelContributionRehydration.ToContribution(dto, tenantId));
            }
        }

        if (contributions.Count == 0)
            return ChannelScoringLpiReadModel.Empty(matterId);

        var resolver = ChannelContributionLineageResolver.FromLineageRows(lineageRows);

        var traces = new List<ChannelContributionTrace>(contributions.Count);
        foreach (var contribution in contributions)
            traces.Add(BuildTrace(contribution, resolver));

        var aggregates = BuildAggregates(traces);
        var effective = traces.Count(t => t.AppliedDelta != 0.0 || string.Equals(t.Effect, "Reopen", StringComparison.Ordinal));
        var noEffect = traces.Count - effective;

        return new ChannelScoringLpiReadModel(
            matterId,
            ChannelScoringFormulaLegend.Default,
            traces
                .OrderByDescending(t => Math.Abs(t.AppliedDelta))
                .ThenBy(t => t.ChannelType, StringComparer.Ordinal)
                .ToArray(),
            aggregates,
            traces.Count,
            effective,
            noEffect);
    }

    // Classifies one contribution with the SAME rules the live adapter applies, then explains the result.
    private static ChannelContributionTrace BuildTrace(
        DecisionContribution contribution, IChannelContributionLineageResolver resolver)
    {
        var effect = ChannelScoringFormula.ClassifyEffect(contribution);
        var lineage = resolver.Resolve(contribution) ?? ChannelContributionLineage.None;
        var hasLineage = lineage.HasLineage;

        var channelCode = DecisionChannelCodes.ToCode(contribution.ChannelType);
        var relationCode = DecisionChannelCodes.ToCode(contribution.Relation);
        var verificationCode = DecisionChannelCodes.ToCode(contribution.VerificationState);
        var targetDimension = MapTargetDimension(contribution.TargetSignalCode);

        // δ is only applied when the contribution both classifies to a signal AND has lineage — the
        // two gates the live projection enforces before a signal ever reaches POLOXI.
        var classifiedDelta = ChannelScoringFormula.DeltaFor(contribution, effect);
        var appliedDelta = hasLineage ? classifiedDelta : 0.0;

        var (effectLabel, formula, explanation) = Explain(contribution, effect, hasLineage, appliedDelta, targetDimension);

        var branchIds = hasLineage ? lineage.BranchIds.ToArray() : [];
        // Mirror the adapter: direct candidate lineage only emits when there is NO branch lineage.
        var candidateIds = hasLineage && lineage.BranchIds.Count == 0
            ? lineage.CandidateIds.ToArray()
            : Array.Empty<Guid>();

        return new ChannelContributionTrace(
            contribution.ContributionId,
            channelCode,
            relationCode,
            verificationCode,
            contribution.TargetSignalCode,
            targetDimension,
            contribution.Magnitude,
            effectLabel,
            hasLineage,
            appliedDelta,
            formula,
            explanation,
            contribution.Provenance?.SourceLabel,
            branchIds,
            candidateIds);
    }

    private static (string EffectLabel, string Formula, string Explanation) Explain(
        DecisionContribution contribution,
        ChannelScoringFormula.ContributionEffect effect,
        bool hasLineage,
        double appliedDelta,
        string? targetDimension)
    {
        var dimension = targetDimension ?? "Verification+Authority+Evidence (legacy coupling)";

        if (effect == ChannelScoringFormula.ContributionEffect.None)
            return ("No effect",
                "— (gated out before scoring)",
                $"{DescribeRelation(contribution)} is not an eligible positive/negative signal (ContextOnly / Insufficient / unverified support), so POLOXI received no δ from it.");

        if (!hasLineage)
            return ("No effect (no lineage)",
                "— (no branch/candidate link)",
                $"This contribution classifies as {EffectName(effect)} but its target node resolves to no POLOXI branch or candidate, so it cannot move the ranking (δ = 0).");

        return effect switch
        {
            ChannelScoringFormula.ContributionEffect.Support => (
                "Positive support",
                contribution.Magnitude is { } m
                    ? $"δ = 0.15 + ({Ceiling(contribution):0.##} − 0.15) × {Math.Clamp(m, 0, 1):0.##} = {appliedDelta:+0.###}"
                    : $"δ = {appliedDelta:+0.###} (fixed {RelationBand(contribution)})",
                $"Verified {DescribeRelation(contribution)} added {appliedDelta:+0.###} to the {dimension} input of {LineageScope(contribution)}; POLOXI folded that into its candidate competition."),
            ChannelScoringFormula.ContributionEffect.Contradict => (
                "Negative (contradiction)",
                $"δ = {appliedDelta:+0.###} (contradiction magnitude)",
                $"{DescribeRelation(contribution)} applied {appliedDelta:+0.###} against the {dimension} input; POLOXI weighed this when recompeting candidates."),
            ChannelScoringFormula.ContributionEffect.Reopen => (
                "Reopen",
                "δ = 0 (reopen request)",
                $"This challenge carries no direct δ; it requested POLOXI to reopen verification of the targeted branch/candidate."),
            _ => ("No effect", "—", "No signal emitted."),
        };
    }

    private static IReadOnlyList<ChannelDimensionAggregate> BuildAggregates(IReadOnlyList<ChannelContributionTrace> traces)
        => traces
            .Where(t => t.AppliedDelta != 0.0 && t.TargetDimension is not null)
            .GroupBy(t => t.TargetDimension!)
            .Select(g => new ChannelDimensionAggregate(
                g.Key,
                g.Count(),
                g.Sum(t => t.AppliedDelta),
                g.Where(t => t.AppliedDelta > 0).Sum(t => t.AppliedDelta),
                g.Where(t => t.AppliedDelta < 0).Sum(t => t.AppliedDelta)))
            .OrderByDescending(a => Math.Abs(a.NetDelta))
            .ToArray();

    private static string? MapTargetDimension(string? targetSignalCode) => targetSignalCode switch
    {
        DecisionChannelCodes.TargetSignal.EvidenceSupport => "Evidence",
        DecisionChannelCodes.TargetSignal.FactSupport => "Fact",
        DecisionChannelCodes.TargetSignal.AuthoritySupport => "Authority",
        DecisionChannelCodes.TargetSignal.LegalSupport => "Legal",
        _ => null,
    };

    private static double Ceiling(DecisionContribution c)
        => c.Relation == ContributionRelation.Qualifies
            ? ChannelScoringFormula.QualifiedSupportMagnitude
            : ChannelScoringFormula.SupportMagnitude;

    private static string RelationBand(DecisionContribution c)
        => c.Relation == ContributionRelation.Qualifies ? "qualified support +0.15" : "support +0.30";

    private static string DescribeRelation(DecisionContribution c)
        => $"{DecisionChannelCodes.ToCode(c.ChannelType)} · {DecisionChannelCodes.ToCode(c.Relation)}";

    private static string LineageScope(DecisionContribution c) => "its linked branch/candidate";

    private static string EffectName(ChannelScoringFormula.ContributionEffect effect) => effect switch
    {
        ChannelScoringFormula.ContributionEffect.Support => "positive support",
        ChannelScoringFormula.ContributionEffect.Contradict => "contradiction",
        ChannelScoringFormula.ContributionEffect.Reopen => "reopen",
        _ => "no signal",
    };
}
