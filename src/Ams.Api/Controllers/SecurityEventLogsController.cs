using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/security-event-logs")]
public sealed class SecurityEventLogsController : ControllerBase
{
    private readonly ISecurityEventLogService _service;

    public SecurityEventLogsController(ISecurityEventLogService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid?   tenantId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? eventTypeCode = null,
        [FromQuery] int     pageNumber = 1,
        [FromQuery] int     pageSize   = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(tenantId, searchTerm, eventTypeCode, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }
}
