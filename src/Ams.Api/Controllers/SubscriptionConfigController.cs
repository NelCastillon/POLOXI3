using Ams.Application.Abstractions.Services;
using Ams.Application.Features.SubscriptionConfig;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/subscription-config")]
public sealed class SubscriptionConfigController : ControllerBase
{
    private readonly ISubscriptionConfigService _service;
    public SubscriptionConfigController(ISubscriptionConfigService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string kind, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchAsync(tenantId, kind, searchTerm, pageNumber, pageSize, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionConfigItemRequest request, CancellationToken ct)
    {
        var id = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionConfigItemRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
