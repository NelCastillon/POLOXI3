using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public sealed class QuoteComparisonController : ControllerBase
{
    private readonly ISubmissionService _service;
    public QuoteComparisonController(ISubmissionService service) => _service = service;

    [HttpGet("compare/{submissionId:guid}")]
    public async Task<IActionResult> Compare(Guid submissionId, CancellationToken cancellationToken)
        => Ok(await _service.GetQuoteComparisonAsync(submissionId, cancellationToken));

    [HttpGet("{quoteId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid quoteId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        if (tenantId == Guid.Empty || !Guid.TryParse(claim, out var authenticatedTenantId) || authenticatedTenantId != tenantId)
            return Forbid();

        var item = await _service.GetQuoteByIdAsync(quoteId, tenantId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
