using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Tenants;

namespace Ams.Application.Abstractions.Services;

public interface ITenantDomainService
{
    Task<TenantDomainDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TenantDomainDto>> SearchByTenantAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<TenantDomainDto>> SearchAllAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTenantDomainRequest request, CancellationToken cancellationToken = default);
    Task UpdateRedirectAsync(Guid id, string? redirectTarget, string? notes = null, CancellationToken cancellationToken = default);
    Task SetPrimaryAsync(Guid tenantId, Guid domainId, CancellationToken cancellationToken = default);
    Task VerifyAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
