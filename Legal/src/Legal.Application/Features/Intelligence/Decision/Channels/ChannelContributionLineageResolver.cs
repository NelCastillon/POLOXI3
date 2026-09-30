using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Table-backed IChannelContributionLineageResolver.
//
// The resolver contract is intentionally SYNCHRONOUS (the adapter is a pure projection and must not do
// I/O mid-loop). Lineage, however, lives in the DB (migration 0368). This resolver bridges the two by
// PREFETCHING all lineage rows for a set of contributions once, then answering Resolve(...) from an
// in-memory map keyed by the run-scoped (HierarchyExecutionId, HierarchyNodeId).
//
// Fail-soft: a node with no lineage row resolves to ChannelContributionLineage.None, so the adapter
// simply does not project that contribution — it never guesses a branch/candidate. Build one instance
// per decision run via PrefetchAsync, then hand it to the adapter.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class ChannelContributionLineageResolver : IChannelContributionLineageResolver
{
    private readonly IReadOnlyDictionary<(Guid Execution, Guid Node), ChannelContributionLineage> _byNode;

    private ChannelContributionLineageResolver(
        IReadOnlyDictionary<(Guid Execution, Guid Node), ChannelContributionLineage> byNode)
        => _byNode = byNode;

    // Empty resolver (no lineage for anything) — useful as a fail-soft default.
    public static ChannelContributionLineageResolver Empty { get; } =
        new(new Dictionary<(Guid, Guid), ChannelContributionLineage>());

    // Builds a resolver directly from already-loaded lineage rows (e.g. a session-scoped read). Keeps
    // the node→branch/candidate mapping the DB's source-of-truth; no guessing, no extra round-trips.
    public static ChannelContributionLineageResolver FromLineageRows(
        IEnumerable<HierarchyNodeDecisionLineageDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var map = new Dictionary<(Guid, Guid), ChannelContributionLineage>();
        foreach (var group in rows.GroupBy(r => (r.HierarchyExecutionId, r.HierarchyNodeId)))
        {
            var branchIds = group.Where(r => r.DecisionBranchId.HasValue).Select(r => r.DecisionBranchId!.Value).Distinct().ToArray();
            var candidateIds = group.Where(r => r.DecisionCandidateId.HasValue).Select(r => r.DecisionCandidateId!.Value).Distinct().ToArray();
            map[group.Key] = branchIds.Length == 0 && candidateIds.Length == 0
                ? ChannelContributionLineage.None
                : new ChannelContributionLineage(branchIds, candidateIds);
        }

        return new ChannelContributionLineageResolver(map);
    }

    public ChannelContributionLineage Resolve(DecisionContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        return _byNode.TryGetValue((contribution.HierarchyExecutionId, contribution.HierarchyNodeId), out var lineage)
            ? lineage
            : ChannelContributionLineage.None;
    }

    // Prefetches lineage for every distinct target node referenced by the contributions and builds a
    // ready-to-use synchronous resolver. Tenant-scoped; contributions from other tenants are ignored.
    public static async Task<ChannelContributionLineageResolver> PrefetchAsync(
        IHierarchyNodeDecisionLineageRepository repository,
        Guid tenantId,
        IReadOnlyList<DecisionContribution> contributions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(contributions);

        var map = new Dictionary<(Guid, Guid), ChannelContributionLineage>();

        foreach (var key in contributions
            .Where(c => c is not null && c.TenantId == tenantId)
            .Select(c => (c.HierarchyExecutionId, c.HierarchyNodeId))
            .Distinct())
        {
            if (map.ContainsKey(key))
                continue;

            var rows = await repository.GetLineageForNodeAsync(tenantId, key.Item1, key.Item2, cancellationToken);
            if (rows.Count == 0)
            {
                map[key] = ChannelContributionLineage.None;
                continue;
            }

            var branchIds = rows.Where(r => r.DecisionBranchId.HasValue).Select(r => r.DecisionBranchId!.Value).Distinct().ToArray();
            var candidateIds = rows.Where(r => r.DecisionCandidateId.HasValue).Select(r => r.DecisionCandidateId!.Value).Distinct().ToArray();
            map[key] = new ChannelContributionLineage(branchIds, candidateIds);
        }

        return new ChannelContributionLineageResolver(map);
    }
}
