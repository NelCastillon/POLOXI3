using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/document-exceptions")]
public sealed class DocumentExceptionsController : ControllerBase
{
    private readonly IDocumentExceptionService _service;

    public DocumentExceptionsController(IDocumentExceptionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByTenant([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetByTenantAsync(tenantId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentExceptionRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPost("{id:guid}/classify")]
    public async Task<IActionResult> Classify(Guid id, [FromBody] ClassifyDocumentExceptionRequest request, CancellationToken cancellationToken)
    {
        if (id != request.DocumentExceptionId)
        {
            return BadRequest("Route id must match request document exception id.");
        }

        await _service.ClassifyAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateDocumentExceptionStatusRequest request, CancellationToken cancellationToken)
    {
        if (id != request.DocumentExceptionId)
        {
            return BadRequest("Route id must match request document exception id.");
        }

        await _service.UpdateStatusAsync(request, cancellationToken);
        return NoContent();
    }
}
