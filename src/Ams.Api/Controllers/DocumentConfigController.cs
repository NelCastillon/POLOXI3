using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentConfig;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/document-config")]
public sealed class DocumentConfigController : ControllerBase
{
    private readonly IDocumentConfigService _service;
    public DocumentConfigController(IDocumentConfigService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? kind, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchAsync(tenantId, kind, searchTerm, pageNumber, pageSize, ct));

    [HttpGet("groups")]
    public async Task<IActionResult> SearchGroups([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchGroupsAsync(tenantId, searchTerm, pageNumber, pageSize, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentConfigItemRequest request, CancellationToken ct)
    {
        var id = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateDocumentGroupRequest request, CancellationToken ct)
    {
        var id = await _service.CreateGroupAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentConfigItemRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("groups/{id:guid}")]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateDocumentGroupRequest request, CancellationToken ct)
    {
        await _service.UpdateGroupAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("groups/{id:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken ct)
    {
        await _service.DeleteGroupAsync(id, ct);
        return NoContent();
    }
}
