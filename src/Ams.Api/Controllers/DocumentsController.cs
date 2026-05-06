using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    public DocumentsController(IDocumentService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        if (item is null) return NotFound();

        var content = System.Text.Encoding.UTF8.GetBytes($"Seed document placeholder for {item.FileName}.");
        return File(content, item.ContentType ?? "application/octet-stream", item.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? categoryCode, [FromQuery] string? entityName, [FromQuery] Guid? entityId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, categoryCode, entityName, entityId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("metadata")]
    public async Task<IActionResult> UpdateMetadata([FromBody] UpdateDocumentMetadataRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateMetadataAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.ArchiveAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    // ── Version control ──────────────────────────────────────

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetVersionsAsync(id, cancellationToken));

    [HttpPost("versions")]
    public async Task<IActionResult> CreateVersion([FromBody] CreateDocumentVersionRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateVersionAsync(request, cancellationToken));

    // ── Secure sharing ───────────────────────────────────────

    [HttpGet("{id:guid}/share-links")]
    public async Task<IActionResult> GetShareLinks(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetShareLinksAsync(id, cancellationToken));

    [HttpPost("share-links")]
    public async Task<IActionResult> CreateShareLink([FromBody] CreateDocumentShareLinkRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateShareLinkAsync(request, cancellationToken));

    [HttpPost("share-links/{shareLinkId:guid}/revoke")]
    public async Task<IActionResult> RevokeShareLink(Guid shareLinkId, CancellationToken cancellationToken)
    {
        await _service.RevokeShareLinkAsync(shareLinkId, cancellationToken);
        return NoContent();
    }

    // ── Audit / access log ───────────────────────────────────

    [HttpGet("{id:guid}/access-log")]
    public async Task<IActionResult> GetAccessLog(Guid id, [FromQuery] int top = 50, CancellationToken cancellationToken = default)
        => Ok(await _service.GetAccessLogAsync(id, top, cancellationToken));

    [HttpGet("by-entity")]
    public async Task<IActionResult> GetByEntity([FromQuery] Guid tenantId, [FromQuery] string entityName, [FromQuery] Guid entityId, CancellationToken cancellationToken)
        => Ok(await _service.GetByEntityAsync(tenantId, entityName, entityId, cancellationToken));

    [HttpPut("rename")]
    public async Task<IActionResult> Rename([FromBody] RenameDocumentRequest request, CancellationToken cancellationToken)
    {
        await _service.RenameAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? deletedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(new DeleteDocumentRequest { DocumentId = id, DeletedByUserId = deletedByUserId }, cancellationToken);
        return NoContent();
    }
}
