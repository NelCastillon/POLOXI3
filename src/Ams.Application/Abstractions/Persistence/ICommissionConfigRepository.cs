using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CommissionConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface ICommissionConfigRepository
{
    Task<CommissionConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CommissionConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCommissionConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCommissionConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
