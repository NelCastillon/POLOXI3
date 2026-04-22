using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("ai/actions")]
public sealed class AiActionsController : ControllerBase
{
    private readonly IAiService _service;
    public AiActionsController(IAiService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetNextActions([FromQuery] Guid tenantId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.GetNextActionsAsync(tenantId, pageNumber, pageSize, cancellationToken));
}
