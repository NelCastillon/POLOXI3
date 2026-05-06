using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/workbench/operations")]
public sealed class OperationsWorkbenchController : ControllerBase
{
    private readonly IOperationsWorkbenchService _service;

    public OperationsWorkbenchController(IOperationsWorkbenchService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid? userId,
        [FromQuery] bool myItemsOnly = false,
        [FromQuery] string? assigneeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetWorkbenchAsync(tenantId, userId, myItemsOnly, assigneeFilter, cancellationToken);
        return Ok(result);
    }

    [HttpPost("items/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _service.RetryItemAsync(tenantId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("items/{id:guid}/skip")]
    public async Task<IActionResult> Skip(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _service.SkipAutomationStepAsync(tenantId, id, cancellationToken);
        return NoContent();
    }
}
