using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/integrations")]
public sealed class IntegrationCatalogController : ControllerBase
{
    private readonly IIntegrationService _service;
    public IntegrationCatalogController(IIntegrationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetCatalog([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.GetCatalogAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetCatalogItemByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
