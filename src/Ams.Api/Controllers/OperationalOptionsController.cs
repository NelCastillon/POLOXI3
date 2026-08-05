using Ams.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/ops/options")]
public sealed class OperationalOptionsController : ControllerBase
{
    private readonly IOperationalOptionRepository _repository;

    public OperationalOptionsController(IOperationalOptionRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetByGroup([FromQuery] Guid tenantId, [FromQuery] string optionGroupCode, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(optionGroupCode))
            return BadRequest("tenantId and optionGroupCode are required.");

        return Ok(await _repository.GetByGroupAsync(tenantId, optionGroupCode, cancellationToken));
    }
}
