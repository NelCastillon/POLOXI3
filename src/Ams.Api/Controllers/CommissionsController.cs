using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Commissions;
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

    [HttpPost("payees")]
    public async Task<IActionResult> CreatePayee([FromBody] CreateCommissionPayeeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreatePayeeAsync(request, cancellationToken));

    [HttpPost("seed")]
    public async Task<IActionResult> EnsureSeed([FromQuery] Guid tenantId, [FromQuery] Guid? createdByUserId = null, CancellationToken cancellationToken = default)
    {
        await _service.EnsureSeedAsync(tenantId, createdByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPut("payees/{id:guid}")]
    public async Task<IActionResult> UpdatePayee(Guid id, [FromBody] UpdateCommissionPayeeRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdatePayeeAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("transactions/{id:guid}")]
    public async Task<IActionResult> GetTransactionById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetTransactionByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> SearchTransactions([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchTransactionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateCommissionTransactionRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateTransactionAsync(request, cancellationToken));

    [HttpPut("transactions/{id:guid}")]
    public async Task<IActionResult> UpdateTransaction(Guid id, [FromBody] UpdateCommissionTransactionRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateTransactionAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("payouts/{id:guid}")]
    public async Task<IActionResult> GetPayoutById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetPayoutByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> SearchPayouts([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchPayoutsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost("payouts")]
    public async Task<IActionResult> CreatePayout([FromBody] CreateCommissionPayoutRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreatePayoutAsync(request, cancellationToken));

    [HttpPut("payouts/{id:guid}")]
    public async Task<IActionResult> UpdatePayout(Guid id, [FromBody] UpdateCommissionPayoutRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdatePayoutAsync(id, request, cancellationToken);
        return NoContent();
    }
}
