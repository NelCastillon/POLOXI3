using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Communications;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    public NotificationsController(INotificationService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("templates")]
    public async Task<IActionResult> SearchTemplates([FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchTemplatesAsync(searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> SetRead(Guid id, [FromQuery] bool isRead = true, CancellationToken cancellationToken = default)
    {
        await _service.SetReadAsync(id, isRead, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromQuery] string statusCode, CancellationToken cancellationToken = default)
    {
        await _service.SetStatusAsync(id, statusCode, cancellationToken);
        return NoContent();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead([FromQuery] Guid tenantId, [FromQuery] Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        await _service.MarkAllReadAsync(tenantId, recipientUserId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("read")]
    public async Task<IActionResult> DeleteRead([FromQuery] Guid tenantId, [FromQuery] Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        await _service.DeleteReadAsync(tenantId, recipientUserId, cancellationToken);
        return NoContent();
    }
}
