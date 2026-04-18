using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/ops/activities")]
public sealed class OperationalActivitiesController : ControllerBase
{
    private readonly IOperationalActivityService _service;
    public OperationalActivitiesController(IOperationalActivityService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] Guid? accountId, [FromQuery] Guid? engagementId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, accountId, engagementId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOperationalActivityRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(id);
    }
}
