using Ams.Application.Abstractions.Services;
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
    public async Task<IActionResult> GetById(Guid quoteId, CancellationToken cancellationToken)
    {
        var item = await _service.GetQuoteByIdAsync(quoteId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
