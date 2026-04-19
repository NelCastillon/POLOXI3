using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantFeatures;

namespace Ams.Application;

public sealed class TenantFeatureService : ITenantFeatureService
{
    private readonly ITenantFeatureRepository _repository;

    public TenantFeatureService(ITenantFeatureRepository repository) => _repository = repository;

    public Task<IReadOnlyList<TenantFeatureDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantAsync(tenantId, cancellationToken);

    public Task UpsertOverrideAsync(Guid tenantId, OverrideTenantFeatureRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertOverrideAsync(tenantId, request, cancellationToken);

    public Task SetEnabledAsync(Guid tenantId, string featureCode, bool enabled, CancellationToken cancellationToken = default)
        => _repository.SetEnabledAsync(tenantId, featureCode, enabled, cancellationToken);

    public Task ResetToDefaultAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken = default)
        => _repository.ResetToDefaultAsync(tenantId, featureCode, cancellationToken);
}
