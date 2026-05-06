using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AiConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface IAiConfigRepository
{
    Task<AiConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AiConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateAiConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateAiConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
