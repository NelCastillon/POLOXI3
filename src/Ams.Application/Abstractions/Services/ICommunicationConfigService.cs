using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CommunicationConfig;

namespace Ams.Application.Abstractions.Services;

public interface ICommunicationConfigService
{
    Task<CommunicationConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CommunicationConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCommunicationConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCommunicationConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
