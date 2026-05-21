using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/document-packets")]
public sealed class DocumentPacketsController : ControllerBase
{
    private readonly IDocumentPacketService _service;

    public DocumentPacketsController(IDocumentPacketService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByTenant([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetByTenantAsync(tenantId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var packet = await _service.GetByIdAsync(id, cancellationToken);
        return packet is null ? NotFound() : Ok(packet);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentPacketRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPost("{id:guid}/documents")]
    public async Task<IActionResult> AddDocument(Guid id, [FromBody] AddDocumentPacketDocumentRequest request, CancellationToken cancellationToken)
    {
        if (id != request.DocumentPacketId) return BadRequest("Route id must match request packet id.");
        return Ok(await _service.AddDocumentAsync(request, cancellationToken));
    }

    [HttpDelete("documents/{packetDocumentId:guid}")]
    public async Task<IActionResult> RemoveDocument(Guid packetDocumentId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.RemoveDocumentAsync(packetDocumentId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/documents/reorder")]
    public async Task<IActionResult> ReorderDocuments(Guid id, [FromBody] ReorderDocumentPacketDocumentsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.DocumentPacketId) return BadRequest("Route id must match request packet id.");
        await _service.ReorderDocumentsAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, [FromBody] SendDocumentPacketRequest request, CancellationToken cancellationToken)
    {
        if (id != request.DocumentPacketId) return BadRequest("Route id must match request packet id.");
        await _service.SendAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateDocumentPacketStatusRequest request, CancellationToken cancellationToken)
    {
        if (id != request.DocumentPacketId) return BadRequest("Route id must match request packet id.");
        await _service.UpdateStatusAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
