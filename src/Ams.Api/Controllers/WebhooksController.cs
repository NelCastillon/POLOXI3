using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Integrations;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly IIntegrationService _service;
    public WebhooksController(IIntegrationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetWebhooks([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.GetWebhooksAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetWebhookByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebhookEndpointRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateWebhookAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWebhookEndpointRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateWebhookAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteWebhookAsync(id, cancellationToken);
        return NoContent();
    }
}
