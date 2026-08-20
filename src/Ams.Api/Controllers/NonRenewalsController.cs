using Ams.Application.Abstractions.Services;
using Ams.Application.Features.NonRenewals;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/non-renewals")]
public sealed class NonRenewalsController : ControllerBase
{
    private readonly INonRenewalService _service;

    public NonRenewalsController(INonRenewalService service) => _service = service;

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
    public async Task<IActionResult> Create([FromBody] CreateNonRenewalRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.CreateAsync(request, cancellationToken) });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNonRenewalRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateNonRenewalStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/insured-notification")]
    public async Task<IActionResult> RecordInsuredNotification(Guid id, [FromBody] RecordInsuredNotificationRequest request, CancellationToken cancellationToken)
    {
        await _service.RecordInsuredNotificationAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("activities")]
    public async Task<IActionResult> AddActivity([FromBody] AddNonRenewalActivityRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.AddActivityAsync(request, cancellationToken) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.ArchiveAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
