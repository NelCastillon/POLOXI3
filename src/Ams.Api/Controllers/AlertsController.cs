using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Alerts;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController : ControllerBase
{
    private readonly IAlertService _service;

    public AlertsController(IAlertService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm   = null,
        [FromQuery] string? statusCode   = null,
        [FromQuery] string? severityCode = null,
        [FromQuery] string? regionCode   = null,
        [FromQuery] Guid?   tenantId     = null,
        [FromQuery] bool?   openOnly     = null,
        [FromQuery] int     pageNumber   = 1,
        [FromQuery] int     pageSize     = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(searchTerm, statusCode, severityCode, regionCode, tenantId, openOnly, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = await _service.GetByIdAsync(id, cancellationToken);
        return alert is null ? NotFound() : Ok(alert);
    }

    [HttpGet("open-count")]
    public async Task<IActionResult> GetOpenCount(CancellationToken cancellationToken = default)
    {
        var count = await _service.GetOpenCountAsync(cancellationToken);
        return Ok(new { Count = count });
    }

    [HttpPatch("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeAlertRequest request, CancellationToken cancellationToken = default)
    {
        await _service.AcknowledgeAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveAlertRequest request, CancellationToken cancellationToken = default)
    {
        await _service.ResolveAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignAlertRequest request, CancellationToken cancellationToken = default)
    {
        await _service.AssignAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] EscalateAlertRequest request, CancellationToken cancellationToken = default)
    {
        await _service.EscalateAsync(id, request, cancellationToken);
        return NoContent();
    }
}
