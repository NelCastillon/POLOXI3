using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
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

    [HttpGet("renewal-calls")]
    public async Task<IActionResult> GetRenewalCalls([FromQuery] Guid tenantId, [FromQuery] Guid? userId, [FromQuery] string? statusCode, CancellationToken cancellationToken)
    {
        var result = await _service.GetRenewalCallsAsync(tenantId, userId, statusCode, cancellationToken);
        return Ok(result);
    }

    [HttpGet("renewal-calls/{renewalKey:guid}")]
    public async Task<IActionResult> GetRenewalCall(Guid renewalKey, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await _service.GetRenewalCallAsync(tenantId, renewalKey, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("renewal-calls/{renewalCallId:guid}")]
    public async Task<IActionResult> UpdateRenewalCall(Guid renewalCallId, [FromBody] UpdateProducerRenewalCallRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _service.UpdateRenewalCallAsync(renewalCallId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("next-lead-number")]
    public async Task<IActionResult> NextLeadNumber([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var number = await _service.GetNextLeadNumberAsync(tenantId, cancellationToken);
        return Ok(number);
    }

    [HttpPost("log-contact")]
    public async Task<IActionResult> LogContact([FromBody] ProducerWorkbenchLogContactRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _service.LogContactAsync(request, cancellationToken);
        return NoContent();
    }
}
