using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Duplicates;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/duplicates")]
public sealed class DuplicatesController : ControllerBase
{
    private readonly IDuplicateService _service;

    public DuplicatesController(IDuplicateService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid tenantId,
        [FromQuery] string? entityType = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? confidenceBand = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(new DuplicateSearchRequest
        {
            TenantId = tenantId,
            EntityType = entityType,
            SearchTerm = searchTerm,
            ConfidenceBand = confidenceBand,
            StatusCode = statusCode,
            PageNumber = pageNumber,
            PageSize = pageSize
        }, cancellationToken);

        return Ok(result);
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] DuplicateScanRequest request, CancellationToken cancellationToken = default)
        => Ok(await _service.ScanAsync(request, cancellationToken));

    [HttpPatch("{groupId:guid}/primary")]
    public async Task<IActionResult> SetPrimary(Guid groupId, [FromBody] DuplicateSetPrimaryRequest request, CancellationToken cancellationToken = default)
    {
        await _service.SetPrimaryAsync(groupId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{groupId:guid}/merge")]
    public async Task<IActionResult> Merge(Guid groupId, [FromBody] DuplicateResolveRequest request, CancellationToken cancellationToken = default)
    {
        await _service.MergeAsync(groupId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{groupId:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid groupId, [FromBody] DuplicateResolveRequest request, CancellationToken cancellationToken = default)
    {
        await _service.DismissAsync(groupId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("bulk-merge")]
    public async Task<IActionResult> BulkMerge([FromBody] DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default)
    {
        await _service.BulkMergeAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("bulk-dismiss")]
    public async Task<IActionResult> BulkDismiss([FromBody] DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default)
    {
        await _service.BulkDismissAsync(request, cancellationToken);
        return NoContent();
    }
}
