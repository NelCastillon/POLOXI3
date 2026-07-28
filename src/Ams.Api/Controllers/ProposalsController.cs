using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/proposals")]
public sealed class ProposalsController : ControllerBase
{
    private readonly ISubmissionService _service;
    public ProposalsController(ISubmissionService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!TryTenant(tenantId, out var denied)) return denied;
        var item = await _service.GetProposalByIdAsync(id, tenantId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateProposalRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var userId = GetUserId();
        var id = await _service.GenerateProposalAsync(request with { GeneratedByUserId = userId }, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id, tenantId = request.TenantId }, new { id });
    }

    private bool TryTenant(Guid requestedTenantId, out IActionResult denied)
    {
        denied = Forbid();
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        return requestedTenantId != Guid.Empty && Guid.TryParse(claim, out var authenticatedTenantId) && authenticatedTenantId == requestedTenantId;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
