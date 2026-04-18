using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TimeEntriesController : ControllerBase
{
    private readonly ITimeEntryService _service;
    public TimeEntriesController(ITimeEntryService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
}
