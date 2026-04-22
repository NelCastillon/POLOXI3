using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/integrations/carriers")]
public sealed class CarrierIntegrationStatusController : ControllerBase
{
    private readonly IIntegrationService _service;
    public CarrierIntegrationStatusController(IIntegrationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetStatuses([FromQuery] Guid tenantId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.GetCarrierStatusesAsync(tenantId, pageNumber, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetCarrierStatusByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
