using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _service;
    public SubscriptionsController(ISubscriptionService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm = null, [FromQuery] Guid? tenantId = null, [FromQuery] Guid? planId = null, [FromQuery] string? statusCode = null, [FromQuery] string? renewalType = null, [FromQuery] string? billingCycle = null, [FromQuery] bool? pastDue = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(searchTerm, tenantId, planId, statusCode, renewalType, billingCycle, pastDue, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPatch("{id:guid}/upgrade")]
    public async Task<IActionResult> Upgrade(Guid id, [FromBody] UpgradeSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await _service.UpgradeAsync(id, request.PlanId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/downgrade")]
    public async Task<IActionResult> Downgrade(Guid id, [FromBody] DowngradeSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await _service.DowngradeAsync(id, request.PlanId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/renew")]
    public async Task<IActionResult> Renew(Guid id, [FromBody] RenewSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await _service.RenewAsync(id, request.NewEndDateUtc, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _service.CancelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
