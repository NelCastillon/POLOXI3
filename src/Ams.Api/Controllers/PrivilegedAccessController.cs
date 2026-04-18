using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/iam/pam")]
public sealed class PrivilegedAccessController : ControllerBase
{
    private readonly IPrivilegedAccessService _pamService;
    private readonly ISodRuleService _sodService;
    private readonly IAccessReviewService _reviewService;

    public PrivilegedAccessController(IPrivilegedAccessService pamService, ISodRuleService sodService, IAccessReviewService reviewService)
    {
        _pamService = pamService;
        _sodService = sodService;
        _reviewService = reviewService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPamById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _pamService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> SearchPam([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _pamService.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("sod/{id:guid}")]
    public async Task<IActionResult> GetSodById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _sodService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("sod")]
    public async Task<IActionResult> SearchSod([FromQuery] Guid? tenantId, [FromQuery] string? searchTerm, [FromQuery] string? severityCode = null, [FromQuery] bool? isActive = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _sodService.SearchAsync(tenantId, searchTerm, severityCode, isActive, pageNumber, pageSize, cancellationToken));

    [HttpGet("reviews/{id:guid}")]
    public async Task<IActionResult> GetReviewById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _reviewService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> SearchReviews([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _reviewService.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
}
