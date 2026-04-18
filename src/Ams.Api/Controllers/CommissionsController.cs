using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CommissionsController : ControllerBase
{
    private readonly ICommissionService _service;
    public CommissionsController(ICommissionService service) => _service = service;

    [HttpGet("payees/{id:guid}")]
    public async Task<IActionResult> GetPayeeById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetPayeeByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("payees")]
    public async Task<IActionResult> SearchPayees([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchPayeesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("transactions/{id:guid}")]
    public async Task<IActionResult> GetTransactionById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetTransactionByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> SearchTransactions([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchTransactionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("payouts/{id:guid}")]
    public async Task<IActionResult> GetPayoutById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetPayoutByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> SearchPayouts([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchPayoutsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
}
