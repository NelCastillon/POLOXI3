using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Integrations;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/downloads/exceptions")]
public sealed class DownloadExceptionsController : ControllerBase
{
    private readonly IIntegrationService _service;
    public DownloadExceptionsController(IIntegrationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetExceptions([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.GetDownloadExceptionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDownloadExceptionByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCarrierDownloadExceptionRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateCarrierDownloadExceptionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { Id = id });
    }

    [HttpPost("{id:guid}/manual-match")]
    public async Task<IActionResult> ManualMatch(Guid id, [FromBody] ManualCarrierDownloadMatchRequest request, CancellationToken cancellationToken)
    {
        await _service.ManualMatchCarrierDownloadExceptionAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveDownloadExceptionRequest request, CancellationToken cancellationToken)
    {
        await _service.ResolveDownloadExceptionAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        await _service.RetryDownloadExceptionAsync(id, cancellationToken);
        return NoContent();
    }
}
