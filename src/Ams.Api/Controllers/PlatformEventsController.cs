using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PlatformEvents;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/platform-events")]
public sealed class PlatformEventsController : ControllerBase
{
    private readonly IPlatformEventService _service;

    public PlatformEventsController(IPlatformEventService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm       = null,
        [FromQuery] string? eventTypeCode    = null,
        [FromQuery] string? processingStatus = null,
        [FromQuery] string? sourceService    = null,
        [FromQuery] Guid?   tenantId         = null,
        [FromQuery] string? correlationId    = null,
        [FromQuery] int     pageNumber       = 1,
        [FromQuery] int     pageSize         = 25,
        CancellationToken cancellationToken  = default)
    {
        var result = await _service.SearchAsync(searchTerm, eventTypeCode, processingStatus, sourceService, tenantId, correlationId, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var evt = await _service.GetByIdAsync(id, cancellationToken);
        return evt is null ? NotFound() : Ok(evt);
    }

    [HttpPatch("{id:guid}/replay")]
    public async Task<IActionResult> Replay(Guid id, [FromBody] ReplayPlatformEventRequest request, CancellationToken cancellationToken = default)
    {
        await _service.ReplayAsync(id, cancellationToken);
        return NoContent();
    }
}
