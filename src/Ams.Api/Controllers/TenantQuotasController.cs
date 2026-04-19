using Ams.Application.Abstractions.Services;
using Ams.Application.Features.TenantQuotas;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/tenant-quotas")]
public sealed class TenantQuotasController : ControllerBase
{
    private readonly ITenantQuotaService _service;

    public TenantQuotasController(ITenantQuotaService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize  = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(searchTerm, statusCode, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-tenant/{tenantId:guid}")]
    public async Task<IActionResult> GetByTenant(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = await _service.GetByTenantAsync(tenantId, cancellationToken);
        return Ok(items);
    }

    [HttpPut("by-tenant/{tenantId:guid}")]
    public async Task<IActionResult> Upsert(Guid tenantId, [FromBody] UpsertTenantQuotaRequest request, CancellationToken cancellationToken = default)
    {
        var id = await _service.UpsertAsync(tenantId, request, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/override-limit")]
    public async Task<IActionResult> OverrideLimit(Guid id, [FromBody] OverrideLimitRequest request, CancellationToken cancellationToken = default)
    {
        await _service.OverrideLimitAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/reset-override")]
    public async Task<IActionResult> ResetOverride(Guid id, [FromBody] ResetOverrideRequest request, CancellationToken cancellationToken = default)
    {
        await _service.ResetOverrideAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/notify")]
    public async Task<IActionResult> NotifyTenant(Guid id, [FromBody] NotifyTenantQuotaRequest request, CancellationToken cancellationToken = default)
    {
        await _service.NotifyTenantAsync(id, request, cancellationToken);
        return NoContent();
    }
}
