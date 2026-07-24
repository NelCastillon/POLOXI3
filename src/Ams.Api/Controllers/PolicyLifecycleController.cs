using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyLifecycle;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
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
        => Ok(await _service.GetOptionsAsync(tenantId, cancellationToken));

    [HttpGet("workbench")]
    public async Task<IActionResult> GetWorkbench([FromQuery] Guid tenantId, [FromQuery] string? mode, CancellationToken cancellationToken)
        => Ok(await _service.GetWorkbenchAsync(tenantId, mode, cancellationToken));

    [HttpGet("policies/{policyId:guid}")]
    public async Task<IActionResult> GetDetail([FromQuery] Guid tenantId, Guid policyId, CancellationToken cancellationToken)
    {
        var detail = await _service.GetDetailAsync(tenantId, policyId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreatePolicyLifecycleTransactionRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.CreateTransactionAsync(request, cancellationToken) });

    [HttpPut("transactions/{policyTransactionId:guid}/status")]
    public async Task<IActionResult> TransitionTransaction(Guid policyTransactionId, [FromBody] TransitionPolicyLifecycleTransactionRequest request, CancellationToken cancellationToken)
    {
        await _service.TransitionTransactionAsync(policyTransactionId, request, cancellationToken);
        return NoContent();
    }
}
