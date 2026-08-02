using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Operations;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ops/service-requests")]
public sealed class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;
    public ServiceRequestsController(IServiceRequestService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanViewPolicy(User, tenantId)) return Forbid();
        var item = await _service.GetByIdAsync(tenantId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] Guid? accountId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => AuthenticatedRequestContext.CanViewPolicy(User, tenantId)
            ? Ok(await _service.SearchAsync(tenantId, accountId, searchTerm, pageNumber, pageSize, cancellationToken))
            : Forbid();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequestRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        request.CreatedByUserId = AuthenticatedRequestContext.GetUserId(User);
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServiceRequestRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        request.ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User);
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid tenantId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, tenantId)) return Forbid();
        await _service.DeleteAsync(tenantId, id, AuthenticatedRequestContext.GetUserId(User), cancellationToken);
        return NoContent();
    }
}
