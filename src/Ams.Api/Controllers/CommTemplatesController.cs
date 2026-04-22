using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Communications;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CommTemplatesController : ControllerBase
{
    private readonly ICommTemplateService _service;
    public CommTemplatesController(ICommTemplateService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByTenant([FromQuery] Guid tenantId, [FromQuery] string? channel,
        [FromQuery] string? category, [FromQuery] string? status, CancellationToken cancellationToken)
        => Ok(await _service.GetByTenantAsync(tenantId, channel, category, status, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommTemplateRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommTemplateRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(request with { TemplateId = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/use")]
    public async Task<IActionResult> IncrementUsage(Guid id, CancellationToken cancellationToken)
    {
        await _service.IncrementUsageAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
