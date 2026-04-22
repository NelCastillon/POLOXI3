using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/appetite")]
public sealed class AppetiteFinderController : ControllerBase
{
    private readonly ISubmissionService _service;
    public AppetiteFinderController(ISubmissionService service) => _service = service;

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] AppetiteSearchRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SearchAppetiteAsync(request, cancellationToken));
}
