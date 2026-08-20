using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
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
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var center = await _service.GetCenterAsync(tenantId, cancellationToken);
        if (!CanViewFinancial(tenantId)) RedactFinancial(center);
        return Ok(center);
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")
            ? Ok(await _service.GetOptionsAsync(tenantId, cancellationToken))
            : Forbid();

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog([FromQuery] Guid tenantId, [FromQuery] string? lineOfBusinessCode, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")
            ? Ok(await _service.GetCatalogAsync(tenantId, lineOfBusinessCode, cancellationToken))
            : Forbid();

    [HttpGet("catalog/{typeCode}")]
    public async Task<IActionResult> GetTypeCatalog(string typeCode, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var item = await _service.GetTypeCatalogAsync(tenantId, typeCode, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/route-preview")]
    public async Task<IActionResult> GetRoutePreview(Guid id, [FromQuery] Guid tenantId, [FromQuery] string purpose, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanAccessPolicyEndorsementWorkflow(User, tenantId)) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!actorUserId.HasValue) return Forbid();
        var preview = await _service.GetRoutePreviewAsync(tenantId, id, purpose, actorUserId.Value, cancellationToken);
        return Ok(preview ?? new PolicyEndorsementRoutePreviewDto
        {
            IsResolved = false,
            RoutePurposeCode = purpose,
            ResolutionMessage = "No eligible recipient could be resolved from the active tenant workflow route. Review the route, role assignment, and required permission configuration."
        });
    }

    [HttpGet("approval-inbox")]
    public async Task<IActionResult> GetApprovalInbox([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_APPROVE")) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        return actorUserId.HasValue ? Ok(await _service.GetApprovalInboxAsync(tenantId, actorUserId.Value, cancellationToken)) : Forbid();
    }

    [HttpGet("policies/{policyId:guid}/available-types")]
    public async Task<IActionResult> GetAvailableTypes(Guid policyId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var workspace = await _service.GetPolicyWorkspaceAsync(tenantId, policyId, cancellationToken);
        if (workspace is null) return NotFound();
        var catalog = await _service.GetCatalogAsync(tenantId, workspace.Policy.LineOfBusiness, cancellationToken);
        return Ok(catalog.Types);
    }

    [HttpGet("types/{typeCode}/requirements")]
    public async Task<IActionResult> GetRequirements(string typeCode, [FromQuery] Guid tenantId, [FromQuery] Guid policyId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var workspace = await _service.GetPolicyWorkspaceAsync(tenantId, policyId, cancellationToken);
        if (workspace is null) return NotFound();
        var catalog = await _service.GetCatalogAsync(tenantId, workspace.Policy.LineOfBusiness, cancellationToken);
        var item = catalog.Types.SingleOrDefault(x => string.Equals(x.TypeCode, typeCode, StringComparison.OrdinalIgnoreCase));
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("catalog/{endorsementTypeId:guid}/profile")]
    public async Task<IActionResult> UpdateTypeProfile(Guid endorsementTypeId, [FromBody] UpdatePolicyEndorsementTypeProfileRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_MANAGE")) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!actorUserId.HasValue) return Forbid();
        request.ModifiedByUserId = actorUserId;
        await _service.UpdateTypeProfileAsync(endorsementTypeId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/approvals/{approvalId:guid}/assignment")]
    public async Task<IActionResult> AssignApproval(Guid id, Guid approvalId, [FromBody] AssignPolicyEndorsementApprovalRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_APPROVE")) return Forbid();
        request.ActorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!request.ActorUserId.HasValue) return Forbid();
        await _service.AssignApprovalAsync(id, approvalId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/information-requests")]
    public async Task<IActionResult> RequestInformation(Guid id, [FromBody] RequestPolicyEndorsementInformationRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_APPROVE")) return Forbid();
        request.ActorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!request.ActorUserId.HasValue) return Forbid();
        var informationRequestId = await _service.RequestInformationAsync(id, request, cancellationToken);
        return Ok(new { Id = informationRequestId });
    }

    [HttpPost("{id:guid}/information-requests/{informationRequestId:guid}/response")]
    public async Task<IActionResult> RespondToInformationRequest(Guid id, Guid informationRequestId, [FromBody] RespondPolicyEndorsementInformationRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanAccessPolicyEndorsementWorkflow(User, request.TenantId)) return Forbid();
        request.ActorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!request.ActorUserId.HasValue) return Forbid();
        await _service.RespondToInformationRequestAsync(id, informationRequestId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/information-requests/{informationRequestId:guid}/resubmission")]
    public async Task<IActionResult> ResubmitInformationRequest(Guid id, Guid informationRequestId, [FromBody] ResubmitPolicyEndorsementInformationRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanAccessPolicyEndorsementWorkflow(User, request.TenantId)) return Forbid();
        request.ActorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!request.ActorUserId.HasValue) return Forbid();
        await _service.ResubmitInformationRequestAsync(id, informationRequestId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("catalog/{endorsementTypeId:guid}/configuration")]
    public async Task<IActionResult> ReplaceTypeConfiguration(Guid endorsementTypeId, [FromBody] ReplacePolicyEndorsementTypeConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, request.TenantId, "ENDORSEMENT_MANAGE")) return Forbid();
        var actorUserId = AuthenticatedRequestContext.GetUserId(User);
        if (!actorUserId.HasValue) return Forbid();
        request.ModifiedByUserId = actorUserId;
        await _service.ReplaceTypeConfigurationAsync(endorsementTypeId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var item = await _service.GetDetailAsync(tenantId, id, cancellationToken);
        if (item is not null && !CanViewFinancial(tenantId)) RedactFinancial(item.Endorsement);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/workflow")]
    public async Task<IActionResult> GetWorkflowDetail(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var item = await _service.GetWorkflowDetailAsync(tenantId, id, cancellationToken);
        if (item is not null && !CanViewFinancial(tenantId)) RedactFinancial(item);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("policies/{policyId:guid}/workspace")]
    public async Task<IActionResult> GetPolicyWorkspace(Guid policyId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_VIEW")) return Forbid();
        var item = await _service.GetPolicyWorkspaceAsync(tenantId, policyId, cancellationToken);
        if (item is not null && !CanViewFinancial(tenantId)) RedactFinancial(item);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePolicyEndorsementRequest request, CancellationToken cancellationToken)
        => LegacyMutationGone();

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
        => LegacyMutationGone();

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] Guid tenantId, [FromBody] UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken)
        => LegacyMutationGone();

    [HttpPost("activities")]
    public async Task<IActionResult> AddActivity([FromQuery] Guid tenantId, [FromBody] AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken)
        => LegacyMutationGone();

    [HttpPost("deltas")]
    public async Task<IActionResult> UpsertDelta([FromQuery] Guid tenantId, [FromBody] UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken)
        => LegacyMutationGone();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, [FromQuery] Guid tenantId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
        => LegacyMutationGone();

    private bool CanViewFinancial(Guid tenantId)
        => AuthenticatedRequestContext.HasPolicyEndorsementPermission(User, tenantId, "ENDORSEMENT_FINANCIAL_VIEW");

    private ObjectResult LegacyMutationGone()
        => StatusCode(StatusCodes.Status410Gone, new ProblemDetails
        {
            Status = StatusCodes.Status410Gone,
            Title = "Legacy endorsement mutation is disabled",
            Detail = "Use the transactional endorsement workflow endpoints."
        });

    private static void RedactFinancial(PolicyEndorsementCenterDto center)
    {
        foreach (var endorsement in center.Endorsements) RedactFinancial(endorsement);
        center.Deltas = center.Deltas.Where(delta => delta.NumericDelta == 0).ToList();
    }

    private static void RedactFinancial(PolicyEndorsementWorkflowDetailDto detail)
    {
        RedactFinancial(detail.Endorsement);
        detail.FinancialImpact = new();
        detail.AccountingWork = [];
        detail.Changes = detail.Changes.Where(change => !string.Equals(change.CategoryCode, "Financial", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var version in detail.Versions) version.SnapshotJson = "{}";
    }

    private static void RedactFinancial(PolicyEndorsementPolicyWorkspaceDto workspace)
    {
        workspace.Policy.AnnualPremium = 0;
        if (workspace.CurrentVersion is not null) workspace.CurrentVersion.SnapshotJson = "{}";
        foreach (var endorsement in workspace.Endorsements) RedactFinancial(endorsement);
    }

    private static void RedactFinancial(PolicyEndorsementDto endorsement)
    {
        endorsement.PremiumDelta = 0;
        endorsement.AgencyFeeDelta = 0;
        endorsement.TaxDelta = 0;
        endorsement.TaxFeeDelta = 0;
        endorsement.TotalCostDelta = 0;
        endorsement.ProratedPremiumDelta = 0;
        endorsement.BillingImpactCode = null;
        endorsement.CommissionImpactCode = null;
        endorsement.BillingInstruction = null;
    }
}
