using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;

namespace Ams.Application.Abstractions.Persistence;

public interface ITimeEntryRepository
{
    Task<TimeEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TimeEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateTimeEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
