using Ams.Application.Abstractions.Services;
using Ams.Application.Features.RenewalRetention;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/renewal-retention")]
public sealed class RenewalRetentionController : ControllerBase
{
    private readonly IRenewalRetentionService _service;

    public RenewalRetentionController(IRenewalRetentionService service) => _service = service;

    [HttpGet("center")]
    public async Task<IActionResult> GetCenter([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetCenterAsync(tenantId, cancellationToken));

    [HttpPost("initiate-eligible")]
    public async Task<IActionResult> InitiateEligible([FromBody] InitiateEligibleRenewalsRequest request, CancellationToken cancellationToken)
        => Ok(await _service.InitiateEligibleAsync(request, cancellationToken));

    [HttpPost("cases/{id:guid}/launch-placement")]
    public async Task<IActionResult> LaunchPlacement(Guid id, [FromBody] LaunchRenewalPlacementRequest request, CancellationToken cancellationToken)
    {
        await _service.LaunchPlacementAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("cases/{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDetailAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("cases")]
    public async Task<IActionResult> CreateCase([FromBody] CreateRenewalRetentionCaseRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.CreateCaseAsync(request, cancellationToken) });

    [HttpPatch("cases/{id:guid}/stage")]
    public async Task<IActionResult> UpdateStage(Guid id, [FromBody] UpdateRenewalRetentionStageRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateStageAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("activities")]
    public async Task<IActionResult> AddActivity([FromBody] CreateRenewalRetentionActivityRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.AddActivityAsync(request, cancellationToken) });

    [HttpPost("offers")]
    public async Task<IActionResult> AddOffer([FromBody] CreateRenewalRetentionOfferRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.AddOfferAsync(request, cancellationToken) });

    [HttpPatch("offers/{id:guid}/status")]
    public async Task<IActionResult> UpdateOfferStatus(Guid id, [FromBody] UpdateRenewalRetentionOfferStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateOfferStatusAsync(id, request, cancellationToken);
        return NoContent();
    }
}
