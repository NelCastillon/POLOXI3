using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/workbench/marketing")]
public sealed class MarketingWorkbenchController : ControllerBase
{
    private readonly IMarketingWorkbenchService _service;

    public MarketingWorkbenchController(IMarketingWorkbenchService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid? userId,
        [FromQuery] bool teamScope = false,
        [FromQuery] string? branchId = null,
        [FromQuery] string? teamId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetWorkbenchAsync(tenantId, userId, teamScope, branchId, teamId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("content/{id:guid}/approve")]
    public async Task<IActionResult> ApproveContent(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _service.ApproveContentAsync(tenantId, id, cancellationToken);
        return NoContent();
    }
}
