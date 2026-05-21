using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AcordFormsController : ControllerBase
{
    private readonly IAcordFormService _service;

    public AcordFormsController(IAcordFormService service) => _service = service;

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
    public async Task<IActionResult> Create([FromBody] CreateAcordFormRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAcordFormStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateStatusAsync(request with { AcordFormId = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/prefill")]
    public async Task<IActionResult> Prefill(Guid id, [FromBody] PrefillAcordFormRequest request, CancellationToken cancellationToken)
    {
        await _service.PrefillAsync(request with { AcordFormId = id }, cancellationToken);
        return NoContent();
    }
}
