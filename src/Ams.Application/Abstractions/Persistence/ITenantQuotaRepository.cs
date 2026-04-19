using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.TenantQuotas;

namespace Ams.Application.Abstractions.Persistence;

public interface ITenantQuotaRepository
{
    Task<PagedResult<TenantQuotaDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<TenantQuotaDto?> GetByTenantMetricAsync(Guid tenantId, string metricTypeCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantQuotaDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> UpsertAsync(Guid tenantId, UpsertTenantQuotaRequest request, CancellationToken cancellationToken = default);
    Task OverrideLimitAsync(Guid tenantQuotaId, OverrideLimitRequest request, CancellationToken cancellationToken = default);
    Task ResetOverrideAsync(Guid tenantQuotaId, ResetOverrideRequest request, CancellationToken cancellationToken = default);
    Task NotifyTenantAsync(Guid tenantQuotaId, NotifyTenantQuotaRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid tenantQuotaId, CancellationToken cancellationToken = default);
}
