using Ams.Application.Abstractions.Services;
using Ams.Application.Features.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/background-jobs")]
public sealed class BackgroundJobsController : ControllerBase
{
    private readonly IBackgroundJobService _service;

    public BackgroundJobsController(IBackgroundJobService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string?   searchTerm  = null,
        [FromQuery] string?   jobTypeCode = null,
        [FromQuery] string?   statusCode  = null,
        [FromQuery] Guid?     tenantId    = null,
        [FromQuery] bool?     failedOnly  = null,
        [FromQuery] DateTime? fromDateUtc = null,
        [FromQuery] DateTime? toDateUtc   = null,
        [FromQuery] int       pageNumber  = 1,
        [FromQuery] int       pageSize    = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(searchTerm, jobTypeCode, statusCode, tenantId, failedOnly, fromDateUtc, toDateUtc, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _service.GetByIdAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPatch("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, [FromBody] RetryBackgroundJobRequest request, CancellationToken cancellationToken = default)
    {
        await _service.RetryAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBackgroundJobRequest request, CancellationToken cancellationToken = default)
    {
        await _service.CancelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/requeue")]
    public async Task<IActionResult> Requeue(Guid id, [FromBody] RequeueBackgroundJobRequest request, CancellationToken cancellationToken = default)
    {
        await _service.RequeueAsync(id, cancellationToken);
        return NoContent();
    }
}
