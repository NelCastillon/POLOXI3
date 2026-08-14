using Ams.Api.Security;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PremiumFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/premium-finance")]
public sealed class PremiumFinanceController(IPremiumFinanceService service) : ControllerBase
{
    [HttpGet("workbench")]
    public async Task<IActionResult> GetWorkbench([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.CanViewPremiumFinance(User, tenantId)
            ? Ok(await service.GetWorkbenchAsync(tenantId, cancellationToken))
            : Forbid();

    [HttpGet("requests/{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanViewPremiumFinance(User, tenantId)) return Forbid();
        var detail = await service.GetDetailAsync(tenantId, id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("sources/{sourceTypeCode}/{sourceId:guid}")]
    public async Task<IActionResult> GetSource(string sourceTypeCode, Guid sourceId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanViewPremiumFinance(User, tenantId)) return Forbid();
        var source = await service.GetSourceAsync(tenantId, sourceTypeCode, sourceId, cancellationToken);
        return source is null ? NotFound() : Ok(source);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest([FromBody] CreatePremiumFinanceRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { CreatedByUserId = ActorUserId, CreatedByName = ActorName };
        return Ok(new { Id = await service.CreateRequestAsync(request, cancellationToken) });
    }

    [HttpPut("requests/{id:guid}")]
    public async Task<IActionResult> UpdateRequest(Guid id, [FromBody] UpdatePremiumFinanceRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        await service.UpdateRequestAsync(id, request with { ModifiedByUserId = ActorUserId }, cancellationToken);
        return NoContent();
    }

    [HttpPatch("requests/{id:guid}/status")]
    public async Task<IActionResult> UpdateRequestStatus(Guid id, [FromBody] UpdatePremiumFinanceStatusRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { ModifiedByUserId = ActorUserId, ModifiedByName = ActorName };
        await service.UpdateRequestStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("quote-options")]
    public async Task<IActionResult> AddQuoteOption([FromBody] AddPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { CreatedByUserId = ActorUserId, CreatedByName = ActorName };
        return Ok(new { Id = await service.AddQuoteOptionAsync(request, cancellationToken) });
    }

    [HttpPost("requests/{id:guid}/select-option")]
    public async Task<IActionResult> SelectQuoteOption(Guid id, [FromBody] SelectPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { SelectedByUserId = ActorUserId, SelectedByName = ActorName };
        await service.SelectQuoteOptionAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("applications")]
    public async Task<IActionResult> SubmitApplication([FromBody] SubmitPremiumFinanceApplicationRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { SubmittedByUserId = ActorUserId, SubmittedByName = ActorName };
        return Ok(new { Id = await service.SubmitApplicationAsync(request, cancellationToken) });
    }

    [HttpPatch("agreements/{id:guid}")]
    public async Task<IActionResult> UpdateAgreement(Guid id, [FromBody] UpdatePremiumFinanceAgreementRequest request, CancellationToken cancellationToken)
    {
        if (id != request.FinanceAgreementId) return BadRequest("Agreement route does not match request.");
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { ModifiedByUserId = ActorUserId, ModifiedByName = ActorName };
        await service.UpdateAgreementAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPut("agreements/{id:guid}/payment-schedule")]
    public async Task<IActionResult> ReplacePaymentSchedule(Guid id, [FromBody] ReplacePremiumFinancePaymentScheduleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.FinanceAgreementId) return BadRequest("Agreement route does not match request.");
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { ModifiedByUserId = ActorUserId, ModifiedByName = ActorName };
        await service.ReplacePaymentScheduleAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("activities")]
    public async Task<IActionResult> AddActivity([FromBody] AddPremiumFinanceActivityRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { CreatedByUserId = ActorUserId, CreatedByName = ActorName };
        return Ok(new { Id = await service.AddActivityAsync(request, cancellationToken) });
    }

    [HttpPost("documents")]
    public async Task<IActionResult> LinkDocument([FromBody] LinkPremiumFinanceDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        return Ok(new { Id = await service.LinkDocumentAsync(request with { CreatedByUserId = ActorUserId }, cancellationToken) });
    }

    [HttpPost("providers")]
    public async Task<IActionResult> UpsertProvider([FromBody] UpsertPremiumFinanceProviderRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage(request.TenantId)) return Forbid();
        return Ok(new { Id = await service.UpsertProviderAsync(request with { UserId = ActorUserId }, cancellationToken) });
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id, [FromBody] CancelPremiumFinanceRequest request, CancellationToken cancellationToken)
    {
        if (id != request.PremiumFinanceRequestId) return BadRequest("Request route does not match request.");
        if (!CanManage(request.TenantId)) return Forbid();
        request = request with { CancelledByUserId = ActorUserId, CancelledByName = ActorName };
        await service.CancelRequestAsync(request, cancellationToken);
        return NoContent();
    }

    private Guid? ActorUserId => AuthenticatedRequestContext.GetUserId(User);
    private string? ActorName => User.Identity?.Name;
    private bool CanManage(Guid tenantId) => AuthenticatedRequestContext.CanManagePremiumFinance(User, tenantId);
}
