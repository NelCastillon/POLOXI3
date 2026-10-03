using Legal.Api.Security;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.ProviderPortal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Provider portal surface: attorney-approved sharing policies, firm→provider
// requests, and matter review state. All tenant/user scoped from the claims.
[ApiController]
[Route("api/legal_provider_portal")]
[Authorize(Policy = IntelligencePolicies.Search)]
public sealed class LegalProviderPortalController(IProviderPortalRepository repository) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User)
        ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User)
        ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    // ── Sharing policy ──────────────────────────────────────────────────────

    [HttpGet("matters/{matterId}/sharing-policies")]
    public async Task<IActionResult> GetSharingPolicies(Guid matterId, CancellationToken cancellationToken)
        => Ok(await repository.GetSharingPoliciesAsync(TenantId, matterId, cancellationToken));

    [HttpGet("matters/{matterId}/sharing-policies/{providerKey}")]
    public async Task<IActionResult> GetSharingPolicy(Guid matterId, string providerKey, CancellationToken cancellationToken)
        => Ok(await repository.GetSharingPolicyAsync(TenantId, matterId, providerKey, cancellationToken));

    [HttpPut("matters/{matterId}/sharing-policies")]
    public async Task<IActionResult> SaveSharingPolicy(Guid matterId, [FromBody] SaveProviderSharingPolicyRequest request, CancellationToken cancellationToken)
    {
        if (matterId != request.MatterId)
            return BadRequest("Matter id in the route must match the request body.");
        if (string.IsNullOrWhiteSpace(request.ProviderKey))
            return BadRequest("A provider key is required.");
        return Ok(await repository.UpsertSharingPolicyAsync(TenantId, ActorUserId, request, cancellationToken));
    }

    // ── Firm → provider requests ────────────────────────────────────────────

    [HttpGet("matters/{matterId}/requests")]
    public async Task<IActionResult> GetRequests(Guid matterId, [FromQuery] string? providerKey, CancellationToken cancellationToken)
        => Ok(await repository.GetRequestsAsync(TenantId, matterId, providerKey, cancellationToken));

    [HttpPost("matters/{matterId}/requests")]
    public async Task<IActionResult> CreateRequest(Guid matterId, [FromBody] CreateProviderRequestRequest request, CancellationToken cancellationToken)
    {
        if (matterId != request.MatterId)
            return BadRequest("Matter id in the route must match the request body.");
        if (string.IsNullOrWhiteSpace(request.ProviderKey) || string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("A provider key and title are required.");
        return Ok(await repository.CreateRequestAsync(TenantId, ActorUserId, request, cancellationToken));
    }

    [HttpPut("requests/{providerRequestId}/status")]
    public async Task<IActionResult> UpdateRequestStatus(Guid providerRequestId, [FromBody] UpdateProviderRequestStatusRequest request, CancellationToken cancellationToken)
    {
        if (providerRequestId != request.ProviderRequestId)
            return BadRequest("Request id in the route must match the request body.");
        var updated = await repository.UpdateRequestStatusAsync(TenantId, ActorUserId, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    // ── Review state ────────────────────────────────────────────────────────

    [HttpGet("matters/{matterId}/review-state")]
    public async Task<IActionResult> GetReviewState(Guid matterId, CancellationToken cancellationToken)
        => Ok(await repository.GetReviewStateAsync(TenantId, ActorUserId, matterId, cancellationToken));

    [HttpPost("matters/{matterId}/review-state/open")]
    public async Task<IActionResult> RecordOpened(Guid matterId, CancellationToken cancellationToken)
        => Ok(await repository.RecordMatterOpenedAsync(TenantId, ActorUserId, matterId, cancellationToken));
}
