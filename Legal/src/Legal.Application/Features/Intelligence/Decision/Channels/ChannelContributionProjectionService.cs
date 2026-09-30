using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// End-to-end bridge: persisted channel contributions → typed DecisionBranchSignals.
//
// This is the ONE async entry point the decision pipeline calls to fold verified, source-truth channel
// contributions (migration 0367) into POLOXI recompetition. It composes the whole chain WITHOUT the
// pure adapter ever doing I/O:
//   1. Read persisted contributions for the run (ChannelContributionDto).
//   2. Rehydrate them into validator-agnostic DecisionContribution boundary objects.
//   3. Prefetch node→branch/candidate lineage once (migration 0368) into a synchronous resolver.
//   4. Run the pure LegalChannelSignalAdapter to emit TYPED, domain-neutral DecisionBranchSignals.
//
// Fail-soft and additive: no contributions or no lineage simply yields an empty signal set — POLOXI
// Wide2 remains the sole decision engine and is never blocked. The projected signals are appended to
// the existing recompetition input, never replacing legacy signals.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IChannelContributionProjectionService
{
    // Projects all verified channel contributions for a run-scoped hierarchy execution into typed
    // branch signals ready for DecisionRecompetition. Returns an empty list when nothing projects.
    Task<IReadOnlyList<DecisionBranchSignal>> ProjectForExecutionAsync(
        Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default);

    // Projects verified channel contributions that map (via durable 0368 lineage) to a decision session
    // into typed branch signals for that session's recompetition. This is the join point the decision
    // pipeline uses, since recompetition is DecisionSessionId-scoped. Empty when nothing projects.
    Task<IReadOnlyList<DecisionBranchSignal>> ProjectForSessionAsync(
        Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
}

public sealed class ChannelContributionProjectionService(
    IChannelContributionRepository contributionRepository,
    IHierarchyNodeDecisionLineageRepository lineageRepository) : IChannelContributionProjectionService
{
    private readonly IChannelContributionRepository _contributionRepository =
        contributionRepository ?? throw new ArgumentNullException(nameof(contributionRepository));
    private readonly IHierarchyNodeDecisionLineageRepository _lineageRepository =
        lineageRepository ?? throw new ArgumentNullException(nameof(lineageRepository));

    public async Task<IReadOnlyList<DecisionBranchSignal>> ProjectForExecutionAsync(
        Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default)
    {
        var dtos = await _contributionRepository.GetContributionsForExecutionAsync(tenantId, hierarchyExecutionId, cancellationToken);
        if (dtos.Count == 0)
            return [];

        var contributions = dtos
            .Select(dto => ChannelContributionRehydration.ToContribution(dto, tenantId))
            .ToArray();

        var resolver = await ChannelContributionLineageResolver.PrefetchAsync(
            _lineageRepository, tenantId, contributions, cancellationToken);

        var adapter = new LegalChannelSignalAdapter(resolver);
        return adapter.Project(contributions);
    }

    public async Task<IReadOnlyList<DecisionBranchSignal>> ProjectForSessionAsync(
        Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        // The durable 0368 lineage is the authoritative link between hierarchy executions/nodes and this
        // decision session. Read it first; with no lineage nothing can project (fail-soft).
        var lineageRows = await _lineageRepository.GetLineageForSessionAsync(tenantId, decisionSessionId, cancellationToken);
        if (lineageRows.Count == 0)
            return [];

        // Gather contributions from every hierarchy execution this session's lineage references, then
        // keep only those whose target node actually has a lineage row for THIS session.
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
            return [];

        var resolver = ChannelContributionLineageResolver.FromLineageRows(lineageRows);
        var adapter = new LegalChannelSignalAdapter(resolver);
        return adapter.Project(contributions);
    }
}
