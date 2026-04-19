using Ams.Application.Abstractions.Services;
using Ams.Application.Features.TenantDeploymentAssignments;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/tenant-deployment-assignments")]
public sealed class TenantDeploymentAssignmentsController : ControllerBase
{
    private readonly ITenantDeploymentAssignmentService _service;

    public TenantDeploymentAssignmentsController(ITenantDeploymentAssignmentService service)
        => _service = service;

    [HttpGet("{tenantId:guid}")]
    public async Task<IActionResult> GetByTenantId(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var assignment = await _service.GetByTenantIdAsync(tenantId, cancellationToken);
        return assignment is null ? NotFound() : Ok(assignment);
    }

    [HttpPut("{tenantId:guid}")]
    public async Task<IActionResult> Upsert(Guid tenantId, [FromBody] UpsertTenantDeploymentAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        request.TenantId = tenantId;
        var id = await _service.UpsertAsync(request, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpDelete("{tenantId:guid}")]
    public async Task<IActionResult> Delete(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(tenantId, cancellationToken);
        return NoContent();
    }
}
