using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.SubscriptionConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface ISubscriptionConfigRepository
{
    Task<SubscriptionConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<SubscriptionConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateSubscriptionConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateSubscriptionConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
