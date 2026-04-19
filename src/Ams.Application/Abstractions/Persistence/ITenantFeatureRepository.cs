using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantFeatures;

namespace Ams.Application.Abstractions.Persistence;

public interface ITenantFeatureRepository
{
    Task<IReadOnlyList<TenantFeatureDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpsertOverrideAsync(Guid tenantId, OverrideTenantFeatureRequest request, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(Guid tenantId, string featureCode, bool enabled, CancellationToken cancellationToken = default);
    Task ResetToDefaultAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken = default);
}
