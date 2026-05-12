using Ams.Application.Abstractions.Services;
using Ams.Application.Features.BillingAccounts;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/billing/accounts")]
public sealed class BillingAccountsController : ControllerBase
{
    private readonly IBillingAccountService _service;

    public BillingAccountsController(IBillingAccountService service)
    {
        _service = service;
    }

    [HttpPost("ensure-seed")]
    public async Task<IActionResult> EnsureSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await _service.EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 250, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetById(Guid accountId, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(accountId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBillingAccountRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{accountId:guid}")]
    public async Task<IActionResult> Update(Guid accountId, [FromBody] UpdateBillingAccountRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(accountId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{accountId:guid}")]
    public async Task<IActionResult> Delete(Guid accountId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(accountId, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
