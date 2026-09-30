using Legal.Application.Features.Intelligence.Decision.Channels;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Read access to the durable Hierarchy Node → Decision lineage map (migration 0368;
// POLOXI.Legal_HierarchyNodeDecisionLineage).
//
// This is the ONE place the run-scoped (HierarchyExecutionId, HierarchyNodeId) → authoritative POLOXI
// DecisionSession/Branch/Candidate mapping lives. The lineage resolver reads it to turn a persisted
// channel contribution's target node into branch/candidate ids WITHOUT the adapter ever guessing.
// Fail-soft: no row = ChannelContributionLineage.None (the contribution simply does not project).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IHierarchyNodeDecisionLineageRepository
{
    // All active lineage rows for a run-scoped hierarchy node (a node may map to multiple targets).
    Task<IReadOnlyCollection<HierarchyNodeDecisionLineageDto>> GetLineageForNodeAsync(
        Guid tenantId, Guid hierarchyExecutionId, Guid hierarchyNodeId, CancellationToken cancellationToken = default);

    // All active lineage rows scoped to a decision session — drives session-scoped channel projection,
    // since the decision pipeline recompetes per DecisionSessionId (not per hierarchy execution).
    Task<IReadOnlyCollection<HierarchyNodeDecisionLineageDto>> GetLineageForSessionAsync(
        Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
}

// Flat read DTO mirroring the resolvable columns of POLOXI.Legal_HierarchyNodeDecisionLineage.
public sealed record HierarchyNodeDecisionLineageDto(
    Guid HierarchyNodeDecisionLineageId,
    Guid DecisionMatterId,
    Guid HierarchyExecutionId,
    Guid HierarchyNodeId,
    Guid DecisionSessionId,
    Guid? DecisionBranchId,
    Guid? DecisionCandidateId,
    string LineageSourceCode);
