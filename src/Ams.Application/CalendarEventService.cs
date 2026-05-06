using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class CalendarEventService : ICalendarEventService
{
    private readonly ICalendarEventRepository _repository;

    public CalendarEventService(ICalendarEventRepository repository) => _repository = repository;

    public Task<CalendarEventDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<CalendarEventDto>> SearchAsync(Guid tenantId, DateTime? startUtc = null, DateTime? endUtc = null, Guid? assignedToUserId = null, string? eventTypeCode = null, string? statusCode = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, startUtc, endUtc, assignedToUserId, eventTypeCode, statusCode, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
    public Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);
}
