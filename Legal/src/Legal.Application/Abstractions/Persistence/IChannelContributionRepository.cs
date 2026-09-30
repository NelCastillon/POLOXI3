using Legal.Application.Features.Intelligence.Decision.Channels;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Persistence for Decision Channel Contributions (migration 0367; POLOXI.Legal_ChannelContribution).
//
// Qualitative source-truth ONLY: a contribution records that a channel asserted a relation against a
// run-scoped authoritative hierarchy node, with verification state + provenance. It carries NO numeric
// score — POLOXI Wide2 remains the sole decision engine. Rows are append-only source-truth; the derived
// node signal/state is persisted separately by the existing DecisionSupportSignal path.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IChannelContributionRepository
{
    // Persists one or more contributions (batch insert). No-op when the collection is empty.
    Task SaveContributionsAsync(IReadOnlyCollection<ChannelContributionPersistence> contributions, CancellationToken cancellationToken = default);

    // All contributions bound to a single run-scoped hierarchy node — the node's provenance surface.
    Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForNodeAsync(Guid tenantId, Guid hierarchyNodeId, CancellationToken cancellationToken = default);

    // All contributions for a matter's authoritative hierarchy execution.
    Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForExecutionAsync(Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default);

    // All contributions recorded against a matter (across executions), newest first.
    Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
}
