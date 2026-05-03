using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PortalConfig;

namespace Ams.Application.Abstractions.Services;

public interface IPortalConfigService
{
    Task<PortalConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PortalConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreatePortalConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdatePortalConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
