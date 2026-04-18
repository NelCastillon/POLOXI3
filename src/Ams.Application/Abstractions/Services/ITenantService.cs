using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface ITenantService
{
    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TenantDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
