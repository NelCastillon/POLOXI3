using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/policies")]
public sealed class PolicyBindController : ControllerBase
{
    private readonly ISubmissionService _service;
    public PolicyBindController(ISubmissionService service) => _service = service;

    [HttpPost("bind")]
    public async Task<IActionResult> Bind([FromBody] BindPolicyRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.BindPolicyAsync(request, cancellationToken);
        return Ok(new { id });
    }
}
