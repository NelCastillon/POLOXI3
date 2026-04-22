using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("accounts/{accountId:guid}/ai")]
public sealed class AiAccountController : ControllerBase
{
    private readonly IAiService _service;
    public AiAccountController(IAiService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAccountSummary(Guid accountId, CancellationToken cancellationToken)
    {
        var summary = await _service.GetAccountSummaryAsync(accountId, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }
}
