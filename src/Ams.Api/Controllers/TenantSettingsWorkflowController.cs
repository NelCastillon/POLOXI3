using Ams.Application.Abstractions.Services;
using Ams.Application.Features.TenantSettings;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/tenant-settings/workflow")]
public sealed class TenantSettingsWorkflowController : ControllerBase
{
    private readonly ITenantSettingsWorkflowService _service;

    public TenantSettingsWorkflowController(ITenantSettingsWorkflowService service)
        => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByPage([FromQuery] Guid tenantId, [FromQuery] string pageCode, CancellationToken cancellationToken)
        => Ok(await _service.GetByPageAsync(tenantId, pageCode, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByPage), new { tenantId = request.TenantId, pageCode = request.PageCode }, new { Id = id });
    }

    [HttpPut("{workflowItemId:guid}")]
    public async Task<IActionResult> Update(Guid workflowItemId, [FromBody] UpdateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(workflowItemId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{workflowItemId:guid}/advance")]
    public async Task<IActionResult> Advance(Guid workflowItemId, [FromBody] AdvanceTenantSettingsWorkflowRequest request, CancellationToken cancellationToken)
    {
        await _service.AdvanceAsync(workflowItemId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{workflowItemId:guid}")]
    public async Task<IActionResult> Delete(Guid workflowItemId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(workflowItemId, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
