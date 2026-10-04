using Legal.Application.Features.MatterLifecycle;

namespace Legal.Application.Abstractions.Persistence;

// Persistence for Judz Matter Lifecycle (POLOXI.Legal_Matter* tables). All stage,
// transition, and requirement configuration is database-backed. Tenant-scoped.
public interface IMatterLifecycleRepository
{
    // Composed read model for a matter's active primary lifecycle, or null if none exists yet.
    Task<MatterLifecycleSnapshotDto?> GetSnapshotAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);

    // Ensures an active primary lifecycle exists for the matter; creates one at the initial
    // stage from the resolved lifecycle version if absent. Returns the lifecycle id.
    Task<Guid> EnsureLifecycleAsync(Guid tenantId, Guid userId, EnsureMatterLifecycleRequest request, CancellationToken cancellationToken = default);

    // Performs a directed stage transition: validates the edge exists/active, closes the current
    // history row, opens a new one, appends STAGE_EXITED/STAGE_ENTERED events, and updates the
    // lifecycle's current stage. Returns false if the matter has no lifecycle; throws on invalid edge.
    Task<bool> PerformTransitionAsync(Guid tenantId, Guid userId, PerformMatterLifecycleTransitionRequest request, CancellationToken cancellationToken = default);

    // ── Configuration admin (DB-backed lifecycle authoring) ──────────────────
    Task<IReadOnlyList<MatterLifecycleDefinitionDto>> GetDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<MatterLifecycleDefinitionDetailDto?> GetDefinitionDetailAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default);
    Task<Guid> SaveDefinitionAsync(Guid tenantId, Guid userId, SaveMatterLifecycleDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<Guid> SaveStageAsync(Guid tenantId, Guid userId, SaveMatterLifecycleStageRequest request, CancellationToken cancellationToken = default);
    Task<Guid> SaveTransitionAsync(Guid tenantId, Guid userId, SaveMatterLifecycleTransitionRequest request, CancellationToken cancellationToken = default);
}
