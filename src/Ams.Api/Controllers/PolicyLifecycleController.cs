using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyLifecycle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/policy-lifecycle")]
public sealed class PolicyLifecycleController : ControllerBase
{
    private readonly IPolicyLifecycleService _service;

    public PolicyLifecycleController(IPolicyLifecycleService service)
    {
        _service = service;
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => CanAccess(tenantId) ? Ok(await _service.GetOptionsAsync(tenantId, cancellationToken)) : Forbid();

    [HttpGet("workbench")]
    public async Task<IActionResult> GetWorkbench([FromQuery] Guid tenantId, [FromQuery] string? mode, CancellationToken cancellationToken)
        => CanAccess(tenantId) ? Ok(await _service.GetWorkbenchAsync(tenantId, mode, cancellationToken)) : Forbid();

    [HttpGet("policies/{policyId:guid}")]
    public async Task<IActionResult> GetDetail([FromQuery] Guid tenantId, Guid policyId, CancellationToken cancellationToken)
    {
        if (!CanAccess(tenantId)) return Forbid();
        var detail = await _service.GetDetailAsync(tenantId, policyId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("policies/{policyId:guid}/workspace")]
    public async Task<IActionResult> GetWorkspace([FromQuery] Guid tenantId, Guid policyId, CancellationToken cancellationToken)
    {
        if (!CanAccess(tenantId)) return Forbid();
        var workspace = await _service.GetWorkspaceAsync(tenantId, policyId, cancellationToken);
        return workspace is null ? NotFound() : Ok(workspace);
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreatePolicyLifecycleTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        request.RequestedByUserId = GetAuthenticatedUserId();
        return Ok(new { Id = await _service.CreateTransactionAsync(request, cancellationToken) });
    }

    [HttpPut("transactions/{policyTransactionId:guid}/status")]
    public async Task<IActionResult> TransitionTransaction(Guid policyTransactionId, [FromBody] TransitionPolicyLifecycleTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        request.ChangedByUserId = GetAuthenticatedUserId();
        await _service.TransitionTransactionAsync(policyTransactionId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("policies/{policyId:guid}/activities")]
    public async Task<IActionResult> CreateActivity(Guid policyId, [FromBody] CreatePolicyServicingActivityRequest request, CancellationToken cancellationToken)
    {
        if (request.PolicyId != policyId) return BadRequest("Route policy does not match request policy.");
        if (!CanManage(request.TenantId)) return Forbid();
        request.PerformedByUserId = GetAuthenticatedUserId();
        return Ok(await _service.CreateActivityAsync(request, cancellationToken));
    }

    [HttpPost("policies/{policyId:guid}/communications")]
    public async Task<IActionResult> SendCommunication(Guid policyId, [FromBody] SendPolicyCommunicationRequest request, CancellationToken cancellationToken)
    {
        if (request.PolicyId != policyId) return BadRequest("Route policy does not match request policy.");
        if (!CanManage(request.TenantId)) return Forbid();
        request.SentByUserId = GetAuthenticatedUserId();
        return Ok(await _service.SendCommunicationAsync(request, cancellationToken));
    }

    private bool CanAccess(Guid tenantId)
    {
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        return tenantId != Guid.Empty
            && Guid.TryParse(claim, out var authenticatedTenantId)
            && authenticatedTenantId == tenantId
            && (User.HasClaim("permission", "POLICY_VIEW")
                || User.HasClaim("permission", "NAV_ALL")
                || User.IsInRole("SYSTEM_ADMIN")
                || User.IsInRole("TENANT_ADMIN")
                || User.Identity?.AuthenticationType == "Development");
    }

    private bool CanManage(Guid tenantId)
        => CanAccess(tenantId)
            && (User.HasClaim("permission", "POLICY_MANAGE")
                || User.HasClaim("permission", "POLICY_EDIT")
                || User.HasClaim("permission", "NAV_ALL")
                || User.IsInRole("SYSTEM_ADMIN")
                || User.IsInRole("TENANT_ADMIN")
                || User.Identity?.AuthenticationType == "Development");

    private Guid? GetAuthenticatedUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("user_id")
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("UserId");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
