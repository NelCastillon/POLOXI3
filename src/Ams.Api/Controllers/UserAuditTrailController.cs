using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Audit;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/user-audit-trail")]
public sealed class UserAuditTrailController : ControllerBase
{
    private readonly IUserAuditTrailService _service;

    public UserAuditTrailController(IUserAuditTrailService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] SearchUserAuditTrailRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? fromDateUtc = null,
        [FromQuery] DateTime? toDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetSummaryAsync(tenantId, fromDateUtc, toDateUtc, cancellationToken);
        return Ok(result);
    }

    [HttpGet("action-types")]
    public async Task<IActionResult> GetActionTypes([FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetActionTypesAsync(tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{auditTrailId:guid}")]
    public async Task<IActionResult> GetById(Guid auditTrailId, [FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(tenantId, auditTrailId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
