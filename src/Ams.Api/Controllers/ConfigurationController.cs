using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/platform/configuration")]
public sealed class ConfigurationController : ControllerBase
{
    private readonly IConfigurationService _service;
    public ConfigurationController(IConfigurationService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("key")]
    public async Task<IActionResult> GetByKey([FromQuery] string settingKey, [FromQuery] string scopeCode, [FromQuery] Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByKeyAsync(settingKey, scopeCode, tenantId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? scopeCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(searchTerm, scopeCode, pageNumber, pageSize, cancellationToken));
}
