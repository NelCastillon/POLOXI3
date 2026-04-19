using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/tenant-domains")]
public sealed class TenantDomainsController : ControllerBase
{
    private readonly ITenantDomainService _service;
    public TenantDomainsController(ITenantDomainService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid? tenantId = null, [FromQuery] string? searchTerm = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (tenantId.HasValue)
            return Ok(await _service.SearchByTenantAsync(tenantId.Value, searchTerm, pageNumber, pageSize, cancellationToken));
        return Ok(await _service.SearchAllAsync(searchTerm, pageNumber, pageSize, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantDomainRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPatch("{id:guid}/redirect")]
    public async Task<IActionResult> UpdateRedirect(Guid id, [FromBody] UpdateTenantDomainRedirectRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateRedirectAsync(id, request.RedirectTarget, request.Notes, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{tenantId:guid}/set-primary/{domainId:guid}")]
    public async Task<IActionResult> SetPrimary(Guid tenantId, Guid domainId, CancellationToken cancellationToken)
    {
        await _service.SetPrimaryAsync(tenantId, domainId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken cancellationToken)
    {
        await _service.VerifyAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
