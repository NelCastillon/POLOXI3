using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IPeriodCloseEntryRepository
{
    Task<PeriodCloseEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PeriodCloseEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePeriodCloseEntryRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdatePeriodCloseEntryRequest request, CancellationToken cancellationToken = default);
}
