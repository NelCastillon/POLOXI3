using Ams.Application.Abstractions.Services;
using Ams.Application.Features.QuotaViolations;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/quota-violations")]
public sealed class QuotaViolationsController : ControllerBase
{
    private readonly IQuotaViolationService _service;

    public QuotaViolationsController(IQuotaViolationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm   = null,
        [FromQuery] string? statusCode   = null,
        [FromQuery] string? severityCode = null,
        [FromQuery] int     pageNumber   = 1,
        [FromQuery] int     pageSize     = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(searchTerm, statusCode, severityCode, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var violation = await _service.GetByIdAsync(id, cancellationToken);
        return violation is null ? NotFound() : Ok(violation);
    }

    [HttpGet("open-count")]
    public async Task<IActionResult> GetOpenCount(CancellationToken cancellationToken = default)
    {
        var count = await _service.GetOpenCountAsync(cancellationToken);
        return Ok(new { Count = count });
    }

    [HttpPatch("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        await _service.AcknowledgeAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        await _service.ResolveAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/notify")]
    public async Task<IActionResult> Notify(Guid id, [FromBody] NotifyQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        await _service.NotifyAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/restrict")]
    public async Task<IActionResult> ApplyRestriction(Guid id, [FromBody] ApplyRestrictionRequest request, CancellationToken cancellationToken = default)
    {
        await _service.ApplyRestrictionAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/temporary-increase")]
    public async Task<IActionResult> GrantTemporaryIncrease(Guid id, [FromBody] GrantTemporaryIncreaseRequest request, CancellationToken cancellationToken = default)
    {
        await _service.GrantTemporaryIncreaseAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/convert-to-overage")]
    public async Task<IActionResult> ConvertToOverage(Guid id, [FromBody] ConvertToOverageRequest request, CancellationToken cancellationToken = default)
    {
        await _service.ConvertToOverageAsync(id, request, cancellationToken);
        return NoContent();
    }
}
