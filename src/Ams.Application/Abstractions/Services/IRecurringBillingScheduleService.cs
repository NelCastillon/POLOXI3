using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IRecurringBillingScheduleService
{
    Task<RecurringBillingScheduleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RecurringBillingScheduleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
