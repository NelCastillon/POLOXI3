using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("analytics/ai")]
public sealed class AiInsightsController : ControllerBase
{
    private readonly IAiService _service;
    public AiInsightsController(IAiService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetInsights([FromQuery] Guid tenantId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.GetInsightsAsync(tenantId, pageNumber, pageSize, cancellationToken));
}
