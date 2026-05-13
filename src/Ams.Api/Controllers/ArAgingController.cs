using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Billing;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/billing/ar-aging")]
public sealed class ArAgingController : ControllerBase
{
    private readonly IArAgingSnapshotService _service;
    public ArAgingController(IArAgingSnapshotService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateArAgingSnapshotRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateArAgingSnapshotRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromQuery] Guid tenantId, [FromQuery] DateOnly? snapshotDate, [FromQuery] Guid? createdByUserId, CancellationToken cancellationToken)
        => Ok(await _service.SyncFromInvoicesAsync(tenantId, snapshotDate ?? DateOnly.FromDateTime(DateTime.UtcNow), createdByUserId, cancellationToken));
}
