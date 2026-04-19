using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.TenantQuotas;

namespace Ams.Application;

public sealed class TenantQuotaService : ITenantQuotaService
{
    private readonly ITenantQuotaRepository _repository;

    public TenantQuotaService(ITenantQuotaRepository repository) => _repository = repository;

    public Task<PagedResult<TenantQuotaDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, statusCode, pageNumber, pageSize, cancellationToken);

    public Task<IReadOnlyList<TenantQuotaDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantAsync(tenantId, cancellationToken);

    public Task<Guid> UpsertAsync(Guid tenantId, UpsertTenantQuotaRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertAsync(tenantId, request, cancellationToken);

    public Task OverrideLimitAsync(Guid tenantQuotaId, OverrideLimitRequest request, CancellationToken cancellationToken = default)
        => _repository.OverrideLimitAsync(tenantQuotaId, request, cancellationToken);

    public Task ResetOverrideAsync(Guid tenantQuotaId, ResetOverrideRequest request, CancellationToken cancellationToken = default)
        => _repository.ResetOverrideAsync(tenantQuotaId, request, cancellationToken);

    public Task NotifyTenantAsync(Guid tenantQuotaId, NotifyTenantQuotaRequest request, CancellationToken cancellationToken = default)
        => _repository.NotifyTenantAsync(tenantQuotaId, request, cancellationToken);

    public Task DeleteAsync(Guid tenantQuotaId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(tenantQuotaId, cancellationToken);
}
