using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DataConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface IDataConfigRepository
{
    Task<DataConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<DataConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateDataConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateDataConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
