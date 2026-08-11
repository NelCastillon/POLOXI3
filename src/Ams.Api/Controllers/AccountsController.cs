using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Accounts;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountService _service;

    public AccountsController(IAccountService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        if (!AuthenticatedRequestContext.CanManageAccounts(User, tenantId.Value)) return Forbid();
        request.TenantId = tenantId.Value;
        request.CreatedByUserId = AuthenticatedRequestContext.GetUserId(User);
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(id);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/contacts")]
    public async Task<IActionResult> GetContacts(Guid id, CancellationToken cancellationToken)
    {
        var contacts = await _service.GetContactsByAccountIdAsync(id, cancellationToken);
        return Ok(contacts);
    }

    [HttpGet("{id:guid}/360")]
    public async Task<IActionResult> GetAccount360(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.HasTenantAccess(User, tenantId)) return Forbid();
        var account = await _service.GetAccount360Async(tenantId, id, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPut("{id:guid}/360/service-assignments")]
    public async Task<IActionResult> ReplaceServiceAssignments(Guid id, [FromBody] ReplaceAccountServiceAssignmentsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.AccountId) return BadRequest("Account route does not match request.");
        if (!AuthenticatedRequestContext.CanManageAccounts(User, request.TenantId)) return Forbid();
        request.ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User);
        await _service.ReplaceServiceAssignmentsAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/360/named-insureds")]
    public async Task<IActionResult> UpsertNamedInsured(Guid id, [FromBody] UpsertAccountNamedInsuredRequest request, CancellationToken cancellationToken)
    {
        if (id != request.AccountId) return BadRequest("Account route does not match request.");
        return Ok(await _service.UpsertNamedInsuredAsync(request, cancellationToken));
    }

    [HttpPost("{id:guid}/360/locations")]
    public async Task<IActionResult> UpsertLocation(Guid id, [FromBody] UpsertAccountLocationRequest request, CancellationToken cancellationToken)
    {
        if (id != request.AccountId) return BadRequest("Account route does not match request.");
        return Ok(await _service.UpsertLocationAsync(request, cancellationToken));
    }

    [HttpPost("{id:guid}/360/vehicles")]
    public async Task<IActionResult> UpsertVehicle(Guid id, [FromBody] UpsertAccountVehicleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.AccountId) return BadRequest("Account route does not match request.");
        return Ok(await _service.UpsertVehicleAsync(request, cancellationToken));
    }

    [HttpPost("{id:guid}/360/drivers")]
    public async Task<IActionResult> UpsertDriver(Guid id, [FromBody] UpsertAccountDriverRequest request, CancellationToken cancellationToken)
    {
        if (id != request.AccountId) return BadRequest("Account route does not match request.");
        return Ok(await _service.UpsertDriverAsync(request, cancellationToken));
    }

    [HttpPost("{id:guid}/360/properties")]
    public async Task<IActionResult> UpsertProperty(Guid id, [FromBody] UpsertAccountPropertyRequest request, CancellationToken cancellationToken)
    {
        if (id != request.AccountId) return BadRequest("Account route does not match request.");
        return Ok(await _service.UpsertPropertyAsync(request, cancellationToken));
    }

    [HttpPost("{id:guid}/360/schedule-items")]
    public async Task<IActionResult> UpsertScheduleItem(Guid id, [FromBody] UpsertAccountScheduleItemRequest request, CancellationToken cancellationToken)
    {
        if (id != request.AccountId) return BadRequest("Account route does not match request.");
        return Ok(await _service.UpsertScheduleItemAsync(request, cancellationToken));
    }

    [HttpDelete("{id:guid}/360/{entityType}/{entityId:guid}")]
    public async Task<IActionResult> DeleteAccount360Item(Guid id, string entityType, Guid entityId, [FromQuery] Guid tenantId, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.DeleteAccount360ItemAsync(tenantId, id, entityType, entityId, userId, cancellationToken);
        return NoContent();
    }
}
