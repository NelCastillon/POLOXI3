using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    private readonly IDocumentStorageService _storageService;

    public DocumentsController(IDocumentService service, IDocumentStorageService storageService)
    {
        _service = service;
        _storageService = storageService;
    }

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

        var download = await _storageService.DownloadAsync(item.StoragePath, cancellationToken);
        if (download is null) return NotFound();

        await _service.LogAccessAsync(item.TenantId, item.DocumentId, GetCurrentUserId(), null, "Download", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return File(download.Content, download.ContentType ?? item.ContentType ?? "application/octet-stream", item.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? categoryCode, [FromQuery] string? entityName, [FromQuery] Guid? entityId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, categoryCode, entityName, entityId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPost("upload")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentForm form, CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest("A document file is required.");

        await using var stream = form.File.OpenReadStream();
        var upload = await _storageService.UploadAsync(new DocumentStorageUploadRequest
        {
            TenantId = form.TenantId,
            FileName = form.File.FileName,
            ContentType = form.File.ContentType,
            Content = stream
        }, cancellationToken);

        var documentId = await _service.CreateAsync(new CreateDocumentRequest
        {
            TenantId = form.TenantId,
            DocumentTypeCode = form.DocumentTypeCode,
            CategoryCode = form.CategoryCode,
            FileName = form.FileName ?? form.File.FileName,
            StoragePath = upload.StoragePath,
            ContentType = upload.ContentType ?? form.File.ContentType,
            FileSizeBytes = upload.FileSizeBytes,
            EntityName = form.EntityName,
            EntityId = form.EntityId,
            Description = form.Description,
            Tags = form.Tags,
            RetentionDate = form.RetentionDate,
            UploadedByName = form.UploadedByName,
            CreatedByUserId = form.CreatedByUserId
        }, cancellationToken);

        await _service.LogAccessAsync(form.TenantId, documentId, form.CreatedByUserId ?? GetCurrentUserId(), null, "Upload", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(documentId);
    }

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

    [HttpPost("{id:guid}/versions/upload")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> UploadVersion(Guid id, [FromForm] UploadDocumentVersionForm form, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        if (item is null) return NotFound();

        if (form.File is null || form.File.Length == 0)
            return BadRequest("A document version file is required.");

        await using var stream = form.File.OpenReadStream();
        var upload = await _storageService.UploadAsync(new DocumentStorageUploadRequest
        {
            TenantId = item.TenantId,
            FileName = form.File.FileName,
            ContentType = form.File.ContentType,
            Content = stream
        }, cancellationToken);

        var versionId = await _service.CreateVersionAsync(new CreateDocumentVersionRequest
        {
            TenantId = item.TenantId,
            DocumentId = id,
            FileName = form.FileName ?? form.File.FileName,
            StoragePath = upload.StoragePath,
            ContentType = upload.ContentType ?? form.File.ContentType,
            FileSizeBytes = upload.FileSizeBytes,
            ChangeNotes = form.ChangeNotes,
            CreatedByUserId = form.CreatedByUserId
        }, cancellationToken);

        await _service.LogAccessAsync(item.TenantId, id, form.CreatedByUserId ?? GetCurrentUserId(), null, "UploadVersion", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(versionId);
    }

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
        var item = await _service.GetByIdAsync(id, cancellationToken);
        await _service.DeleteAsync(new DeleteDocumentRequest { DocumentId = id, DeletedByUserId = deletedByUserId }, cancellationToken);
        if (item is not null)
            await _storageService.DeleteAsync(item.StoragePath, cancellationToken);

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("userId");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

public sealed class UploadDocumentForm
{
    public Guid TenantId { get; set; }
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = "Other";
    public string? FileName { get; set; }
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public DateOnly? RetentionDate { get; set; }
    public string? UploadedByName { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class UploadDocumentVersionForm
{
    public string? FileName { get; set; }
    public string? ChangeNotes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public IFormFile? File { get; set; }
}
