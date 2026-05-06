using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Services;

public interface ICalendarEventService
{
    Task<CalendarEventDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CalendarEventDto>> SearchAsync(Guid tenantId, DateTime? startUtc = null, DateTime? endUtc = null, Guid? assignedToUserId = null, string? eventTypeCode = null, string? statusCode = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
