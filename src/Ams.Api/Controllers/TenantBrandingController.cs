using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/platform/branding")]
public sealed class TenantBrandingController : ControllerBase
{
    private readonly ITenantBrandingService _service;
    public TenantBrandingController(ITenantBrandingService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("tenant/{tenantId:guid}")]
    public async Task<IActionResult> GetByTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        var item = await _service.GetByTenantIdAsync(tenantId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPut("tenant/{tenantId:guid}")]
    public async Task<IActionResult> Update(Guid tenantId, [FromBody] UpdateTenantBrandingRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(tenantId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("tenant/{tenantId:guid}/reset")]
    public async Task<IActionResult> ResetToDefaults(Guid tenantId, CancellationToken cancellationToken)
    {
        await _service.ResetToDefaultsAsync(tenantId, cancellationToken);
        return NoContent();
    }
}
