using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Leads;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/workbench/producer")]
public sealed class ProducerWorkbenchController : ControllerBase
{
    private readonly IProducerWorkbenchService _service;

    public ProducerWorkbenchController(IProducerWorkbenchService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid tenantId, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        var result = await _service.GetWorkbenchAsync(tenantId, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("next-lead-number")]
    public async Task<IActionResult> NextLeadNumber([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var number = await _service.GetNextLeadNumberAsync(tenantId, cancellationToken);
        return Ok(number);
    }

    [HttpPost("log-contact")]
    public async Task<IActionResult> LogContact([FromQuery] Guid tenantId, [FromQuery] Guid itemId, [FromQuery] string itemType, CancellationToken cancellationToken)
    {
        await _service.LogContactAsync(tenantId, itemId, itemType, cancellationToken);
        return NoContent();
    }
}
