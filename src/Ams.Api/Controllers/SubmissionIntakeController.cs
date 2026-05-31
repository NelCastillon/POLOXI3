using Ams.Application.Abstractions.Services;
using Ams.Application.Features.SubmissionIntake;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

/// <summary>
/// Direct submission intake. Stages out-of-band submissions and normalizes them into the
/// mandatory Account -> Opportunity -> Submission chain so no submission is orphaned.
/// </summary>
[ApiController]
[Route("api/submission-intake")]
public sealed class SubmissionIntakeController : ControllerBase
{
    private readonly ISubmissionIntakeService _service;
    public SubmissionIntakeController(ISubmissionIntakeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] string? source,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, status, source, pageNumber, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Capture([FromBody] CreateSubmissionIntakeRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CaptureAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubmissionIntakeRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/match")]
    public async Task<IActionResult> PreviewMatch(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.PreviewMatchAsync(id, cancellationToken));

    [HttpPost("{id:guid}/promote")]
    public async Task<IActionResult> Promote(Guid id, [FromBody] PromoteSubmissionIntakeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.PromoteAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSubmissionIntakeStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
