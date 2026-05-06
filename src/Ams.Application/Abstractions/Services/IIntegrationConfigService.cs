using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.IntegrationConfig;

namespace Ams.Application.Abstractions.Services;

public interface IIntegrationConfigService
{
    Task<IntegrationConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<IntegrationConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateIntegrationConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateIntegrationConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
