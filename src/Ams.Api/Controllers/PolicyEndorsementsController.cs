using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyEndorsements;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/policy-endorsements")]
public sealed class PolicyEndorsementsController : ControllerBase
{
    private readonly IPolicyEndorsementService _service;

    public PolicyEndorsementsController(IPolicyEndorsementService service) => _service = service;

    [HttpGet("center")]
    public async Task<IActionResult> GetCenter([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.CanViewPolicy(User, tenantId)
            ? Ok(await _service.GetCenterAsync(tenantId, cancellationToken))
            : Forbid();

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.CanViewPolicy(User, tenantId)
            ? Ok(await _service.GetOptionsAsync(tenantId, cancellationToken))
            : Forbid();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanViewPolicy(User, tenantId)) return Forbid();
        var item = await _service.GetDetailAsync(tenantId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/workflow")]
    public async Task<IActionResult> GetWorkflowDetail(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var item = await _service.GetWorkflowDetailAsync(tenantId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("policies/{policyId:guid}/workspace")]
    public async Task<IActionResult> GetPolicyWorkspace(Guid policyId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var item = await _service.GetPolicyWorkspaceAsync(tenantId, policyId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePolicyEndorsementRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        request.CreatedByUserId = AuthenticatedRequestContext.GetUserId(User);
        return Ok(new { Id = await _service.CreateAsync(request, cancellationToken) });
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreatePolicyEndorsementTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_CREATE")) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!actorUserId.HasValue) return Forbid();

        request.CreatedByUserId = actorUserId;
        request.AllowBackdate = AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_BACKDATE");
        var id = await _service.CreateTransactionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetWorkflowDetail), new { id, tenantId = request.TenantId }, new { Id = id });
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SavePolicyEndorsementDraftRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_EDIT_DRAFT")) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!actorUserId.HasValue) return Forbid();

        request.ModifiedByUserId = actorUserId;
        request.AllowBackdate = AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_BACKDATE");
        await _service.SaveDraftAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/transitions")]
    public async Task<IActionResult> Transition(Guid id, [FromBody] TransitionPolicyEndorsementRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanAccessPolicyEndorsementWorkflow(User, request.TenantId)) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!actorUserId.HasValue) return Forbid();

        request.ActorUserId = actorUserId;
        request.GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User);
        await _service.TransitionAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/approvals/{approvalId:guid}/decision")]
    public async Task<IActionResult> DecideApproval(Guid id, Guid approvalId, [FromBody] DecidePolicyEndorsementApprovalRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_APPROVE")) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!actorUserId.HasValue) return Forbid();

        request.ActorUserId = actorUserId;
        request.GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User);
        await _service.DecideApprovalAsync(id, approvalId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reversal")]
    public async Task<IActionResult> Reverse(Guid id, [FromBody] ReversePolicyEndorsementRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_REVERSE")) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!actorUserId.HasValue) return Forbid();

        request.ActorUserId = actorUserId;
        request.GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User);
        request.AllowBackdate = AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_BACKDATE");
        var reversalId = await _service.ReverseAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetWorkflowDetail), new { id = reversalId, tenantId = request.TenantId }, new { Id = reversalId });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromQuery] Guid tenantId, [FromBody] UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageEndorsementAsync(tenantId, id, cancellationToken)) return Forbid();
        request.ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User);
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] Guid tenantId, [FromBody] UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageEndorsementAsync(tenantId, id, cancellationToken)) return Forbid();
        request.ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User);
        await _service.UpdateStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("activities")]
    public async Task<IActionResult> AddActivity([FromQuery] Guid tenantId, [FromBody] AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageEndorsementAsync(tenantId, request.EndorsementId, cancellationToken)) return Forbid();
        request.CreatedByUserId = AuthenticatedRequestContext.GetUserId(User);
        return Ok(new { Id = await _service.AddActivityAsync(request, cancellationToken) });
    }

    [HttpPost("deltas")]
    public async Task<IActionResult> UpsertDelta([FromQuery] Guid tenantId, [FromBody] UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageEndorsementAsync(tenantId, request.EndorsementId, cancellationToken)) return Forbid();
        request.CreatedByUserId = AuthenticatedRequestContext.GetUserId(User);
        return Ok(new { Id = await _service.UpsertDeltaAsync(request, cancellationToken) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, [FromQuery] Guid tenantId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        if (!await CanManageEndorsementAsync(tenantId, id, cancellationToken)) return Forbid();
        await _service.ArchiveAsync(id, AuthenticatedRequestContext.GetUserId(User), cancellationToken);
        return NoContent();
    }

    private async Task<bool> CanManageEndorsementAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.CanManagePolicy(User, tenantId)
            && await _service.GetDetailAsync(tenantId, endorsementId, cancellationToken) is not null;
}
