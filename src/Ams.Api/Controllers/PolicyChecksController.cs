using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyChecks;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/policy-checks")]
public sealed class PolicyChecksController : ControllerBase
{
    private readonly IPolicyCheckService _service;

    public PolicyChecksController(IPolicyCheckService service) => _service = service;

    [HttpGet("center")]
    public async Task<IActionResult> GetCenter([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetCenterAsync(tenantId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDetailAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePolicyCheckRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.CreateAsync(request, cancellationToken) });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePolicyCheckRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePolicyCheckStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdatePolicyCheckItemRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateItemAsync(itemId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("discrepancies")]
    public async Task<IActionResult> AddDiscrepancy([FromBody] AddPolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.AddDiscrepancyAsync(request, cancellationToken) });

    [HttpPatch("discrepancies/{discrepancyId:guid}")]
    public async Task<IActionResult> ResolveDiscrepancy(Guid discrepancyId, [FromBody] ResolvePolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken)
    {
        await _service.ResolveDiscrepancyAsync(discrepancyId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("activities")]
    public async Task<IActionResult> AddActivity([FromBody] AddPolicyCheckActivityRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.AddActivityAsync(request, cancellationToken) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.ArchiveAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
