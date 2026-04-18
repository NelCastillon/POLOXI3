using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Governance;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/governance")]
public sealed class GovernanceController : ControllerBase
{
    private readonly IAccessRequestService _accessRequestService;
    private readonly IAccessReviewService  _accessReviewService;

    public GovernanceController(IAccessRequestService accessRequestService, IAccessReviewService accessReviewService)
    {
        _accessRequestService = accessRequestService;
        _accessReviewService  = accessReviewService;
    }

    // ── Access Requests ───────────────────────────────────────────────────────

    [HttpGet("access-requests/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _accessRequestService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("access-requests")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid    tenantId,
        [FromQuery] string? searchTerm         = null,
        [FromQuery] string? requestTypeCode    = null,
        [FromQuery] string? statusCode         = null,
        [FromQuery] Guid?   requestedForUserId = null,
        [FromQuery] Guid?   requestedByUserId  = null,
        [FromQuery] int     pageNumber         = 1,
        [FromQuery] int     pageSize           = 25,
        CancellationToken   cancellationToken  = default)
        => Ok(await _accessRequestService.SearchAsync(tenantId, searchTerm, requestTypeCode, statusCode, requestedForUserId, requestedByUserId, pageNumber, pageSize, cancellationToken));

    [HttpPost("access-requests")]
    public async Task<IActionResult> Submit([FromBody] SubmitAccessRequestRequest request, CancellationToken cancellationToken)
    {
        var id = await _accessRequestService.SubmitAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPatch("access-requests/{id:guid}/process")]
    public async Task<IActionResult> Process(Guid id, [FromBody] ProcessAccessRequestRequest request, CancellationToken cancellationToken)
    {
        await _accessRequestService.ProcessAsync(id, request, cancellationToken);
        return NoContent();
    }

    // ── Access Review Campaigns ───────────────────────────────────────────────

    [HttpGet("access-reviews")]
    public async Task<IActionResult> SearchCampaigns(
        [FromQuery] Guid    tenantId,
        [FromQuery] string? searchTerm  = null,
        [FromQuery] string? statusCode  = null,
        [FromQuery] int     pageNumber  = 1,
        [FromQuery] int     pageSize    = 25,
        CancellationToken   ct          = default)
        => Ok(await _accessReviewService.SearchCampaignsAsync(tenantId, searchTerm, statusCode, pageNumber, pageSize, ct));

    [HttpGet("access-reviews/{id:guid}")]
    public async Task<IActionResult> GetCampaignById(Guid id, CancellationToken ct)
    {
        var item = await _accessReviewService.GetCampaignByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("access-reviews")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateAccessReviewCampaignRequest request, CancellationToken ct)
    {
        var id = await _accessReviewService.CreateCampaignAsync(request, ct);
        return CreatedAtAction(nameof(GetCampaignById), new { id }, new { id });
    }

    [HttpPut("access-reviews/{id:guid}")]
    public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] UpdateAccessReviewCampaignRequest request, CancellationToken ct)
    {
        await _accessReviewService.UpdateCampaignAsync(id, request, ct);
        return NoContent();
    }

    [HttpPatch("access-reviews/{id:guid}/activate")]
    public async Task<IActionResult> ActivateCampaign(Guid id, [FromQuery] Guid changedByUserId, CancellationToken ct)
    {
        await _accessReviewService.ChangeCampaignStatusAsync(id, "Active", changedByUserId, ct);
        return NoContent();
    }

    [HttpPatch("access-reviews/{id:guid}/complete")]
    public async Task<IActionResult> CompleteCampaign(Guid id, [FromQuery] Guid changedByUserId, CancellationToken ct)
    {
        await _accessReviewService.ChangeCampaignStatusAsync(id, "Completed", changedByUserId, ct);
        return NoContent();
    }

    [HttpPatch("access-reviews/{id:guid}/cancel")]
    public async Task<IActionResult> CancelCampaign(Guid id, [FromQuery] Guid changedByUserId, CancellationToken ct)
    {
        await _accessReviewService.ChangeCampaignStatusAsync(id, "Cancelled", changedByUserId, ct);
        return NoContent();
    }

    [HttpGet("access-reviews/{id:guid}/items")]
    public async Task<IActionResult> GetItems(Guid id, CancellationToken ct)
        => Ok(await _accessReviewService.GetItemsAsync(id, ct));

    [HttpPatch("access-reviews/{id:guid}/items/{itemId:guid}/decide")]
    public async Task<IActionResult> SubmitDecision(Guid id, Guid itemId, [FromBody] SubmitReviewDecisionRequest request, CancellationToken ct)
    {
        await _accessReviewService.SubmitDecisionAsync(id, itemId, request, ct);
        return NoContent();
    }
}
