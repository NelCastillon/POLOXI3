using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/platform/locales")]
public sealed class LocalesController : ControllerBase
{
    private readonly ISupportedLocaleService _service;
    public LocalesController(ISupportedLocaleService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("code/{localeCode}")]
    public async Task<IActionResult> GetByCode(string localeCode, CancellationToken cancellationToken)
    {
        var item = await _service.GetByCodeAsync(localeCode, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken));
}
