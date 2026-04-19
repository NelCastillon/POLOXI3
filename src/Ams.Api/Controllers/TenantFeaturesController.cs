using Ams.Application.Abstractions.Services;
using Ams.Application.Features.TenantFeatures;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/features")]
public sealed class TenantFeaturesController : ControllerBase
{
    private readonly ITenantFeatureService _service;

    public TenantFeaturesController(ITenantFeatureService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByTenant(Guid tenantId, CancellationToken cancellationToken = default)
        => Ok(await _service.GetByTenantAsync(tenantId, cancellationToken));

    [HttpPost("override")]
    public async Task<IActionResult> Override(Guid tenantId, [FromBody] OverrideTenantFeatureRequest request, CancellationToken cancellationToken = default)
    {
        await _service.UpsertOverrideAsync(tenantId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{featureCode}/enable")]
    public async Task<IActionResult> Enable(Guid tenantId, string featureCode, CancellationToken cancellationToken = default)
    {
        await _service.SetEnabledAsync(tenantId, featureCode, true, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{featureCode}/disable")]
    public async Task<IActionResult> Disable(Guid tenantId, string featureCode, CancellationToken cancellationToken = default)
    {
        await _service.SetEnabledAsync(tenantId, featureCode, false, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{featureCode}/reset")]
    public async Task<IActionResult> Reset(Guid tenantId, string featureCode, CancellationToken cancellationToken = default)
    {
        await _service.ResetToDefaultAsync(tenantId, featureCode, cancellationToken);
        return NoContent();
    }
}
