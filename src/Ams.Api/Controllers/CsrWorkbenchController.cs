using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/workbench/csr")]
public sealed class CsrWorkbenchController : ControllerBase
{
    private readonly ICsrWorkbenchService _service;

    public CsrWorkbenchController(ICsrWorkbenchService service)
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
}
