using Legal.Application.Features.MatterLifecycle;

namespace Legal.Application.Abstractions.Services;

// Application service for Judz Matter Lifecycle. Supplies operational stage context to the
// Case Command Center and Decision Intelligence; never mutates POLOXI Core decision state.
public interface IMatterLifecycleService
{
    // Returns the matter's lifecycle snapshot, creating a default lifecycle on first access.
    Task<MatterLifecycleSnapshotDto?> GetOrCreateSnapshotAsync(Guid tenantId, Guid userId, Guid decisionMatterId, CancellationToken cancellationToken = default);

    // Reads the snapshot without creating a lifecycle (null if none exists).
    Task<MatterLifecycleSnapshotDto?> GetSnapshotAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);

    // Performs a directed stage transition; throws InvalidMatterLifecycleTransitionException on an invalid edge.
    Task<bool> PerformTransitionAsync(Guid tenantId, Guid userId, PerformMatterLifecycleTransitionRequest request, CancellationToken cancellationToken = default);

    // ── Configuration admin (DB-backed lifecycle authoring) ──────────────────
    Task<IReadOnlyList<MatterLifecycleDefinitionDto>> GetDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<MatterLifecycleDefinitionDetailDto?> GetDefinitionDetailAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default);
    Task<Guid> SaveDefinitionAsync(Guid tenantId, Guid userId, SaveMatterLifecycleDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<Guid> SaveStageAsync(Guid tenantId, Guid userId, SaveMatterLifecycleStageRequest request, CancellationToken cancellationToken = default);
    Task<Guid> SaveTransitionAsync(Guid tenantId, Guid userId, SaveMatterLifecycleTransitionRequest request, CancellationToken cancellationToken = default);
}
