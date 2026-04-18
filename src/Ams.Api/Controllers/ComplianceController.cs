using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Compliance;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/compliance")]
public sealed class ComplianceController : ControllerBase
{
    private readonly IPolicyDocumentService  _service;
    private readonly IAcknowledgementService _ackService;

    public ComplianceController(IPolicyDocumentService service, IAcknowledgementService ackService)
    {
        _service    = service;
        _ackService = ackService;
    }

    // ── Policy Documents ──────────────────────────────────────────────────────

    [HttpGet("policies")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid?   tenantId    = null,
        [FromQuery] string? searchTerm  = null,
        [FromQuery] string? typeCode    = null,
        [FromQuery] string? statusCode  = null,
        [FromQuery] bool?   isActive    = null,
        [FromQuery] int     pageNumber  = 1,
        [FromQuery] int     pageSize    = 25,
        CancellationToken   ct          = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, typeCode, statusCode, isActive, pageNumber, pageSize, ct));

    [HttpGet("policies/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("policies")]
    public async Task<IActionResult> Create([FromBody] CreatePolicyDocumentRequest request, CancellationToken ct)
    {
        var id = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("policies/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePolicyDocumentRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("policies/{id:guid}/version")]
    public async Task<IActionResult> CreateVersion(Guid id, [FromBody] VersionPolicyDocumentRequest request, CancellationToken ct)
    {
        var newId = await _service.CreateVersionAsync(id, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = newId }, new { id = newId });
    }

    [HttpPatch("policies/{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, [FromQuery] Guid? publishedByUserId, CancellationToken ct)
    {
        await _service.PublishAsync(id, publishedByUserId, ct);
        return NoContent();
    }

    [HttpPatch("policies/{id:guid}/retire")]
    public async Task<IActionResult> Retire(Guid id, [FromQuery] Guid? retiredByUserId, CancellationToken ct)
    {
        await _service.RetireAsync(id, retiredByUserId, ct);
        return NoContent();
    }

    [HttpGet("policies/{id:guid}/acknowledgements")]
    public async Task<IActionResult> GetAcknowledgements(Guid id, CancellationToken ct)
        => Ok(await _service.GetAcknowledgementsAsync(id, ct));

    [HttpGet("policies/{id:guid}/versions")]
    public async Task<IActionResult> GetVersionHistory(Guid id, CancellationToken ct)
        => Ok(await _service.GetVersionHistoryAsync(id, ct));

    [HttpGet("policies/{id:guid}/audience")]
    public async Task<IActionResult> GetAudience(Guid id, CancellationToken ct)
        => Ok(await _service.GetAudienceAsync(id, ct));

    [HttpPost("policies/{id:guid}/audience")]
    public async Task<IActionResult> AddAudienceMember(Guid id, [FromBody] AddAudienceMemberRequest request, CancellationToken ct)
    {
        var newId = await _service.AddAudienceMemberAsync(id, request, ct);
        return Ok(new { id = newId });
    }

    [HttpDelete("policies/{id:guid}/audience/{audienceId:guid}")]
    public async Task<IActionResult> RemoveAudienceMember(Guid id, Guid audienceId, CancellationToken ct)
    {
        await _service.RemoveAudienceMemberAsync(audienceId, ct);
        return NoContent();
    }

    // ── Acknowledgements ──────────────────────────────────────────────────────

    [HttpGet("acknowledgements/summary")]
    public async Task<IActionResult> GetAckSummary([FromQuery] Guid? tenantId, CancellationToken ct)
        => Ok(await _ackService.GetSummaryAsync(tenantId, ct));

    [HttpGet("acknowledgements/pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] Guid?   tenantId   = null,
        [FromQuery] Guid?   policyId   = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken   ct         = default)
        => Ok(await _ackService.GetPendingAsync(tenantId, policyId, searchTerm, ct));

    [HttpGet("acknowledgements/overdue")]
    public async Task<IActionResult> GetOverdue(
        [FromQuery] Guid?   tenantId   = null,
        [FromQuery] Guid?   policyId   = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken   ct         = default)
        => Ok(await _ackService.GetOverdueAsync(tenantId, policyId, searchTerm, ct));

    [HttpGet("acknowledgements")]
    public async Task<IActionResult> SearchAcknowledged(
        [FromQuery] Guid?   tenantId   = null,
        [FromQuery] Guid?   policyId   = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int     pageNumber = 1,
        [FromQuery] int     pageSize   = 25,
        CancellationToken   ct         = default)
        => Ok(await _ackService.SearchAcknowledgedAsync(tenantId, policyId, searchTerm, pageNumber, pageSize, ct));
}
