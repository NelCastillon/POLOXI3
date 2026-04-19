using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Tenants;

namespace Ams.Application.Abstractions.Persistence;

public interface ITenantRepository
{
    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TenantDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken = default);
    Task SetStatusAsync(Guid id, string statusCode, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
