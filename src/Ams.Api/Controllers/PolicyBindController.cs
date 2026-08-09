using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/policies")]
public sealed class PolicyBindController : ControllerBase
{
    private readonly ISubmissionService _service;
    private readonly IPolicyCreationService _policyCreationService;

    public PolicyBindController(ISubmissionService service, IPolicyCreationService policyCreationService)
    {
        _service = service;
        _policyCreationService = policyCreationService;
    }

    [HttpPost("bind")]
    [Authorize]
    public async Task<IActionResult> Bind([FromBody] BindPolicyRequest request, CancellationToken cancellationToken)
    {
        if (!CanAccess(request.TenantId, "WORKBENCH_PRODUCER", out var denied)) return denied;
        var userId = GetUserId();
        if (!userId.HasValue) return Forbid();
        var id = await _service.BindPolicyAsync(request with
        {
            RequestedByUserId = userId,
            ApprovedByUserId = null,
            BoundByUserId = null
        }, cancellationToken);
        return Ok(new { id });
    }

    [HttpGet("manual/options")]
    public async Task<IActionResult> GetManualPolicyOptions([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _policyCreationService.GetManualPolicyOptionsAsync(tenantId, cancellationToken));

    [HttpPost("/api/accounts/{accountId:guid}/policies/manual/draft")]
    public async Task<IActionResult> SaveManualPolicyDraft(Guid accountId, [FromBody] UpsertManualPolicyDraftRequest request, CancellationToken cancellationToken)
    {
        request.AccountId = accountId;
        var draft = await _policyCreationService.SaveManualPolicyDraftAsync(null, request, cancellationToken);
        return Ok(draft);
    }

    [HttpGet("/api/accounts/{accountId:guid}/policies/manual/draft/{draftId:guid}")]
    public async Task<IActionResult> GetManualPolicyDraft(Guid accountId, Guid draftId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var draft = await _policyCreationService.GetManualPolicyDraftAsync(tenantId, accountId, draftId, cancellationToken);
        return draft is null ? NotFound() : Ok(draft);
    }

    [HttpPut("/api/accounts/{accountId:guid}/policies/manual/draft/{draftId:guid}")]
    public async Task<IActionResult> UpdateManualPolicyDraft(Guid accountId, Guid draftId, [FromBody] UpsertManualPolicyDraftRequest request, CancellationToken cancellationToken)
    {
        request.AccountId = accountId;
        return Ok(await _policyCreationService.SaveManualPolicyDraftAsync(draftId, request, cancellationToken));
    }

    [HttpPost("/api/accounts/{accountId:guid}/policies/manual/validate")]
    public async Task<IActionResult> ValidateManualPolicy(Guid accountId, [FromBody] CreateManualPolicyRequest request, CancellationToken cancellationToken)
    {
        request.AccountId = accountId;
        return Ok(await _policyCreationService.ValidateManualPolicyAsync(request, cancellationToken));
    }

    [HttpGet("/api/accounts/{accountId:guid}/policies/duplicate-check")]
    public async Task<IActionResult> CheckManualPolicyDuplicate(Guid accountId, [FromQuery] Guid tenantId, [FromQuery] Guid carrierId, [FromQuery] string policyNumber, [FromQuery] DateOnly effectiveDate, [FromQuery] DateOnly expirationDate, [FromQuery] string? lineOfBusiness, CancellationToken cancellationToken)
    {
        var request = new CreateManualPolicyRequest
        {
            TenantId = tenantId,
            AccountId = accountId,
            CarrierId = carrierId,
            PolicyNumber = policyNumber,
            EffectiveDate = effectiveDate,
            ExpirationDate = expirationDate,
            LineOfBusiness = lineOfBusiness ?? string.Empty,
            ManualReasonCode = "DuplicateCheck",
            NamedInsured = "DuplicateCheck"
        };

        return Ok((await _policyCreationService.ValidateManualPolicyAsync(request, cancellationToken)).Duplicates);
    }

    [HttpPost("/api/accounts/{accountId:guid}/policies/manual")]
    public async Task<IActionResult> CreateManualPolicy(Guid accountId, [FromBody] CreateManualPolicyRequest request, CancellationToken cancellationToken)
    {
        request.AccountId = accountId;
        var result = await _policyCreationService.CreateManualPolicyAsync(request, cancellationToken);
        return Created($"/policies/{result.PolicyId}", result);
    }

    private bool CanAccess(Guid tenantId, string permission, out IActionResult denied)
    {
        denied = Forbid();
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        if (tenantId == Guid.Empty || !Guid.TryParse(claim, out var authenticatedTenantId) || authenticatedTenantId != tenantId) return false;
        return User.HasClaim("permission", permission)
            || User.HasClaim("permission", "NAV_ALL")
            || User.IsInRole("SYSTEM_ADMIN")
            || User.IsInRole("TENANT_ADMIN")
            || User.Identity?.AuthenticationType == "Development";
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
