using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Integrations;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/automation")]
public sealed class AutomationFlowsController : ControllerBase
{
    private readonly IIntegrationService _service;
    public AutomationFlowsController(IIntegrationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetFlows([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.GetAutomationFlowsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetAutomationFlowByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAutomationFlowRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAutomationFlowAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAutomationFlowRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAutomationFlowAsync(id, request, cancellationToken);
        return NoContent();
    }
}
