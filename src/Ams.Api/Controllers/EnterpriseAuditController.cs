using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Audit;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/enterprise-audit")]
public sealed class EnterpriseAuditController : ControllerBase
{
    private readonly IEnterpriseAuditService _service;

    public EnterpriseAuditController(IEnterpriseAuditService service)
    {
        _service = service;
    }

    [HttpPost("events")]
    public async Task<IActionResult> Log([FromBody] LogEnterpriseAuditEventRequest request, CancellationToken cancellationToken = default)
    {
        var id = await _service.LogAsync(request, cancellationToken);
        return Ok(id);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] SearchEnterpriseAuditEventsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetSummaryAsync(tenantId, fromUtc, toUtc, cancellationToken);
        return Ok(result);
    }

    [HttpGet("alerts/open")]
    public async Task<IActionResult> GetOpenAlerts([FromQuery] Guid tenantId, [FromQuery] int top = 10, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetOpenAlertsAsync(tenantId, top, cancellationToken);
        return Ok(result);
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetOptionsAsync(tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("capabilities")]
    public async Task<IActionResult> GetCapabilities([FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetCapabilitiesAsync(tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{auditEventId:guid}")]
    public async Task<IActionResult> GetById(Guid auditEventId, [FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetByIdAsync(tenantId, auditEventId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
