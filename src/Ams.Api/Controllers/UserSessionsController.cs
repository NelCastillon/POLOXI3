using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/platform/sessions")]
public sealed class UserSessionsController : ControllerBase
{
    private readonly IUserSessionService _service;
    public UserSessionsController(IUserSessionService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] Guid? userId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, userId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromQuery] string? reason, CancellationToken cancellationToken = default)
    {
        await _service.RevokeAsync(id, reason, cancellationToken);
        return NoContent();
    }

    [HttpPost("revoke-all")]
    public async Task<IActionResult> RevokeAll([FromQuery] Guid tenantId, [FromQuery] Guid? userId, [FromQuery] string? reason, CancellationToken cancellationToken = default)
    {
        await _service.RevokeAllAsync(tenantId, userId, reason, cancellationToken);
        return NoContent();
    }
}
