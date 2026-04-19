using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UsageController : ControllerBase
{
    private readonly IUsageService _service;

    public UsageController(IUsageService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetPlatformUsage(CancellationToken cancellationToken = default)
        => Ok(await _service.GetPlatformUsageAsync(cancellationToken));

    [HttpGet("events")]
    public async Task<IActionResult> GetUsageEvents(
        [FromQuery] Guid?   tenantId      = null,
        [FromQuery] string? metricType    = null,
        [FromQuery] string? sourceService = null,
        [FromQuery] int     pageNumber    = 1,
        [FromQuery] int     pageSize      = 50,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetUsageEventsAsync(tenantId, metricType, sourceService, pageNumber, pageSize, cancellationToken));
}
