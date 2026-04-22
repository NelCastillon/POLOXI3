using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ESignController : ControllerBase
{
    private readonly IESignService _service;
    public ESignController(IESignService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByTenant([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetByTenantAsync(tenantId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendESignRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SendAsync(request, cancellationToken));

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidESignRequest request, CancellationToken cancellationToken)
    {
        await _service.VoidAsync(request with { ESignRequestId = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/remind")]
    public async Task<IActionResult> Remind(Guid id, CancellationToken cancellationToken)
    {
        await _service.RemindAsync(id, cancellationToken);
        return NoContent();
    }
}
