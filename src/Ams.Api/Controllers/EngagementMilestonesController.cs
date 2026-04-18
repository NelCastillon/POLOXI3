using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Engagements;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/ops/milestones")]
public sealed class EngagementMilestonesController : ControllerBase
{
    private readonly IEngagementMilestoneService _service;
    public EngagementMilestonesController(IEngagementMilestoneService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] Guid? engagementId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, engagementId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEngagementMilestoneRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(id);
    }
}
