using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyEndorsements;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/policy-endorsements")]
public sealed class PolicyEndorsementsController : ControllerBase
{
    private readonly IPolicyEndorsementService _service;

    public PolicyEndorsementsController(IPolicyEndorsementService service) => _service = service;

    [HttpGet("center")]
    public async Task<IActionResult> GetCenter([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetCenterAsync(tenantId, cancellationToken));

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetOptionsAsync(tenantId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDetailAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePolicyEndorsementRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.CreateAsync(request, cancellationToken) });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("activities")]
    public async Task<IActionResult> AddActivity([FromBody] AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.AddActivityAsync(request, cancellationToken) });

    [HttpPost("deltas")]
    public async Task<IActionResult> UpsertDelta([FromBody] UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.UpsertDeltaAsync(request, cancellationToken) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.ArchiveAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
