using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PortalInvites;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/client/portal-invites")]
public sealed class PortalInvitesController : ControllerBase
{
    private readonly IPortalInviteService _service;

    public PortalInvitesController(IPortalInviteService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePortalInviteRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
}
