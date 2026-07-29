using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/client-acceptances")]
public sealed class ClientAcceptancesController : ControllerBase
{
    private readonly ISubmissionService _service;

    public ClientAcceptancesController(ISubmissionService service) => _service = service;

    [HttpGet("readiness")]
    public async Task<IActionResult> GetReadiness([FromQuery] Guid proposalId, [FromQuery] Guid? quoteId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!CanAccess(tenantId, "CLIENT_ACCEPTANCE_VIEW", out var denied)) return denied;
        return Ok(await _service.GetClientAcceptanceReadinessAsync(proposalId, quoteId, tenantId, cancellationToken));
    }

    [HttpGet("submission/{submissionId:guid}")]
    public async Task<IActionResult> GetForSubmission(Guid submissionId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!CanAccess(tenantId, "CLIENT_ACCEPTANCE_VIEW", out var denied)) return denied;
        return Ok(await _service.GetClientAcceptancesAsync(submissionId, tenantId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!CanAccess(tenantId, "CLIENT_ACCEPTANCE_VIEW", out var denied)) return denied;
        var item = await _service.GetClientAcceptanceByIdAsync(id, tenantId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] RecordClientAcceptanceRequest request, CancellationToken cancellationToken)
    {
        if (!CanAccess(request.TenantId, "CLIENT_ACCEPTANCE_RECORD", out var denied)) return denied;
        request.RecordedByUserId = GetUserId();
        request.SignerIpAddress ??= HttpContext.Connection.RemoteIpAddress?.ToString();
        request.UserAgent ??= Request.Headers.UserAgent.ToString();
        try
        {
            var id = await _service.RecordClientAcceptanceAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id, tenantId = request.TenantId }, new { id });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Client acceptance could not be recorded", Detail = exception.Message });
        }
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawClientAcceptanceRequest request, CancellationToken cancellationToken)
    {
        if (!CanAccess(request.TenantId, "CLIENT_ACCEPTANCE_WITHDRAW", out var denied)) return denied;
        try
        {
            await _service.WithdrawClientAcceptanceAsync(id, request with { WithdrawnByUserId = GetUserId() }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Client acceptance could not be withdrawn", Detail = exception.Message });
        }
    }

    private bool CanAccess(Guid tenantId, string permission, out IActionResult denied)
    {
        denied = Forbid();
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        if (tenantId == Guid.Empty || !Guid.TryParse(claim, out var authenticatedTenantId) || authenticatedTenantId != tenantId) return false;
        return User.HasClaim("permission", permission) || User.HasClaim("permission", "NAV_ALL") || User.IsInRole("SYSTEM_ADMIN") || User.IsInRole("TENANT_ADMIN") || User.Identity?.AuthenticationType == "Development";
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
