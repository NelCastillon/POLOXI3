using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/ops/calendar-events")]
public sealed class CalendarEventsController : ControllerBase
{
    private readonly ICalendarEventService _service;

    public CalendarEventsController(ICalendarEventService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] DateTime? startUtc, [FromQuery] DateTime? endUtc, [FromQuery] Guid? assignedToUserId, [FromQuery] string? eventTypeCode, [FromQuery] string? statusCode, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, startUtc, endUtc, assignedToUserId, eventTypeCode, statusCode, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCalendarEventRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
