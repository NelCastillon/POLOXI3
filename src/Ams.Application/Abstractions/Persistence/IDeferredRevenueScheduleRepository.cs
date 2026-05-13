using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IDeferredRevenueScheduleRepository
{
    Task<DeferredRevenueScheduleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DeferredRevenueScheduleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDeferredRevenueScheduleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateDeferredRevenueScheduleRequest request, CancellationToken cancellationToken = default);
}
