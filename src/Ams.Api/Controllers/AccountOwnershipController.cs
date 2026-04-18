using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/client/account-ownership")]
public sealed class AccountOwnershipController : ControllerBase
{
    private readonly IAccountOwnerHistoryService _service;

    public AccountOwnershipController(IAccountOwnerHistoryService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] Guid? accountId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, accountId, searchTerm, pageNumber, pageSize, cancellationToken));
}
