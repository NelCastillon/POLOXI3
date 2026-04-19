using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/system-logs")]
public sealed class SystemLogsController : ControllerBase
{
    private readonly ISystemLogService _service;

    public SystemLogsController(ISystemLogService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword = null,
        [FromQuery] string? level = null,
        [FromQuery] string? serviceName = null,
        [FromQuery] string? regionCode = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] int     pageNumber = 1,
        [FromQuery] int     pageSize   = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(keyword, level, serviceName, regionCode, correlationId, tenantId, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
