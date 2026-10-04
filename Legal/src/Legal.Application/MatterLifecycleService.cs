using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.MatterLifecycle;

namespace Legal.Application;

// Application service for Judz Matter Lifecycle. Thin orchestration over the DB-backed
// repository; keeps lifecycle concerns (operational stage context) fully separate from
// POLOXI Core decision mechanics.
public sealed class MatterLifecycleService(IMatterLifecycleRepository repository) : IMatterLifecycleService
{
    public async Task<MatterLifecycleSnapshotDto?> GetOrCreateSnapshotAsync(Guid tenantId, Guid userId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetSnapshotAsync(tenantId, decisionMatterId, cancellationToken);
        if (existing is not null)
            return existing;

        await repository.EnsureLifecycleAsync(tenantId, userId, new EnsureMatterLifecycleRequest(decisionMatterId), cancellationToken);
        return await repository.GetSnapshotAsync(tenantId, decisionMatterId, cancellationToken);
    }

    public Task<MatterLifecycleSnapshotDto?> GetSnapshotAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.GetSnapshotAsync(tenantId, decisionMatterId, cancellationToken);

    public Task<bool> PerformTransitionAsync(Guid tenantId, Guid userId, PerformMatterLifecycleTransitionRequest request, CancellationToken cancellationToken = default)
        => repository.PerformTransitionAsync(tenantId, userId, request, cancellationToken);

    public Task<IReadOnlyList<MatterLifecycleDefinitionDto>> GetDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetDefinitionsAsync(tenantId, cancellationToken);

    public Task<MatterLifecycleDefinitionDetailDto?> GetDefinitionDetailAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default)
        => repository.GetDefinitionDetailAsync(tenantId, definitionId, cancellationToken);

    public Task<Guid> SaveDefinitionAsync(Guid tenantId, Guid userId, SaveMatterLifecycleDefinitionRequest request, CancellationToken cancellationToken = default)
        => repository.SaveDefinitionAsync(tenantId, userId, request, cancellationToken);

    public Task<Guid> SaveStageAsync(Guid tenantId, Guid userId, SaveMatterLifecycleStageRequest request, CancellationToken cancellationToken = default)
        => repository.SaveStageAsync(tenantId, userId, request, cancellationToken);

    public Task<Guid> SaveTransitionAsync(Guid tenantId, Guid userId, SaveMatterLifecycleTransitionRequest request, CancellationToken cancellationToken = default)
        => repository.SaveTransitionAsync(tenantId, userId, request, cancellationToken);
}
