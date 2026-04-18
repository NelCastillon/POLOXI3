using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    public DashboardController(IDashboardService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetKpi([FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
        => Ok(await _service.GetKpiAsync(tenantId, cancellationToken));
}
